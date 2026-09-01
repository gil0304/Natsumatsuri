using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Festival;
using Matsuri.Stalls;
using UnityEngine;

namespace Matsuri.Visitors
{
    /// <summary>
    /// NPC が毎回シーンを走査しないための「会場の要約」 (§57)。
    ///
    /// 300〜1000人のNPCが、思考のたびに屋台リスト・装飾リスト・周囲の人数を
    /// 自分で数え直すと O(NPC × オブジェクト) になって破綻する。
    /// ここで1秒に1回だけ会場をまとめ直し、NPC は出来上がった表を引くだけにする。
    /// 混雑度は 5m の空間ハッシュで O(1) 参照。
    /// </summary>
    public sealed class VisitorWorldCache
    {
        struct AmbienceSource
        {
            public Vector3 Position;
            public float Radius;
            public float Value;
        }

        const float CellSize = 5f;
        const float RestSearchRadius = 30f;

        readonly List<Stall> _stalls = new List<Stall>();
        readonly List<Facility> _benches = new List<Facility>();
        readonly List<AmbienceSource> _ambience = new List<AmbienceSource>();
        readonly Dictionary<int, int> _density = new Dictionary<int, int>();

        /// <summary>いま建っている屋台。</summary>
        public IReadOnlyList<Stall> Stalls => _stalls;

        /// <summary>雰囲気を出している物（装飾・ゴミ箱・案内板）の数。祭りの魅力に使う。</summary>
        public int AmbienceSourceCount => _ambience.Count;

        /// <summary>入り口設備が建っていればその位置。</summary>
        public bool HasEntrance { get; private set; }
        public Vector3 EntrancePosition { get; private set; }

        /// <summary>出口設備が建っていればその位置。</summary>
        public bool HasExit { get; private set; }
        public Vector3 ExitPosition { get; private set; }

        /// <summary>屋台の重心。NPC のぶらぶら歩きの中心になる。</summary>
        public Vector3 WanderCenter { get; private set; }
        public float WanderRadius { get; private set; } = 22f;

        public void SetDefaultWander(Vector3 center, float radius)
        {
            WanderCenter = center;
            WanderRadius = Mathf.Max(8f, radius);
        }

        public void Clear()
        {
            _stalls.Clear();
            _benches.Clear();
            _ambience.Clear();
            _density.Clear();
            HasEntrance = false;
            HasExit = false;
        }

        /// <summary>会場をまとめ直す。1秒に1回程度でよい。</summary>
        public void Refresh()
        {
            _stalls.Clear();
            _benches.Clear();
            _ambience.Clear();
            HasEntrance = false;
            HasExit = false;

            var game = GameManager.Instance;

            var stallManager = game != null ? game.Stalls : null;
            if (stallManager != null && stallManager.Stalls != null)
            {
                var list = stallManager.Stalls;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) _stalls.Add(list[i]);
            }

            var festival = game != null ? game.Festival : null;
            var built = festival != null ? festival.BuiltObjects : null;
            if (built != null)
            {
                for (int i = 0; i < built.Count; i++)
                {
                    var obj = built[i];
                    if (obj == null) continue;

                    if (obj is Facility facility) CollectFacility(facility);
                    else if (obj is Decoration decoration) CollectDecoration(decoration);
                }
            }

            RecomputeWander();
        }

        void CollectFacility(Facility facility)
        {
            var data = facility.Data;
            if (data == null) return;
            Vector3 pos = facility.transform.position;

            switch (data.Effect)
            {
                case FacilityEffect.Rest:
                    _benches.Add(facility);
                    break;
                case FacilityEffect.Entrance:
                    EntrancePosition = pos; HasEntrance = true;
                    break;
                case FacilityEffect.Exit:
                    ExitPosition = pos; HasExit = true;
                    break;
            }

            // 清潔さ・トイレ・案内板は「居心地のよさ」として雰囲気に足す (§34)。
            if (data.Effect == FacilityEffect.Cleanliness ||
                data.Effect == FacilityEffect.Relief ||
                data.Effect == FacilityEffect.Guidance)
            {
                _ambience.Add(new AmbienceSource
                {
                    Position = pos,
                    Radius = Mathf.Max(1f, data.EffectRadius),
                    Value = data.EffectStrength * 0.25f
                });
            }
        }

        void CollectDecoration(Decoration decoration)
        {
            var data = decoration.Data;
            if (data == null) return;
            _ambience.Add(new AmbienceSource
            {
                Position = decoration.transform.position,
                Radius = Mathf.Max(1f, data.EffectRadius),
                Value = data.AmbienceValue
            });
        }

        void RecomputeWander()
        {
            if (_stalls.Count == 0) return;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _stalls.Count; i++) sum += _stalls[i].transform.position;
            Vector3 center = sum / _stalls.Count;

            float maxDist = 8f;
            for (int i = 0; i < _stalls.Count; i++)
            {
                float d = Vector3.Distance(_stalls[i].transform.position, center);
                if (d > maxDist) maxDist = d;
            }

            WanderCenter = center;
            WanderRadius = Mathf.Clamp(maxDist + 6f, 10f, 60f);
        }

        // ------------------------------------------------------------------
        // 混雑度 (§34)
        // ------------------------------------------------------------------

        public void RebuildDensity(IReadOnlyList<VisitorAgent> agents)
        {
            _density.Clear();
            if (agents == null) return;

            for (int i = 0; i < agents.Count; i++)
            {
                var v = agents[i];
                if (v == null) continue;
                int key = CellKey(v.Position);
                _density.TryGetValue(key, out int n);
                _density[key] = n + 1;
            }
        }

        static int CellKey(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x / CellSize);
            int z = Mathf.FloorToInt(p.z / CellSize);
            return (x * 73856093) ^ (z * 19349663);
        }

        /// <summary>その場所の混雑人数。</summary>
        public int GetCrowdingAt(Vector3 position)
        {
            _density.TryGetValue(CellKey(position), out int n);
            return n;
        }

        /// <summary>その場所の「雰囲気」の合計 (§34 装飾で満足度が上がる)。</summary>
        public float GetAmbienceAt(Vector3 position)
        {
            float total = 0f;
            for (int i = 0; i < _ambience.Count; i++)
            {
                var a = _ambience[i];
                float d = Vector3.Distance(a.Position, position);
                if (d >= a.Radius) continue;
                total += a.Value * (1f - d / a.Radius);
            }
            return total;
        }

        /// <summary>一番近い空きベンチを探し、占有まで済ませる (§20)。</summary>
        public bool TryFindRestSpot(Vector3 from, out Facility facility)
        {
            facility = null;
            if (_benches.Count == 0) return false;

            Facility best = null;
            float bestSq = RestSearchRadius * RestSearchRadius;

            for (int i = 0; i < _benches.Count; i++)
            {
                var b = _benches[i];
                if (b == null) continue;
                float sq = (b.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }

            if (best == null || !best.TryOccupy()) return false;
            facility = best;
            return true;
        }
    }
}
