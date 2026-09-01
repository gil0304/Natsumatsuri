using Matsuri.Core;
using Matsuri.TimeSystem;
using UnityEngine;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 来場のペースと出現 (§8 / §33)。
    ///
    /// 何人来るかは「時刻」と「祭りの魅力」で決まる。
    /// 屋台が多く、種類が豊富で、居場所（盆踊り場・休憩所・神社）があるほど人が集まる。
    /// </summary>
    public sealed partial class VisitorManager
    {
        /// <summary>
        /// 来場ペースを進め、余った端数が1人ぶんたまったら出現させる。
        ///
        /// ArrivalCurve は「ゲーム内1分あたりの人数」なので、実時間へ直してから積む。
        /// こうしておくと祭りの長さ (§7) を変えても総来場者数が変わらない。
        /// </summary>
        void UpdateArrivals(float dt, FestivalClock clock)
        {
            if (!_arriving || _balance == null || _pool == null || _catalog == null) return;

            if (_total >= Mathf.Max(1, _balance.MaxTotalVisitors)) return;

            float curve = _balance.ArrivalCurve != null
                ? _balance.ArrivalCurve.Evaluate(clock.Normalized)
                : 2f;
            if (curve <= 0f) return;

            float minutesPerSecond = Mathf.Max(0.0001f, _balance.MinutesPerRealSecond);
            _arrivalAccumulator += curve * minutesPerSecond * Attractiveness() * EventAttractMultiplier() * dt;

            // ためすぎると、空きが出た瞬間に一気に湧いて不自然になる。
            if (_arrivalAccumulator > 120f) _arrivalAccumulator = 120f;

            int room = Capacity() - _active.Count;
            int budget = Mathf.Min(Mathf.Max(0, _maxSpawnPerFrame), room);

            while (_arrivalAccumulator >= 1f && budget > 0)
            {
                _arrivalAccumulator -= 1f;
                if (!SpawnOne()) break;
                budget--;
                if (_total >= Mathf.Max(1, _balance.MaxTotalVisitors)) break;
            }
        }

        /// <summary>
        /// 祭りの魅力 (§33)。屋台が1軒も無ければ閑散とする。
        /// 種類の豊富さと居場所の数が効く。
        /// </summary>
        float Attractiveness()
        {
            if (_balance == null) return 1f;

            var game = GameManager.Instance;
            var stalls = game != null ? game.Stalls : null;
            int stallCount = stalls != null ? stalls.Stalls.Count : 0;

            if (stallCount <= 0) return Mathf.Max(0.01f, _balance.EmptyFestivalArrivalMultiplier);

            int kinds = stalls != null ? stalls.DistinctStallKinds : 0;
            int amenities = Festival.AmenityRegistry.All.Count;
            int ambience = _world.AmbienceSourceCount;

            // 軒数そのものより「種類」と「居場所」を厚く見る。
            // 同じ屋台を並べるだけでは祭りは賑わわない。
            float score = stallCount + kinds * 2.5f + amenities * 3f + ambience * 0.6f;
            return 1f + score * Mathf.Max(0f, _balance.AttractionToArrivalScale);
        }

        /// <summary>花火や盆踊りの最中は人が集まってくる (§22)。</summary>
        float EventAttractMultiplier()
        {
            var game = GameManager.Instance;
            var events = game != null ? game.Events : null;
            if (events == null) return 1f;
            return Mathf.Max(0.1f, events.AttractMultiplier);
        }

        /// <summary>1人ぶん出現させる。プールが尽きていたら false。</summary>
        bool SpawnOne()
        {
            var archetype = _catalog.PickArchetype(ref _rng);
            if (archetype == null) return false;

            uint seed = _rng.NextUInt();
            if (seed == 0u) seed = 1u;

            var agent = _pool.Rent(archetype, seed);
            if (agent == null) return false;

            // 入り口にきっちり重ならないよう、門の幅ぶん散らす。
            Vector3 pos = _entrance + new Vector3(
                _rng.NextFloat(-3.5f, 3.5f), 0f, _rng.NextFloat(-1.8f, 1.8f));
            if (NavigationService.TrySample(pos, out var onMesh, 6f)) pos = onMesh;

            int buckets = Mathf.Max(1, _bucketAccum != null ? _bucketAccum.Length : 1);
            agent.Manager = this;
            agent.Bucket = _bucketAssign;
            _bucketAssign = (_bucketAssign + 1) % buckets;

            agent.Spawn(archetype, pos, seed);

            // 圧縮された祭り時間 (§7) に合わせて歩調を上げる。
            float speedScale = _balance != null ? _balance.EffectiveVisitorSpeedScale : 1f;
            if (speedScale > 0f && !Mathf.Approximately(speedScale, 1f))
                agent.WalkingSpeed *= speedScale;

            _lod.Register(agent);
            _active.Add(agent);
            _total++;
            if (_active.Count > _peak) _peak = _active.Count;
            return true;
        }

        /// <summary>
        /// 入場したNPCが最初に向かう点。
        /// 全員が同じ場所を目指すと門の前で詰まるので、会場の中ほどに散らす。
        /// </summary>
        public Vector3 PickEntryTarget(ref MRandom rng)
        {
            Vector3 center = _world.WanderCenter;
            float radius = Mathf.Max(6f, _world.WanderRadius);

            float angle = rng.NextFloat(0f, Mathf.PI * 2f);
            float r = radius * Mathf.Sqrt(rng.NextFloat(0.15f, 1f));
            Vector3 target = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            // 入り口の真後ろへ戻る向きになってしまう場合は、会場側へ寄せる。
            if (target.z < _entrance.z + 4f) target.z = _entrance.z + 4f + Mathf.Abs(target.z - _entrance.z) * 0.5f;

            if (NavigationService.TrySample(target, out var onMesh, 8f)) return onMesh;
            return target;
        }
    }
}
