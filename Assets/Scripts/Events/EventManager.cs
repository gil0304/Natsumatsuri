using System;
using System.Collections.Generic;
using Matsuri.Audio;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Script;
using Matsuri.Visitors;
using UnityEngine;

namespace Matsuri.Events
{
    /// <summary>
    /// 仕様書 §22 / §61。花火・盆踊り・太鼓の開催をまとめる。
    /// 見た目そのものは FireworksController / BonOdoriYagura / TaikoStage が持ち、
    /// 打ち上げの段取りは FireworksDirector が持つ。
    /// ここは「いつ・どこで・誰に効くか」だけを決める (§66)。
    /// 各イベントは自前の Update を持たず、この Update から一括で回す (§57)。
    /// </summary>
    public sealed class EventManager : MonoBehaviour
    {
        [Header("花火 (§61)")]
        [Tooltip("次の玉を打ち上げるまでの間隔（秒）。種類ごとに倍率がかかる。")]
        public float ShotStagger = 0.65f;

        [Tooltip("打ち上げから開花までの時間（秒）。")]
        public float RiseSeconds = 1.15f;

        [Header("太鼓 (§22)")]
        [Tooltip("太鼓1打で近くのNPCの満足度がどれだけ上がるか。")]
        public float TaikoSatisfactionPerBeat = 0.9f;

        [Tooltip("太鼓の効果半径 (m)。")]
        public float TaikoRadius = 22f;

        [Header("盆踊り (§22)")]
        [Tooltip("踊っているNPCの満足度が毎秒どれだけ上がるか。")]
        public float BonOdoriSatisfactionPerSecond = 1.6f;

        /// <summary>いずれかのイベントが始まった瞬間。UI のトーストなどが受ける。</summary>
        public event Action<FestivalEventData> EventStarted;

        /// <summary>まだ空中に玉が残っている間も「開催中」とみなす。</summary>
        public bool IsFireworksActive =>
            (_director != null && _director.LiveShots > 0) ||
            (_fireworksEvent != null && _fireworksEvent.IsActive);

        public bool IsBonOdoriActive => _bonOdoriEvent != null && _bonOdoriEvent.IsActive;

        public bool IsTaikoActive => _taikoEvent != null && _taikoEvent.IsActive;

        /// <summary>開催中イベントの来場者呼び込み倍率の最大値 (§33)。何も無ければ 1。</summary>
        public float AttractMultiplier
        {
            get
            {
                float best = 1f;
                for (int i = 0; i < _active.Count; i++)
                {
                    EventObject e = _active[i];
                    if (e == null || !e.IsActive || e.Data == null) continue;
                    if (e.Data.VisitorAttractMultiplier > best) best = e.Data.VisitorAttractMultiplier;
                }
                return best;
            }
        }

        /// <summary>盆踊りのやぐら。開催していなければ null。NPC が踊りの輪を取りに来る。</summary>
        public BonOdoriYagura Yagura => _yagura;

        /// <summary>太鼓台。開催していなければ null。</summary>
        public TaikoStage Taiko => _taiko;

        /// <summary>会場の範囲。打ち上げ位置の抽選に使う。</summary>
        public GroundBounds VenueBounds
        {
            get
            {
                GameManager gm = GameManager.Instance;
                if (gm != null && gm.Catalog != null) return gm.Catalog.Bounds;
                return GroundBounds.Default;
            }
        }

        readonly List<EventObject> _active = new List<EventObject>(8);
        readonly List<VisitorAgent> _nearbyBuffer = new List<VisitorAgent>(256);

        Transform _root;
        FireworksDirector _director;
        EventObject _fireworksEvent;
        EventObject _bonOdoriEvent;
        EventObject _taikoEvent;
        BonOdoriYagura _yagura;
        TaikoStage _taiko;

        void Awake()
        {
            _root = new GameObject("Events").transform;
            _root.SetParent(transform, false);
            _director = new FireworksDirector(this, _root);
        }

        // ────────────────────────────────────────────────────────
        // 花火 (§22 / §61)
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// 花火をあげる。kind は MatsuriIds の花火種 (kiku / botan / yanagi / heart / special / oodama)。
        /// 会場上空のランチ位置を選び、複数発を少しずつ時間差で打ち上げる。
        /// </summary>
        public void PlayFireworks(string kind)
        {
            if (string.IsNullOrEmpty(kind)) kind = MatsuriIds.FireworkKiku;

            FestivalEventData data = ResolveEventData(MatsuriIds.Fireworks, FestivalEventKind.Fireworks);

            if (_fireworksEvent == null) _fireworksEvent = SpawnEventObject(data, VenueCenter());
            _fireworksEvent.Begin();

            _director.Prepare();

            MatsuriLog.Info($"花火を打ち上げます: {kind}");
            EventStarted?.Invoke(data);

            StartCoroutine(_director.Sequence(kind, data, ShotStagger, RiseSeconds));
        }

        /// <summary>
        /// FireworksDirector から呼ばれる。効果半径内の全員の満足度を一気に上げる (§34)。
        /// </summary>
        internal void NotifyFireworksBurst(Vector3 origin, FestivalEventData data, float scale)
        {
            float radius = data != null ? data.EffectRadius : 200f;
            float burst = (data != null ? data.SatisfactionBurst : 35f) * Mathf.Clamp(scale, 0.2f, 1.6f);

            CollectNearby(origin, radius, _nearbyBuffer);
            for (int i = 0; i < _nearbyBuffer.Count; i++)
                _nearbyBuffer[i].OnFireworks(burst);
        }

        // ────────────────────────────────────────────────────────
        // 盆踊り (§22)
        // ────────────────────────────────────────────────────────

        /// <summary>やぐらを建て、踊りの輪を開く。周囲のNPCを呼び寄せて輪にする。</summary>
        public void StartBonOdori(Vector3 pos)
        {
            FestivalEventData data = ResolveEventData(MatsuriIds.BonOdori, FestivalEventKind.BonOdori);

            if (_yagura == null) _yagura = BonOdoriYagura.Build(pos, _root);
            else _yagura.transform.position = pos;

            if (_bonOdoriEvent == null) _bonOdoriEvent = SpawnEventObject(data, pos);
            else _bonOdoriEvent.Configure(data, pos);
            _bonOdoriEvent.Begin();

            InviteNearbyDancers(pos, data);

            MatsuriLog.Info($"盆踊りを始めます: {pos}");
            EventStarted?.Invoke(data);
        }

        /// <summary>近い人から順に輪のスロットを割り当てる（呼び寄せ）。</summary>
        void InviteNearbyDancers(Vector3 center, FestivalEventData data)
        {
            if (_yagura == null) return;

            float radius = data != null ? Mathf.Max(data.EffectRadius, _yagura.OuterRadius + 6f) : 30f;
            CollectNearby(center, radius, _nearbyBuffer);

            _nearbyBuffer.Sort((a, b) =>
                (a.Position - center).sqrMagnitude.CompareTo((b.Position - center).sqrMagnitude));

            for (int i = 0; i < _nearbyBuffer.Count; i++)
                if (!_yagura.TryReserveSlot(_nearbyBuffer[i], out _)) break;   // 満員
        }

        /// <summary>NPC 側から踊りの輪に加わる。空きが無ければ false。</summary>
        public bool TryJoinBonOdori(VisitorAgent visitor, out Vector3 slotPosition)
        {
            slotPosition = Vector3.zero;
            if (!IsBonOdoriActive || _yagura == null) return false;
            if (!_yagura.TryReserveSlot(visitor, out int index)) return false;
            slotPosition = _yagura.GetDanceSlot(index);
            return true;
        }

        /// <summary>踊りの輪の中での現在の立ち位置。輪は回るので毎フレーム引き直す。</summary>
        public bool TryGetDancePosition(VisitorAgent visitor, out Vector3 slotPosition)
        {
            slotPosition = Vector3.zero;
            if (_yagura == null) return false;
            return _yagura.TryGetReservedPosition(visitor, out slotPosition);
        }

        /// <summary>踊りの輪から抜ける。</summary>
        public void LeaveBonOdori(VisitorAgent visitor) => _yagura?.ReleaseSlot(visitor);

        // ────────────────────────────────────────────────────────
        // 太鼓 (§22)
        // ────────────────────────────────────────────────────────

        /// <summary>太鼓台を置き、一定のリズムで打ち始める。</summary>
        public void StartTaiko(Vector3 pos)
        {
            FestivalEventData data = ResolveEventData(MatsuriIds.Taiko, FestivalEventKind.Taiko);

            if (_taiko == null)
            {
                _taiko = TaikoStage.Build(pos, _root);
                _taiko.Beat += OnTaikoBeat;
            }
            else
            {
                _taiko.transform.position = pos;
            }

            if (_taikoEvent == null) _taikoEvent = SpawnEventObject(data, pos);
            else _taikoEvent.Configure(data, pos);
            _taikoEvent.Begin();

            MatsuriLog.Info($"太鼓演奏を始めます: {pos}");
            EventStarted?.Invoke(data);
        }

        /// <summary>1打ごとに音を鳴らし、近くのNPCの満足度を少し上げる (§34)。</summary>
        void OnTaikoBeat(Vector3 at)
        {
            GameManager gm = GameManager.Instance;
            gm?.Audio?.PlaySfx(MatsuriSfx.TaikoHit, at, 0.85f);

            CollectNearby(at, TaikoRadius, _nearbyBuffer);
            for (int i = 0; i < _nearbyBuffer.Count; i++)
                AddSatisfaction(_nearbyBuffer[i], TaikoSatisfactionPerBeat);
        }

        // ────────────────────────────────────────────────────────
        // 更新と後片付け
        // ────────────────────────────────────────────────────────

        void Update()
        {
            float dt = UnityEngine.Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                EventObject e = _active[i];
                if (e == null) { _active.RemoveAt(i); continue; }
                e.Advance(dt);
            }

            if (_yagura != null && IsBonOdoriActive)
            {
                _yagura.Tick(dt);
                RewardDancers(dt);
            }

            if (_taiko != null)
            {
                if (IsTaikoActive) _taiko.Tick(dt);
                else _taiko.Rest();
            }
        }

        /// <summary>踊っている人の満足度を上げ続ける (§34)。</summary>
        void RewardDancers(float dt)
        {
            IReadOnlyList<VisitorAgent> all = ActiveVisitors();
            if (all == null) return;

            float gain = BonOdoriSatisfactionPerSecond * dt;
            for (int i = 0; i < all.Count; i++)
            {
                VisitorAgent v = all[i];
                if (v == null || !_yagura.IsDancing(v)) continue;
                AddSatisfaction(v, gain);
            }
        }

        /// <summary>すべてのイベントを止め、建てた物を片付ける。</summary>
        public void StopAll()
        {
            StopAllCoroutines();
            _director?.Reset();

            for (int i = 0; i < _active.Count; i++)
            {
                EventObject e = _active[i];
                if (e == null) continue;
                e.Finish();
                Destroy(e.gameObject);
            }
            _active.Clear();

            _fireworksEvent = null;
            _bonOdoriEvent = null;
            _taikoEvent = null;

            if (_yagura != null)
            {
                _yagura.ReleaseAll();
                Destroy(_yagura.gameObject);
                _yagura = null;
            }

            if (_taiko != null)
            {
                _taiko.Beat -= OnTaikoBeat;
                Destroy(_taiko.gameObject);
                _taiko = null;
            }
        }

        void OnEventFinished(EventObject e)
        {
            if (e == null) return;
            if (ReferenceEquals(e, _bonOdoriEvent)) _yagura?.ReleaseAll();
            if (ReferenceEquals(e, _taikoEvent)) _taiko?.Rest();
        }

        // ────────────────────────────────────────────────────────
        // 補助
        // ────────────────────────────────────────────────────────

        EventObject SpawnEventObject(FestivalEventData data, Vector3 center)
        {
            Transform parent = _root;
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Festival != null && gm.Festival.BuiltRoot != null)
                parent = gm.Festival.BuiltRoot;

            var go = new GameObject(data != null ? $"Event_{data.Id}" : "Event");
            go.transform.SetParent(parent, false);

            var obj = go.AddComponent<EventObject>();
            obj.Configure(data, center);
            obj.Finished += OnEventFinished;
            _active.Add(obj);
            return obj;
        }

        /// <summary>
        /// カタログから引く。見つからなければ既定値のデータを実行時に作る。
        /// データ欠損で祭りが止まらないようにするための保険 (§69)。
        /// </summary>
        FestivalEventData ResolveEventData(string id, FestivalEventKind kind)
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Catalog != null)
            {
                FestivalEventData found = gm.Catalog.GetEvent(id);
                if (found != null) return found;
            }

            MatsuriLog.Warn($"イベントデータが見つかりませんでした: {id}。既定値で開催します。");

            var data = ScriptableObject.CreateInstance<FestivalEventData>();
            data.Id = id;
            data.Kind = kind;
            switch (kind)
            {
                case FestivalEventKind.Fireworks:
                    data.DisplayName = "花火"; data.Cost = 300000; data.Duration = 25f;
                    data.SatisfactionBurst = 35f; data.EffectRadius = 200f;
                    data.StayExtendMultiplier = 1.4f; data.VisitorAttractMultiplier = 1.6f;
                    break;
                case FestivalEventKind.BonOdori:
                    data.DisplayName = "盆踊り"; data.Cost = 120000; data.Duration = 90f;
                    data.SatisfactionBurst = 12f; data.EffectRadius = 30f;
                    data.StayExtendMultiplier = 1.25f; data.VisitorAttractMultiplier = 1.3f;
                    break;
                default:
                    data.DisplayName = "太鼓演奏"; data.Cost = 80000; data.Duration = 60f;
                    data.SatisfactionBurst = 8f; data.EffectRadius = 22f;
                    data.StayExtendMultiplier = 1.15f; data.VisitorAttractMultiplier = 1.2f;
                    break;
            }
            return data;
        }

        IReadOnlyList<VisitorAgent> ActiveVisitors()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && gm.Visitors != null ? gm.Visitors.Active : null;
        }

        void CollectNearby(Vector3 center, float radius, List<VisitorAgent> buffer)
        {
            buffer.Clear();
            IReadOnlyList<VisitorAgent> all = ActiveVisitors();
            if (all == null) return;

            float sqr = radius * radius;
            for (int i = 0; i < all.Count; i++)
            {
                VisitorAgent v = all[i];
                if (v == null || !v.isActiveAndEnabled) continue;

                Vector3 d = v.Position - center;
                d.y = 0f;   // 花火は上空なので高さは無視する
                if (d.sqrMagnitude <= sqr) buffer.Add(v);
            }
        }

        static void AddSatisfaction(VisitorAgent v, float amount)
        {
            if (v == null) return;
            v.Satisfaction = Mathf.Clamp(v.Satisfaction + amount, 0f, 100f);
        }

        Vector3 VenueCenter()
        {
            GroundBounds b = VenueBounds;
            return new Vector3((b.MinX + b.MaxX) * 0.5f, 0f, (b.MinZ + b.MaxZ) * 0.5f);
        }
    }
}
