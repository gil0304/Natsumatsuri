using System.Collections;
using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Data;
using UnityEngine;
using UnityEngine.AI;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場者 GameObject のプール (§57「Object Pooling を使う」)。
    ///
    /// 300〜1000人ぶんの体を祭りの最中に Instantiate すると確実にフレームが飛ぶので、
    /// 事前生成する。ただし事前生成そのものも重いため、1フレームに数体ずつに分散する。
    ///
    /// 1000人ぶん (§56) になると「1フレーム何体」だけでは足りない。
    /// 体の複雑さはアーキタイプで変わるので、体数の上限に加えて
    /// **1フレームの生成に使ってよい時間** も決め、どちらか先に尽きた方で打ち切る。
    /// 生成枠は事前生成と Rent() で共有する。祭りの最中に湧いた人が
    /// 事前生成の枠を無視して体を作り始めたら、分散させた意味が無くなるため。
    ///
    /// 体（メッシュ・服の色・体格）はアーキタイプごとに違うので、
    /// アーキタイプ別の空きリストを持ち、同じアーキタイプの個体を使い回す。
    /// どうしても足りないときだけ体を作り直す。
    /// </summary>
    public sealed class VisitorPool : MonoBehaviour
    {
        /// <summary>1フレームの生成に使ってよい時間 (ミリ秒)。60fps の 1フレーム 16.6ms のうちの一部。</summary>
        const double CreateBudgetMilliseconds = 2.5;

        MatsuriCatalog _catalog;
        Transform _root;
        int _capacity = 300;
        int _perFrame = 6;
        MRandom _rng = new MRandom(0x4D415453u);

        readonly System.Diagnostics.Stopwatch _frameWatch = new System.Diagnostics.Stopwatch();
        int _budgetFrame = -1;
        int _createdThisFrame;

        readonly List<VisitorAgent> _all = new List<VisitorAgent>();
        readonly Dictionary<VisitorArchetype, Stack<VisitorAgent>> _free =
            new Dictionary<VisitorArchetype, Stack<VisitorAgent>>();
        readonly List<VisitorArchetype> _freeKeys = new List<VisitorArchetype>();
        // アーキタイプが未設定（体がまだ無い）個体の置き場。Dictionary は null キーを許さないので分けて持つ。
        readonly Stack<VisitorAgent> _freeUnknown = new Stack<VisitorAgent>();

        Coroutine _prewarm;

        /// <summary>事前生成の目標数。</summary>
        public int Capacity => _capacity;

        /// <summary>これまでに作った実体の数。</summary>
        public int CreatedCount => _all.Count;

        /// <summary>いま貸し出せる数。</summary>
        public int FreeCount
        {
            get
            {
                int n = _freeUnknown.Count;
                for (int i = 0; i < _freeKeys.Count; i++)
                {
                    if (_free.TryGetValue(_freeKeys[i], out var stack)) n += stack.Count;
                }
                return n;
            }
        }

        /// <summary>事前生成が終わったか。</summary>
        public bool IsPrewarmComplete => _all.Count >= _capacity;

        public void Configure(MatsuriCatalog catalog, Transform root, int capacity, int perFrame = 6, uint seed = 0x4D415453u)
        {
            _catalog = catalog;
            _root = root != null ? root : transform;
            _capacity = Mathf.Max(1, capacity);
            _perFrame = Mathf.Clamp(perFrame, 1, 64);
            if (_rng.state == 0u || _all.Count == 0) _rng = new MRandom(seed == 0u ? 1u : seed);
        }

        // ------------------------------------------------------------------
        // 1フレームあたりの生成枠 (§57)
        // ------------------------------------------------------------------

        /// <summary>いまのフレームで、あと1体作ってよいか。</summary>
        bool HasCreateBudget()
        {
            int frame = Time.frameCount;
            if (_budgetFrame != frame)
            {
                _budgetFrame = frame;
                _createdThisFrame = 0;
                _frameWatch.Restart();
            }

            if (_createdThisFrame >= _perFrame) return false;
            return _frameWatch.Elapsed.TotalMilliseconds < CreateBudgetMilliseconds;
        }

        /// <summary>事前生成を開始する。生成コストをフレームに分散する。</summary>
        public void BeginPrewarm()
        {
            if (_prewarm != null) StopCoroutine(_prewarm);
            if (isActiveAndEnabled) _prewarm = StartCoroutine(PrewarmRoutine());
        }

        IEnumerator PrewarmRoutine()
        {
            while (_all.Count < _capacity)
            {
                // 体数の枠と時間の枠、どちらか先に尽きた方でこのフレームは打ち切る。
                while (_all.Count < _capacity && HasCreateBudget())
                {
                    var archetype = PickArchetype();
                    var agent = CreateAgent(archetype);
                    if (agent != null) Push(agent);
                }
                yield return null;
            }
            _prewarm = null;
            MatsuriLog.Info($"来場者プールの事前生成が完了しました（{_all.Count}体）。");
        }

        VisitorArchetype PickArchetype()
        {
            if (_catalog == null) return null;
            return _catalog.PickArchetype(ref _rng);
        }

        VisitorAgent CreateAgent(VisitorArchetype archetype)
        {
            // 非アクティブのまま組み立てる。
            // NavMeshAgent はアクティブな状態で AddComponent すると
            // 「NavMesh の上にいない」警告を出すため、必ずこの順番で作る。
            var go = new GameObject("Visitor");
            go.SetActive(false);
            go.transform.SetParent(_root, false);

            var nav = go.AddComponent<NavMeshAgent>();
            VisitorAgent.ConfigureNavAgent(nav);
            nav.enabled = false;

            var agent = go.AddComponent<VisitorAgent>();
            if (archetype != null)
                agent.BuildBody(archetype, _rng.NextUInt() | 1u);

            _all.Add(agent);
            _createdThisFrame++;
            return agent;
        }

        /// <summary>
        /// 1体貸し出す。空きが無ければその場で作る（上限とフレーム枠は超えない）。
        /// 枠切れのときは null を返すので、呼び出し側は次のフレームに回すこと。
        /// </summary>
        public VisitorAgent Rent(VisitorArchetype archetype, uint seed)
        {
            VisitorAgent agent = Pop(archetype);

            if (agent == null)
            {
                // 別アーキタイプの空きを流用する。体は作り直しになる。
                agent = PopAny();
                if (agent != null && archetype != null && agent.BodyArchetype != archetype)
                {
                    // 体の作り直しも生成と同じくらい重い。枠が無ければ見送る。
                    if (!HasCreateBudget()) { Push(agent); return null; }
                    agent.BuildBody(archetype, seed);
                    _createdThisFrame++;
                }
            }

            if (agent == null)
            {
                if (_all.Count >= _capacity) return null;   // 上限に達している
                if (!HasCreateBudget()) return null;        // 今フレームの生成枠が尽きた
                agent = CreateAgent(archetype);
            }

            if (agent == null) return null;

            agent.gameObject.SetActive(true);
            return agent;
        }

        /// <summary>使い終わった1体を返す。完全リセットしてから空きリストへ戻す。</summary>
        public void Return(VisitorAgent agent)
        {
            if (agent == null) return;
            agent.ResetForPool();
            agent.Manager = null;
            agent.gameObject.SetActive(false);
            agent.transform.SetParent(_root, false);
            Push(agent);
        }

        /// <summary>全部まとめて返す。祭りのリセット時に使う。</summary>
        public void ReturnAll()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var a = _all[i];
                if (a == null) continue;
                if (a.gameObject.activeSelf) Return(a);
            }
        }

        /// <summary>プールごと破棄する。</summary>
        public void Clear()
        {
            if (_prewarm != null) { StopCoroutine(_prewarm); _prewarm = null; }
            for (int i = 0; i < _all.Count; i++)
            {
                var a = _all[i];
                if (a != null) Destroy(a.gameObject);
            }
            _all.Clear();
            _free.Clear();
            _freeKeys.Clear();
            _freeUnknown.Clear();
        }

        // ------------------------------------------------------------------
        // アーキタイプ別の空きリスト
        // ------------------------------------------------------------------

        void Push(VisitorAgent agent)
        {
            var key = agent.BodyArchetype;
            if (key == null) { _freeUnknown.Push(agent); return; }
            if (!_free.TryGetValue(key, out var stack))
            {
                stack = new Stack<VisitorAgent>();
                _free[key] = stack;
                _freeKeys.Add(key);
            }
            stack.Push(agent);
        }

        VisitorAgent Pop(VisitorArchetype archetype)
        {
            if (archetype == null) return null;
            if (!_free.TryGetValue(archetype, out var stack)) return null;
            while (stack.Count > 0)
            {
                var a = stack.Pop();
                if (a != null) return a;
            }
            return null;
        }

        VisitorAgent PopAny()
        {
            while (_freeUnknown.Count > 0)
            {
                var u = _freeUnknown.Pop();
                if (u != null) return u;
            }
            for (int i = 0; i < _freeKeys.Count; i++)
            {
                if (!_free.TryGetValue(_freeKeys[i], out var stack)) continue;
                while (stack.Count > 0)
                {
                    var a = stack.Pop();
                    if (a != null) return a;
                }
            }
            return null;
        }
    }
}
