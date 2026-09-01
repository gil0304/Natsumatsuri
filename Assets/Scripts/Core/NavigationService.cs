using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Matsuri.Core
{
    /// <summary>
    /// NavMesh のランタイム再ベイクを一手に引き受ける (§12 / §28)。
    ///
    /// 屋台・装飾・設備は Matsuri Script の実行によって「その場で」建つため、
    /// 1件建つたびに BuildNavMesh() を呼ぶと数十回のフルベイクが発生して確実にフレームが飛ぶ。
    /// そこで RequestRebake() の連続呼び出しを 0.3 秒デバウンスして 1 回にまとめる。
    ///
    /// またベイクが 1 度も終わっていない状態でも NPC が壊れてはいけないので、
    /// SamplePosition() は「NavMesh が無ければ false を返すだけ」という契約にしてある。
    /// NPC 側は false のときは NavMeshAgent を使わず簡易直線移動に落とす (§57)。
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class NavigationService : MonoBehaviour
    {
        /// <summary>連続する RequestRebake() をまとめる時間（秒）。</summary>
        public const float RebakeDebounceSeconds = 0.3f;

        public static NavigationService Instance { get; private set; }

        [SerializeField] NavMeshSurface _surface;

        float _dueTime = -1f;
        bool _pending;
        bool _everBaked;
        AsyncOperation _running;

        /// <summary>いま貼られている NavMeshSurface。未設定なら null。</summary>
        public NavMeshSurface Surface => _surface;

        /// <summary>ベイク処理の途中かどうか。</summary>
        public bool IsBaking => _running != null && !_running.isDone;

        /// <summary>1度でもベイクが完了して、歩ける床が存在するか。</summary>
        public bool HasNavMesh => _everBaked;

        /// <summary>再ベイクが完了した瞬間。NPC は自分の足元を貼り直すのに使う。</summary>
        public event Action Rebaked;

        /// <summary>デバウンス待ちの再ベイク予約があるか。</summary>
        public bool HasPendingRebake => _pending;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // シーンに2つ置かれてしまった場合は後から来た方を黙って捨てる。
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>ベイク対象の NavMeshSurface を差し替える。</summary>
        public void SetSurface(NavMeshSurface surface)
        {
            if (_surface == surface) return;
            _surface = surface;
            _everBaked = false;
            _running = null;
            if (_surface != null) RequestRebake();
        }

        /// <summary>
        /// 再ベイクを予約する。0.3 秒以内に何度呼ばれても実際のベイクは1回。
        /// 建設演出 (§39) の最中に屋台が次々建っても、最後の1回だけが走る。
        /// </summary>
        public void RequestRebake()
        {
            if (_surface == null) return;
            _pending = true;
            _dueTime = Time.unscaledTime + RebakeDebounceSeconds;
        }

        /// <summary>
        /// 建った物が NavMesh をくり抜くようにする (§28)。
        /// 地面だけをベイク対象にしているので、屋台は「障害物」として道を塞ぐ。
        /// 行列は屋台の前に伸びるため、本体だけを覆い、前面は歩けるまま残す。
        /// </summary>
        public static void AddCarvingObstacle(GameObject root, float maxHeight = 3.0f, float shrink = 0.2f)
        {
            if (root == null) return;
            if (root.GetComponent<NavMeshObstacle>() != null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            bool any = false;
            Bounds b = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                // 提灯や電球のように宙に浮く小物まで含めると、
                // 実際より大きく道を塞いでしまう。低い位置の部材だけを見る。
                if (r.bounds.center.y - root.transform.position.y > maxHeight) continue;

                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = root.transform.InverseTransformPoint(b.center);

            var size = b.size;
            size.x = Mathf.Max(0.4f, size.x - shrink * 2f);
            size.z = Mathf.Max(0.4f, size.z - shrink * 2f);
            size.y = Mathf.Max(0.6f, Mathf.Min(size.y, maxHeight));
            obstacle.size = size;

            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingMoveThreshold = 0.2f;
        }

        /// <summary>デバウンスを待たずに即ベイクする。祭り開始直前などで使う。</summary>
        public void RebakeNow()
        {
            if (_surface == null) return;
            _pending = false;
            Bake();
        }

        void Update()
        {
            if (_running != null && _running.isDone)
            {
                _running = null;
                _everBaked = true;
                Rebaked?.Invoke();
            }

            if (!_pending || _surface == null) return;
            if (IsBaking) return;                      // 走っている間は次を待たせる
            if (Time.unscaledTime < _dueTime) return;

            _pending = false;
            Bake();
        }

        void Bake()
        {
            if (_surface == null) return;

            try
            {
                if (!_everBaked || _surface.navMeshData == null)
                {
                    // 初回は同期ベイク。まだ NPC がほとんど居ないので止まっても目立たない。
                    _surface.BuildNavMesh();
                    _everBaked = _surface.navMeshData != null;
                    _running = null;
                    Rebaked?.Invoke();
                }
                else
                {
                    // 2回目以降は非同期更新。祭りの最中にフレームを飛ばさない。
                    _running = _surface.UpdateNavMesh(_surface.navMeshData);
                    if (_running == null)
                    {
                        _surface.BuildNavMesh();
                        Rebaked?.Invoke();
                    }
                }
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"NavMesh のベイクに失敗しました: {e.Message}");
                _running = null;
            }
        }

        /// <summary>
        /// 目的地を NavMesh の上に吸着させる。
        /// NavMesh が無い / まだベイクできていない場合は false を返し、onNavMesh には desired をそのまま返す。
        /// </summary>
        public bool SamplePosition(Vector3 desired, out Vector3 onNavMesh, float maxDistance = 4f)
        {
            onNavMesh = desired;
            if (maxDistance <= 0f) maxDistance = 4f;

            // _everBaked を見ずに常に試す。シーンに元から NavMesh がある場合も拾えるようにするため。
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                onNavMesh = hit.position;
                return true;
            }
            return false;
        }

        /// <summary>NavigationService が無くても落ちない静的ショートカット。</summary>
        public static bool TrySample(Vector3 desired, out Vector3 onNavMesh, float maxDistance = 4f)
        {
            var inst = Instance;
            if (inst != null) return inst.SamplePosition(desired, out onNavMesh, maxDistance);

            onNavMesh = desired;
            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                onNavMesh = hit.position;
                return true;
            }
            return false;
        }

        /// <summary>NavigationService が無ければ作る。Bootstrap から呼ばれる。</summary>
        public static NavigationService Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            if (host == null) host = new GameObject("NavigationService");
            var svc = host.GetComponent<NavigationService>();
            if (svc == null) svc = host.AddComponent<NavigationService>();
            return svc;
        }
    }
}
