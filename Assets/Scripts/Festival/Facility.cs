using System.Collections.Generic;
using Matsuri.Art;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.TimeSystem;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 仕様書 §20 / §34。会場に建てる設備。
    ///
    /// 「効果だけの設備」と「居場所になる設備」の2種類がある。
    ///
    /// 効果だけの設備:
    /// - ゴミ箱(Cleanliness) / トイレ(Relief): 周囲の満足度低下をやわらげる
    /// - 案内板(Guidance): 周囲の屋台の認知度を上げ、行列の偏りを減らす
    /// - 入り口(Entrance) / 出口(Exit): VisitorManager にスポーン・退場位置を教える
    ///
    /// 居場所になる設備（滞在すると満足度が上がる §34）:
    /// - 休憩所・ベンチ(Rest) / 盆踊り場(Dance) / 神社(Worship) / 手水舎(Purify)
    ///   NPC は <see cref="TryOccupy(Visitors.VisitorAgent, out Vector3)"/> で立ち位置を取り、
    ///   <see cref="StayMinutes"/> ぶん滞在してから <see cref="Release(Visitors.VisitorAgent)"/> で返す。
    /// </summary>
    public sealed class Facility : FestivalObject
    {
        /// <summary>周囲を走査する間隔（実時間・秒）。</summary>
        const float ScanInterval = 0.75f;

        [Tooltip("この設備の設定。")]
        public FacilityData Data;

        [Header("滞在の効き目 (§34)")]
        [Tooltip("滞在時間。**ゲーム内の分**。実秒は BalanceConfig.ToRealSeconds() で換算する。")]
        public float StayMinutes;

        [Tooltip("滞在中に毎秒増える満足度（実時間の1秒あたり）。")]
        public float SatisfactionPerSecond;

        [Tooltip("滞在中に毎秒増える体力。踊りのように疲れるものは負。")]
        public float EnergyPerSecond;

        [Tooltip("滞在中に毎秒増える「遊びたさ」。踊れば満たされるので負になる。")]
        public float FunPerSecond;

        public override FestivalObjectKind Kind => FestivalObjectKind.Facility;

        public FacilityEffect Effect => Data != null ? Data.Effect : FacilityEffect.Rest;
        public float EffectRadius => Data != null ? Data.EffectRadius : 6f;
        public float EffectStrength => Data != null ? Data.EffectStrength : 0f;

        /// <summary>同時に使える人数。0 なら無制限（＝立ち位置を管理しない設備）。</summary>
        public int Capacity => Data != null ? Data.Capacity : 0;

        /// <summary>いま使われている数。</summary>
        public int Occupancy => _occupied;

        /// <summary>いま使われている数（旧名）。</summary>
        public int Occupied => _occupied;

        /// <summary>まだ空きがあるか。</summary>
        public bool HasFreeSlot => Capacity <= 0 || _occupied < Capacity;

        /// <summary>まだ空きがあるか（旧名）。</summary>
        public bool HasRoom => HasFreeSlot;

        /// <summary>いま空いている立ち位置の数。無制限の設備は Capacity と同じ扱いで大きな数を返す。</summary>
        public int FreeSlots => Capacity <= 0 ? 99 : Mathf.Max(0, Capacity - _occupied);

        /// <summary>滞在して満足度が上がる設備か (§34)。</summary>
        public bool IsPlaceToStay => StayMinutes > 0f;

        int _occupied;
        float _scanTimer;
        Transform[] _slots;
        bool[] _slotTaken;
        readonly Dictionary<Visitors.VisitorAgent, int> _slotOf = new Dictionary<Visitors.VisitorAgent, int>(16);

        // ================================================================
        // 生成・登録
        // ================================================================

        void OnEnable()
        {
            if (Data != null) AmenityRegistry.Register(this);
        }

        void OnDisable()
        {
            AmenityRegistry.Unregister(this);
        }

        /// <summary>データを割り当てる。FestivalManager から呼ばれる。</summary>
        public void Configure(FacilityData data)
        {
            // 効果種別が変わると台帳の並びも変わるので、いったん外してから入れ直す。
            AmenityRegistry.Unregister(this);

            Data = data;

            if (data != null)
            {
                ObjectId = data.Id;
                BuildCost = data.BuildCost;
                if (string.IsNullOrEmpty(name) || name.StartsWith("New Game Object")) name = data.DisplayName;
            }

            ApplyStayProfile();
            EnsureAmenityVisual();

            _occupied = 0;
            _slotOf.Clear();
            _scanTimer = Random.Range(0f, ScanInterval);
            CacheSlots();

            if (data != null) AmenityRegistry.Register(this);
        }

        /// <summary>滞在の効き目を効果種別と EffectStrength から決める。</summary>
        void ApplyStayProfile()
        {
            var profile = AmenityProfile.ForEffect(Effect);
            float scale = AmenityProfile.StrengthScale(EffectStrength);

            StayMinutes = profile.StayMinutes;
            SatisfactionPerSecond = profile.SatisfactionPerSecond * scale;
            EnergyPerSecond = profile.EnergyPerSecond * scale;
            FunPerSecond = profile.FunPerSecond * scale;
        }

        /// <summary>
        /// 盆踊り場・休憩所・神社・手水舎は専用の見た目を持つ (§79)。
        /// 汎用の <see cref="ProceduralFacilityFactory"/> が仮の見た目を作っていた場合は、
        /// ここで作り直す。Prefab が指定されているときは尊重してそのまま使う (§69)。
        /// </summary>
        void EnsureAmenityVisual()
        {
            if (Data == null || Data.Prefab != null) return;
            if (!ProceduralAmenityFactory.Handles(Data)) return;
            if (HasSlotChild()) return;    // すでに専用の見た目が組まれている

            // 仮の見た目を切り離してから消す。消えるのが次フレームでも LOD の計算に混ざらない。
            var stale = new List<Transform>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++) stale.Add(transform.GetChild(i));
            for (int i = 0; i < stale.Count; i++)
            {
                if (stale[i] == null) continue;
                stale[i].SetParent(null, false);
                if (Application.isPlaying) Destroy(stale[i].gameObject);
                else DestroyImmediate(stale[i].gameObject);
            }

            ProceduralAmenityFactory.BuildInto(Data, transform);
        }

        bool HasSlotChild()
        {
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != transform && all[i].name.StartsWith("Slot")) return true;
            return false;
        }

        /// <summary>
        /// 立ち位置を子オブジェクト "Slot01".. から探す。
        /// 旧来のベンチ用 "Seat01".. も拾う。
        /// 足りなければ <see cref="AmenitySlotLayout"/> の並びで自前に作る（Prefab 差し替えに備える §69）。
        /// </summary>
        void CacheSlots()
        {
            int capacity = Capacity;
            if (capacity <= 0)
            {
                _slots = System.Array.Empty<Transform>();
                _slotTaken = System.Array.Empty<bool>();
                return;
            }

            var found = new List<Transform>(capacity);
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                if (child.name.StartsWith("Slot") || child.name.StartsWith("Seat")) found.Add(child);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            // 足りないぶんは施設の形に合わせて自前で足す。
            for (int i = found.Count; i < capacity; i++)
            {
                var slot = new GameObject($"Slot{i + 1:00}").transform;
                slot.SetParent(transform, false);
                slot.localPosition = AmenitySlotLayout.Local(Effect, i, capacity);
                found.Add(slot);
            }

            _slots = found.ToArray();
            _slotTaken = new bool[Mathf.Max(capacity, _slots.Length)];
        }

        public override void OnBuilt()
        {
            RegisterGate();
        }

        /// <summary>入り口・出口なら VisitorManager に位置を教える (§28)。</summary>
        void RegisterGate()
        {
            var visitors = GameManager.Instance != null ? GameManager.Instance.Visitors : null;
            if (visitors == null || Data == null) return;

            // 門の少し外側を出入り位置にする。門の中に湧かないように。
            Vector3 outside = transform.position - transform.forward * 1.5f;

            if (Data.Effect == FacilityEffect.Entrance)
            {
                visitors.EntrancePosition = outside;
                MatsuriLog.Info($"入り口を {outside} に設定しました。");
            }
            else if (Data.Effect == FacilityEffect.Exit)
            {
                visitors.ExitPosition = outside;
                MatsuriLog.Info($"出口を {outside} に設定しました。");
            }
        }

        // ================================================================
        // 立ち位置の貸し借り
        // ================================================================

        /// <summary>
        /// 立ち位置を1つ取り、そのワールド座標を返す。空きが無ければ false。
        /// 誰が使っているかを覚えるので、<see cref="Release(Visitors.VisitorAgent)"/> で確実に返せる。
        /// </summary>
        public bool TryOccupy(Visitors.VisitorAgent v, out Vector3 slot)
        {
            slot = transform.position;
            if (v == null) return TryOccupy();

            // 二重取得を防ぐ。すでに持っているならその位置を返す。
            if (_slotOf.TryGetValue(v, out int held))
            {
                slot = GetSlotPosition(held);
                return true;
            }

            if (Capacity <= 0)
            {
                _occupied++;
                return true;
            }

            int index = FirstFreeSlotIndex;
            if (index < 0) return false;

            if (_slotTaken != null && index < _slotTaken.Length) _slotTaken[index] = true;
            _slotOf[v] = index;
            _occupied++;
            slot = GetSlotPosition(index);
            return true;
        }

        /// <summary>誰が使うかを問わず1つ取る（旧API）。</summary>
        public bool TryOccupy()
        {
            if (Capacity > 0 && _occupied >= Capacity) return false;

            if (Capacity > 0)
            {
                int index = FirstFreeSlotIndex;
                if (index < 0) return false;
                if (_slotTaken != null && index < _slotTaken.Length) _slotTaken[index] = true;
            }
            _occupied++;
            return true;
        }

        /// <summary>その人が使っていた立ち位置を返す。持っていなければ何もしない。</summary>
        public void Release(Visitors.VisitorAgent v)
        {
            if (v == null) { Release(); return; }
            if (!_slotOf.TryGetValue(v, out int index)) return;

            _slotOf.Remove(v);
            if (_slotTaken != null && index >= 0 && index < _slotTaken.Length) _slotTaken[index] = false;
            _occupied = Mathf.Max(0, _occupied - 1);
        }

        /// <summary>誰のぶんか分からないまま1つ返す（旧API）。</summary>
        public void Release()
        {
            if (_occupied <= 0) { _occupied = 0; return; }
            _occupied--;

            // 記録の無い占有（旧API経由）を優先して開ける。
            if (_slotTaken == null) return;
            for (int i = _slotTaken.Length - 1; i >= 0; i--)
            {
                if (!_slotTaken[i]) continue;
                if (_slotOf.ContainsValue(i)) continue;
                _slotTaken[i] = false;
                return;
            }
        }

        /// <summary>index 番目の立ち位置のワールド座標。無ければ施設の形に合わせて計算する。</summary>
        public Vector3 GetSlotPosition(int index)
        {
            if (_slots != null && _slots.Length > 0)
            {
                var slot = _slots[Mathf.Clamp(index, 0, _slots.Length - 1)];
                if (slot != null) return slot.position;
            }
            Vector3 local = AmenitySlotLayout.Local(Effect, index, Mathf.Max(1, Capacity));
            return transform.TransformPoint(local);
        }

        /// <summary>index 番目の座席のワールド座標（旧名）。</summary>
        public Vector3 GetSeatPosition(int index) => GetSlotPosition(index);

        /// <summary>いま空いている立ち位置の番号。無ければ -1。</summary>
        public int FirstFreeSlotIndex
        {
            get
            {
                if (Capacity <= 0) return 0;
                if (_slotTaken == null) return _occupied < Capacity ? _occupied : -1;
                for (int i = 0; i < _slotTaken.Length && i < Capacity; i++)
                    if (!_slotTaken[i]) return i;
                return -1;
            }
        }

        /// <summary>いま空いている座席の番号（旧名）。</summary>
        public int FirstFreeSeatIndex => FirstFreeSlotIndex;

        public override void OnFestivalEnd()
        {
            _occupied = 0;
            _slotOf.Clear();
            if (_slotTaken != null) System.Array.Clear(_slotTaken, 0, _slotTaken.Length);
        }

        protected override void OnDestroy()
        {
            AmenityRegistry.Unregister(this);
            base.OnDestroy();
        }

        // ================================================================
        // 周囲への効果
        // ================================================================

        public override void TickFestival(float dt, FestivalClock clock)
        {
            if (Data == null) return;
            if (EffectStrength <= 0f) return;

            // 清潔さ・安心・案内は「じわじわ効く」もの。頻繁に計算する必要はない (§57)。
            if (Effect != FacilityEffect.Cleanliness &&
                Effect != FacilityEffect.Relief &&
                Effect != FacilityEffect.Guidance) return;

            _scanTimer -= dt;
            if (_scanTimer > 0f) return;
            _scanTimer += ScanInterval;

            ApplyComfort(ScanInterval);
        }

        /// <summary>半径内のNPCの満足度をわずかに上げる (§34)。</summary>
        void ApplyComfort(float elapsedSeconds)
        {
            var manager = GameManager.Instance != null ? GameManager.Instance.Visitors : null;
            var visitors = manager != null ? manager.Active : null;
            if (visitors == null || visitors.Count == 0) return;

            float radius = EffectRadius;
            float sqrRadius = radius * radius;

            // EffectStrength は「効果の強さ」であり毎秒の量ではないので、控えめな係数を掛ける。
            float gain = EffectStrength * 0.05f * elapsedSeconds;
            Vector3 origin = transform.position;

            for (int i = 0; i < visitors.Count; i++)
            {
                var v = visitors[i];
                if (v == null) continue;

                Vector3 d = v.Position - origin;
                float sqr = d.x * d.x + d.z * d.z;
                if (sqr > sqrRadius) continue;

                float falloff = 1f - Mathf.Sqrt(sqr) / radius;
                v.Satisfaction = Mathf.Min(100f, v.Satisfaction + gain * falloff);
            }
        }
    }
}
