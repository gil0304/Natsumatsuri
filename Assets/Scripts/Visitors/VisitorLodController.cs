using System.Collections.Generic;
using Matsuri.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Matsuri.Visitors
{
    /// <summary>来場者の距離段 (§57)。</summary>
    public enum VisitorLodLevel
    {
        /// <summary>近距離。影も歩行アニメも NavMeshAgent も全部あり。</summary>
        Near = 0,

        /// <summary>中距離。影を落とさず、歩行アニメを止める。姿は見える。</summary>
        Mid = 1,

        /// <summary>遠距離。Renderer ごと切る。位置の計算だけ続く。</summary>
        Far = 2
    }

    /// <summary>
    /// 来場者の描画負荷を距離で落とす係 (§56 / §57)。
    ///
    /// 1000人を出すと、素直に描くだけで
    ///   ・影のためにもう一度全員を描く（シャドウマップ）
    ///   ・全員ぶんの手続き歩行アニメが毎フレーム Transform を触る
    /// の2つが効いてくる。そこでカメラからの距離で3段に分ける:
    ///
    ///   近 … そのまま。影も歩行アニメもある。NavMeshAgent もここだけ。
    ///   中 … 影を落とさない + 歩行アニメを止める。立ち姿は見える。
    ///   遠 … Renderer ごと無効化する。人数の計算と移動だけ続く。
    ///
    /// **段の切り替えにはヒステリシスを入れる**。境界上で行ったり来たりすると
    /// 影と姿が点滅して、かえって目立つため。
    ///
    /// もう一つの仕事が「NavMeshAgent を何体まで有効にするか」の配分 (§57)。
    /// 早い者勝ちで枠を配ると、最初に湧いた220人が枠を握ったまま会場の隅に居座り、
    /// カメラの目の前の人が簡易移動になってしまう。
    /// そこで**カメラからの距離のヒストグラム**を作り、
    /// 「上位N体がぎりぎり収まる半径」を毎回求めて、それを簡易化の境界にする。
    /// こうすると枠は自然にカメラに近い人へ回る。
    /// </summary>
    public sealed class VisitorLodController
    {
        sealed class Entry
        {
            public VisitorAgent Agent;
            public Renderer[] Renderers;
            public ProceduralWalkAnimator Walk;
            public VisitorLodLevel Level;
            public bool Live;      // いま会場に出ているか
            public bool Applied;   // 一度でも段を適用したか
        }

        /// <summary>距離ヒストグラムの段数。細かすぎても粗すぎても意味が無いのでこの程度。</summary>
        const int HistogramBins = 48;

        /// <summary>段を戻すときの余裕。0.88 なら境界の12%内側まで近づかないと戻らない。</summary>
        const float Hysteresis = 0.88f;

        /// <summary>簡易化距離が動く速さ (m/秒)。急に変えると影が一斉に消えて目立つ。</summary>
        const float AdaptSpeed = 30f;

        readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(512);
        readonly int[] _histogram = new int[HistogramBins];

        float _baseSimplifyDistance = 45f;
        float _farCullDistance = 95f;
        float _simplifyDistance = 45f;
        int _maxDetailed = 220;

        int _nearCount, _midCount, _farCount;

        /// <summary>いま実際に使われている簡易化距離。人数に応じて縮む。</summary>
        public float SimplifyDistance => _simplifyDistance;

        /// <summary>Renderer を切る距離。</summary>
        public float FarCullDistance => _farCullDistance;

        public int NearCount => _nearCount;
        public int MidCount => _midCount;
        public int FarCount => _farCount;

        /// <summary>登録されている（＝体を把握している）人数。</summary>
        public int TrackedCount => _entries.Count;

        // ================================================================
        // 設定
        // ================================================================

        /// <summary>
        /// 距離の設定。
        /// </summary>
        /// <param name="simplifyDistance">BalanceConfig.VisitorSimplifyDistance。簡易化距離の上限。</param>
        /// <param name="farCullDistance">Renderer を切る距離。</param>
        /// <param name="maxDetailedVisitors">詳細更新（＝NavMeshAgent 持ち）にしてよい人数の上限。</param>
        public void Configure(float simplifyDistance, float farCullDistance, int maxDetailedVisitors)
        {
            _baseSimplifyDistance = Mathf.Max(5f, simplifyDistance);
            _farCullDistance = Mathf.Max(_baseSimplifyDistance * 1.2f, farCullDistance);
            _maxDetailed = Mathf.Max(1, maxDetailedVisitors);
            _simplifyDistance = _baseSimplifyDistance;
        }

        // ================================================================
        // 予算の再計算（低頻度でよい）
        // ================================================================

        /// <summary>
        /// カメラからの距離を数え直して、簡易化距離を決める。
        /// 「近い順に _maxDetailed 体」がちょうど入る半径を探す。
        /// 毎フレームやる必要はない（VisitorManager が 0.3秒に1回ほど呼ぶ）。
        /// </summary>
        public void UpdateBudget(IReadOnlyList<VisitorAgent> agents, Vector3 cameraPosition, float dt)
        {
            float target = _baseSimplifyDistance;

            if (agents != null && agents.Count > _maxDetailed)
            {
                for (int i = 0; i < HistogramBins; i++) _histogram[i] = 0;

                float binWidth = _baseSimplifyDistance / HistogramBins;
                float invBin = 1f / Mathf.Max(0.0001f, binWidth);

                for (int i = 0; i < agents.Count; i++)
                {
                    var v = agents[i];
                    if (v == null) continue;

                    float d = FlatDistance(v.Position, cameraPosition);
                    if (d >= _baseSimplifyDistance) continue;   // もともと簡易側なので数えない

                    int bin = (int)(d * invBin);
                    if (bin < 0) bin = 0;
                    else if (bin >= HistogramBins) bin = HistogramBins - 1;
                    _histogram[bin]++;
                }

                int accumulated = 0;
                target = binWidth;   // 最低でも1段ぶんは詳細のままにする
                for (int i = 0; i < HistogramBins; i++)
                {
                    if (accumulated + _histogram[i] > _maxDetailed) break;
                    accumulated += _histogram[i];
                    target = (i + 1) * binWidth;
                }
            }

            // 一気に動かすと影が波打つ。少しずつ寄せる。
            float step = Mathf.Max(0.5f, AdaptSpeed * Mathf.Max(0f, dt));
            _simplifyDistance = Mathf.MoveTowards(_simplifyDistance, target, step);
        }

        /// <summary>この距離なら簡易更新にすべきか。ヒステリシスつき (§57)。</summary>
        public bool ShouldSimplify(bool currentlySimplified, float distance)
        {
            float threshold = currentlySimplified ? _simplifyDistance * Hysteresis : _simplifyDistance;
            return distance > threshold;
        }

        // ================================================================
        // 登録・解除
        // ================================================================

        /// <summary>
        /// 出現した人を登録する。体の Renderer をここで1回だけ集める。
        /// プールで使い回される体は登録も使い回されるので、
        /// GetComponentsInChildren は1体につき1回しか走らない。
        /// </summary>
        public void Register(VisitorAgent agent)
        {
            if (agent == null) return;

            int id = agent.GetInstanceID();
            if (!_entries.TryGetValue(id, out var entry))
            {
                entry = new Entry { Agent = agent };
                _entries[id] = entry;
            }

            entry.Live = true;
            if (NeedsScan(entry)) Scan(entry);
            ApplyLevel(entry, VisitorLodLevel.Near, true);
        }

        /// <summary>プールに返すときに呼ぶ。次に貸し出されたとき見えないままにならないよう戻す。</summary>
        public void Release(VisitorAgent agent)
        {
            if (agent == null) return;
            if (!_entries.TryGetValue(agent.GetInstanceID(), out var entry)) return;

            entry.Live = false;
            RestoreRenderers(entry);
            entry.Level = VisitorLodLevel.Near;
            entry.Applied = false;
        }

        /// <summary>全部忘れる。プールを作り直すときに呼ぶ。</summary>
        public void Clear()
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Agent == null) continue;
                RestoreRenderers(entry);
            }
            _entries.Clear();
            _nearCount = _midCount = _farCount = 0;
            _simplifyDistance = _baseSimplifyDistance;
        }

        // ================================================================
        // 段の適用
        // ================================================================

        /// <summary>
        /// 1人ぶんの段を決めて適用する。
        /// VisitorManager が距離を測ったその場で呼ぶので、距離をもう一度測り直さない。
        /// </summary>
        /// <param name="agent">対象。</param>
        /// <param name="distance">カメラからの平面距離。</param>
        /// <param name="simplified">VisitorAgent 側が簡易更新になっているか。</param>
        public void Apply(VisitorAgent agent, float distance, bool simplified)
        {
            if (agent == null) return;
            if (!_entries.TryGetValue(agent.GetInstanceID(), out var entry)) { Register(agent); return; }

            if (NeedsScan(entry)) Scan(entry);

            VisitorLodLevel want;
            if (!simplified)
            {
                want = VisitorLodLevel.Near;
            }
            else
            {
                bool wasFar = entry.Level == VisitorLodLevel.Far;
                float threshold = wasFar ? _farCullDistance * Hysteresis : _farCullDistance;
                want = distance > threshold ? VisitorLodLevel.Far : VisitorLodLevel.Mid;
            }

            ApplyLevel(entry, want, false);
        }

        /// <summary>
        /// 全員ぶんを一度に評価し直す。
        /// カメラが瞬間移動したとき（来場者視点への切り替えなど）に呼ぶ。
        /// 分散更新を待っていると、消えたままの人が数百ミリ秒残ってしまうため。
        /// </summary>
        public void RefreshAll(IReadOnlyList<VisitorAgent> agents, Vector3 cameraPosition)
        {
            if (agents == null) return;
            for (int i = 0; i < agents.Count; i++)
            {
                var v = agents[i];
                if (v == null) continue;
                Apply(v, FlatDistance(v.Position, cameraPosition), v.IsSimplified);
            }
        }

        /// <summary>いま何人がどの段かを数え直す。デバッグ表示用。</summary>
        public void RecountLevels()
        {
            _nearCount = _midCount = _farCount = 0;
            foreach (var entry in _entries.Values)
            {
                if (!entry.Live) continue;
                switch (entry.Level)
                {
                    case VisitorLodLevel.Near: _nearCount++; break;
                    case VisitorLodLevel.Mid:  _midCount++;  break;
                    default:                   _farCount++;  break;
                }
            }
        }

        // ================================================================
        // 内部
        // ================================================================

        void ApplyLevel(Entry entry, VisitorLodLevel level, bool force)
        {
            if (!force && entry.Applied && entry.Level == level) return;

            entry.Level = level;
            entry.Applied = true;

            bool visible = level != VisitorLodLevel.Far;
            var shadows = level == VisitorLodLevel.Near ? ShadowCastingMode.On : ShadowCastingMode.Off;

            var renderers = entry.Renderers;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    r.enabled = visible;
                    r.shadowCastingMode = shadows;
                }
            }

            // 歩行アニメは近距離だけ。
            // VisitorAgent 側も簡易化のときに同じことをするが、境界が同じなので食い違わない。
            if (entry.Walk != null)
            {
                bool wantWalk = level == VisitorLodLevel.Near;
                if (entry.Walk.enabled != wantWalk) entry.Walk.enabled = wantWalk;
            }
        }

        void RestoreRenderers(Entry entry)
        {
            var renderers = entry.Renderers;
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.enabled = true;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        /// <summary>体が作り直されていないか。作り直されると古い Renderer は破棄済みになる。</summary>
        static bool NeedsScan(Entry entry)
        {
            var renderers = entry.Renderers;
            if (renderers == null || renderers.Length == 0) return true;
            return renderers[0] == null;
        }

        void Scan(Entry entry)
        {
            var agent = entry.Agent;
            if (agent == null) { entry.Renderers = null; entry.Walk = null; return; }

            entry.Renderers = agent.GetComponentsInChildren<Renderer>(true);
            entry.Walk = agent.GetComponentInChildren<ProceduralWalkAnimator>(true);
            entry.Applied = false;
        }

        internal static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
