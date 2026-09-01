using System.Collections.Generic;
using Matsuri.Audio;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Festival;
using Matsuri.TimeSystem;
using Matsuri.Visitors;
using UnityEngine;

namespace Matsuri.Stalls
{
    /// <summary>
    /// 仕様書 §30 / §32 / §33。屋台1軒の実体。
    /// 行列は <see cref="StallQueue"/> に委譲し、この class は
    /// 「接客・売上・人気度・営業状態・演出」に責務を絞る (§66)。
    /// 自前の Update は持たない。更新は StallManager.TickAll から来る (§57)。
    /// </summary>
    public sealed class Stall : FestivalObject
    {
        /// <summary>接客中の1枠。Capacity のぶんだけ同時に走る。</summary>
        struct ServiceSlot
        {
            public VisitorAgent Visitor;
            public float Remaining;
        }

        [Header("データ (§48)")]
        public StallData Data;

        [Tooltip("現在の販売価格 (§32)。SetPrice から変更する。")]
        public int Price;

        public override FestivalObjectKind Kind => FestivalObjectKind.Stall;

        // ── 経営数値 ────────────────────────────────────────────
        public long Revenue { get; private set; }
        public int SalesCount { get; private set; }

        /// <summary>人気度 (§33)。行列のにぎわいと売れ行きで上下する。</summary>
        public float Popularity => _popularity;

        public int QueueLength => _queue.Count;

        /// <summary>営業中か。祭り開催中のみ true。</summary>
        public bool IsOpen { get; private set; }

        // ── 位置マーカー (§23 の階層) ───────────────────────────
        public Transform[] QueuePoints { get; private set; } = System.Array.Empty<Transform>();
        public Transform CustomerPosition { get; private set; }
        public Transform StaffPosition { get; private set; }

        readonly StallQueue _queue = new StallQueue();
        readonly List<ServiceSlot> _serving = new List<ServiceSlot>(4);

        float _popularity;
        GameObject _steamVfx;
        AudioSource _ambienceSource;
        bool _ambiencePlaying;
        StallManager _manager;
        bool _configured;

        /// <summary>行列に並んでいる人の一覧（読み取り専用）。NPC の混雑判定などに使う。</summary>
        public IReadOnlyList<VisitorAgent> QueueMembers => _queue.Members;

        /// <summary>いま接客中の人数。</summary>
        public int ServingCount => _serving.Count;

        // ────────────────────────────────────────────────────────
        // 初期化
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// データと価格を確定し、子オブジェクトから位置マーカー類を拾う。
        /// Prefab 差し替え (§69) でマーカーが無い可能性があるので、無ければ自前で生成する。
        /// </summary>
        public void Configure(StallData data, int price)
        {
            Data = data;
            if (Data == null)
            {
                MatsuriLog.Error("屋台の設定に失敗しました: StallData が null です。");
                return;
            }

            ObjectId = Data.Id;
            if (string.IsNullOrEmpty(InstanceId))
                InstanceId = $"{Data.Id}_{GetInstanceID():X}";

            Price = Data.ClampPrice(price <= 0 ? Data.DefaultPrice : price);
            _popularity = Data.BasePopularity;

            CollectMarkers();

            float spacing = Data.VisualRecipe != null ? Data.VisualRecipe.QueueSpacing : 0.75f;
            _queue.Configure(transform, QueuePoints, Data.MaxQueueLength, spacing);

            _steamVfx = FindChildDeep("SteamVFX")?.gameObject;
            if (_steamVfx != null) _steamVfx.SetActive(false);

            SetupAmbienceSource();

            _configured = true;
            _manager?.MarkIndexDirty();
        }

        void OnEnable()
        {
            _manager = ResolveManager();
            _manager?.Register(this);
        }

        void OnDisable()
        {
            _manager?.Unregister(this);
        }

        protected override void OnDestroy()
        {
            _manager?.Unregister(this);
            base.OnDestroy();
        }

        StallManager ResolveManager()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Stalls != null) return gm.Stalls;
            return FindFirstObjectByType<StallManager>();
        }

        /// <summary>QueuePoint01..NN / CustomerPosition / StaffPosition を集める。無ければ作る。</summary>
        void CollectMarkers()
        {
            var points = new List<Transform>();
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n.StartsWith("QueuePoint", System.StringComparison.OrdinalIgnoreCase)) points.Add(all[i]);
                else if (n == "CustomerPosition") CustomerPosition = all[i];
                else if (n == "StaffPosition") StaffPosition = all[i];
            }

            points.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            float depth = Data != null && Data.VisualRecipe != null ? Data.VisualRecipe.Depth : 2.2f;
            float spacing = Data != null && Data.VisualRecipe != null ? Data.VisualRecipe.QueueSpacing : 0.75f;
            int wanted = Data != null && Data.VisualRecipe != null ? Data.VisualRecipe.QueuePointCount : 8;
            wanted = Mathf.Clamp(wanted, 1, 32);

            float frontZ = depth * 0.5f + 0.9f;

            if (CustomerPosition == null)
                CustomerPosition = CreateMarker("CustomerPosition", new Vector3(0f, 0f, frontZ));

            if (StaffPosition == null)
                StaffPosition = CreateMarker("StaffPosition", new Vector3(0f, 0f, -depth * 0.25f));

            // 足りないぶんの QueuePoint を屋台の正面方向へ伸ばして作る
            for (int i = points.Count; i < wanted; i++)
            {
                Vector3 local = new Vector3(0f, 0f, frontZ + spacing * (i + 1));
                points.Add(CreateMarker($"QueuePoint{i + 1:00}", local));
            }

            QueuePoints = points.ToArray();
        }

        Transform CreateMarker(string markerName, Vector3 localPosition)
        {
            var go = new GameObject(markerName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        Transform FindChildDeep(string childName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == childName) return all[i];
            return null;
        }

        /// <summary>調理音・遊びの音 (§24)。接客中だけ鳴らす。</summary>
        void SetupAmbienceSource()
        {
            _ambienceSource = GetComponentInChildren<AudioSource>(true);
            if (_ambienceSource == null)
            {
                var go = new GameObject("AudioSource");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                _ambienceSource = go.AddComponent<AudioSource>();
            }

            _ambienceSource.playOnAwake = false;
            _ambienceSource.loop = true;
            _ambienceSource.spatialBlend = 1f;
            _ambienceSource.rolloffMode = AudioRolloffMode.Linear;
            _ambienceSource.minDistance = 2.5f;
            _ambienceSource.maxDistance = 18f;
            _ambienceSource.volume = 0.45f;

            AudioClip clip = ResolveAmbienceClip();
            if (clip != null) _ambienceSource.clip = clip;
        }

        AudioClip ResolveAmbienceClip()
        {
            if (Data == null) return null;
            switch (Data.Ambience)
            {
                case StallAmbienceKind.Sizzle:  return ProceduralAudioLibrary.Get(MatsuriSfx.Sizzle);
                case StallAmbienceKind.Shaving: return ProceduralAudioLibrary.Get(MatsuriSfx.Shaving);
                case StallAmbienceKind.Water:   return ProceduralAudioLibrary.Get(MatsuriSfx.Water);
                case StallAmbienceKind.Pop:     return ProceduralAudioLibrary.Get(MatsuriSfx.Pop);
                case StallAmbienceKind.Whirr:   return ProceduralAudioLibrary.Get(MatsuriSfx.Whirr);
                default: return null;
            }
        }

        // ────────────────────────────────────────────────────────
        // 行列 (§30) — 実処理は StallQueue
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// 接客にかかる実時間（秒）。
        /// StallData.ServiceTime は §30 のとおり「ゲーム内の分」で書かれているので、
        /// 祭りの時間圧縮 (§7) で割って実時間に直す。
        /// こうしておくと祭りを2分にしても5分にしても、
        /// 「たこ焼きは8分並ぶ」という体感が変わらない。
        /// </summary>
        public float ServiceSeconds
        {
            get
            {
                if (Data == null) return 1f;
                var balance = Core.GameManager.Instance != null ? Core.GameManager.Instance.Balance : null;
                return balance != null
                    ? balance.ToRealSeconds(Data.ServiceTime)
                    : Mathf.Max(0.25f, Data.ServiceTime);
            }
        }

        public bool CanAcceptQueue => IsOpen && _queue.CanAccept;

        public bool TryJoinQueue(VisitorAgent v)
        {
            if (!IsOpen) return false;
            return _queue.TryJoin(v);
        }

        public void LeaveQueue(VisitorAgent v)
        {
            _queue.Leave(v);

            // 接客中に抜けた場合も枠を空ける
            for (int i = _serving.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_serving[i].Visitor, v)) _serving.RemoveAt(i);
        }

        /// <summary>-1 なら並んでいない。</summary>
        public int GetQueueIndex(VisitorAgent v) => _queue.IndexOf(v);

        public Vector3 GetQueueSlotPosition(int index) => _queue.GetSlotPosition(index);

        public bool IsBeingServed(VisitorAgent v)
        {
            for (int i = 0; i < _serving.Count; i++)
                if (ReferenceEquals(_serving[i].Visitor, v)) return true;
            return false;
        }

        /// <summary>その NPC が並び始めてからの秒数。Patience 超過の判定に使う (§34)。</summary>
        public float GetWaitTime(VisitorAgent v) => _queue.GetWaitTime(v);

        /// <summary>価格変更 (§32)。StallData の上下限に必ず丸める。</summary>
        public void SetPrice(int price)
        {
            if (Data == null) { Price = Mathf.Max(0, price); return; }
            Price = Data.ClampPrice(price);
        }

        // ────────────────────────────────────────────────────────
        // 営業
        // ────────────────────────────────────────────────────────

        public override void OnBuilt()
        {
            if (!_configured && Data != null) Configure(Data, Price);
        }

        public override void OnFestivalStart()
        {
            IsOpen = true;
        }

        public override void OnFestivalEnd()
        {
            IsOpen = false;
            _queue.Clear();
            _serving.Clear();
            SetCookingVisuals(false);
        }

        public override void TickFestival(float dt, FestivalClock clock)
        {
            if (Data == null || dt <= 0f) return;

            _queue.Tick(dt);
            AdvanceService(dt);
            if (IsOpen) FillServiceSlots();
            UpdatePopularity(dt);
            SetCookingVisuals(_serving.Count > 0);
        }

        /// <summary>接客中の客の残り時間を進め、終わったら売上を立てる。</summary>
        void AdvanceService(float dt)
        {
            for (int i = _serving.Count - 1; i >= 0; i--)
            {
                ServiceSlot slot = _serving[i];

                if (slot.Visitor == null || !slot.Visitor.isActiveAndEnabled)
                {
                    _serving.RemoveAt(i);
                    continue;
                }

                slot.Remaining -= dt;
                if (slot.Remaining > 0f)
                {
                    _serving[i] = slot;
                    continue;
                }

                _serving.RemoveAt(i);
                CompleteSale(slot.Visitor);
            }
        }

        /// <summary>1件の販売成立 (§32 売上 / §33 人気度 / §34 満足度)。</summary>
        void CompleteSale(VisitorAgent visitor)
        {
            visitor.OnServed(this, Price);

            Revenue += Price;
            SalesCount++;

            GameManager gm = GameManager.Instance;
            gm?.Economy?.AddRevenue(Price, Data.Id);

            BalanceConfig balance = gm != null ? gm.Balance : null;
            float perSale = balance != null ? balance.PopularityPerSale : 0.8f;
            float maxPop = balance != null ? balance.MaxPopularity : 100f;
            _popularity = Mathf.Min(maxPop, _popularity + perSale);

            gm?.Audio?.PlaySfx(MatsuriSfx.Purchase, transform.position, 0.7f);
        }

        /// <summary>空いた枠へ行列の先頭から入れる。</summary>
        void FillServiceSlots()
        {
            int capacity = Mathf.Max(1, Data.Capacity);
            while (_serving.Count < capacity && _queue.Count > 0)
            {
                VisitorAgent head = _queue.Dequeue();
                if (head == null || !head.isActiveAndEnabled) continue;

                _serving.Add(new ServiceSlot
                {
                    Visitor = head,
                    Remaining = ServiceSeconds
                });
            }
        }

        /// <summary>
        /// §33。行列が長いほど「にぎわっている屋台」として人気度が上がり、
        /// 客が引くと PopularityDecay で基準へ戻る。
        /// </summary>
        void UpdatePopularity(float dt)
        {
            GameManager gm = GameManager.Instance;
            BalanceConfig b = gm != null ? gm.Balance : null;

            float perQueuer = b != null ? b.PopularityPerQueuer : 1.5f;
            float decay     = b != null ? b.PopularityDecay : 2f;
            float maxPop    = b != null ? b.MaxPopularity : 100f;

            float crowdTarget = Data.BasePopularity + (_queue.Count + _serving.Count) * perQueuer;
            crowdTarget = Mathf.Clamp(crowdTarget, 0f, maxPop);

            // にぎわいで上がるのは早く、戻るのはゆっくり。
            float rate = _popularity < crowdTarget ? decay * 2.5f : decay;
            _popularity = Mathf.MoveTowards(_popularity, crowdTarget, rate * dt);
            _popularity = Mathf.Clamp(_popularity, 0f, maxPop);
        }

        /// <summary>調理VFX (§23 SteamVFX) と調理音を接客中だけ出す。</summary>
        void SetCookingVisuals(bool cooking)
        {
            if (_steamVfx != null)
            {
                bool want = cooking && Data != null && Data.HasSteam;
                if (_steamVfx.activeSelf != want) _steamVfx.SetActive(want);
            }

            if (_ambienceSource == null || _ambienceSource.clip == null) return;

            if (cooking && !_ambiencePlaying)
            {
                _ambienceSource.Play();
                _ambiencePlaying = true;
            }
            else if (!cooking && _ambiencePlaying)
            {
                _ambienceSource.Stop();
                _ambiencePlaying = false;
            }
        }
    }
}
