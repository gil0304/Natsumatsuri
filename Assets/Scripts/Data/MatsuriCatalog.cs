using System.Collections.Generic;
using UnityEngine;
using Matsuri.Script;

namespace Matsuri.Data
{
    /// <summary>
    /// 「この祭りで使える物」の総目録 (§19〜§22)。
    /// Matsuri Script の Validator / Interpreter / コード補完 (§43) はこの実体を
    /// <see cref="IMatsuriCatalog"/> 越しに参照する。
    /// 表記ゆれ (§13) は <see cref="NameUtility"/> で正規化した索引で吸収する。
    /// </summary>
    [CreateAssetMenu(fileName = "MatsuriCatalog", menuName = "Matsuri/Matsuri Catalog", order = 20)]
    public sealed class MatsuriCatalog : ScriptableObject, IMatsuriCatalog
    {
        [Header("バランス")]
        [Tooltip("初期予算・敷地範囲などの基準値。")]
        public BalanceConfig Balance;

        [Header("収録データ")]
        public StallData[] Stalls = System.Array.Empty<StallData>();
        public FacilityData[] Facilities = System.Array.Empty<FacilityData>();
        public DecorationData[] Decorations = System.Array.Empty<DecorationData>();
        public FestivalEventData[] Events = System.Array.Empty<FestivalEventData>();
        public VisitorArchetype[] Archetypes = System.Array.Empty<VisitorArchetype>();

        [Header("会場の敷地 (§17)")]
        [Tooltip("Balance が設定されているときに使う敷地範囲 (m)。Balance が null なら GroundBounds.Default。")]
        public float GroundMinX = -60f;
        public float GroundMaxX = 60f;
        public float GroundMinZ = -60f;
        public float GroundMaxZ = 60f;

        // ── 索引（正規化名 → データ）。RebuildIndex で構築する ──────────────
        private Dictionary<string, StallData> _stallIndex;
        private Dictionary<string, FacilityData> _facilityIndex;
        private Dictionary<string, DecorationData> _decorationIndex;
        private Dictionary<string, FestivalEventData> _eventIndex;
        private Dictionary<string, VisitorArchetype> _archetypeIndex;

        // ── GetAll のキャッシュ ────────────────────────────────────────────
        private CatalogEntry[] _stallEntries;
        private CatalogEntry[] _facilityEntries;
        private CatalogEntry[] _decorationEntries;
        private CatalogEntry[] _eventEntries;

        private static readonly CatalogEntry[] EmptyEntries = System.Array.Empty<CatalogEntry>();
        private static readonly string[] EmptyNames = System.Array.Empty<string>();

        private void OnEnable() => RebuildIndex();

#if UNITY_EDITOR
        private void OnValidate() => RebuildIndex();
#endif

        // ─────────────────────────────────────────────────────────────────
        // 索引の構築
        // ─────────────────────────────────────────────────────────────────

        /// <summary>索引と GetAll のキャッシュを作り直す。データを差し替えたら呼ぶ。</summary>
        public void RebuildIndex()
        {
            _stallIndex = new Dictionary<string, StallData>(64);
            _facilityIndex = new Dictionary<string, FacilityData>(32);
            _decorationIndex = new Dictionary<string, DecorationData>(32);
            _eventIndex = new Dictionary<string, FestivalEventData>(16);
            _archetypeIndex = new Dictionary<string, VisitorArchetype>(16);

            var stallList = new List<CatalogEntry>();
            if (Stalls != null)
            {
                for (int i = 0; i < Stalls.Length; i++)
                {
                    var d = Stalls[i];
                    if (d == null) continue;
                    Register(_stallIndex, d.Id, d.DisplayName, d.Aliases, d);
                    stallList.Add(new CatalogEntry(d.Id, d.DisplayName, MatsuriEntryKind.Stall,
                        d.BuildCost, d.DefaultPrice, d.MinPrice, d.MaxPrice));
                }
            }
            _stallEntries = stallList.ToArray();

            var facilityList = new List<CatalogEntry>();
            if (Facilities != null)
            {
                for (int i = 0; i < Facilities.Length; i++)
                {
                    var d = Facilities[i];
                    if (d == null) continue;
                    Register(_facilityIndex, d.Id, d.DisplayName, d.Aliases, d);
                    facilityList.Add(new CatalogEntry(d.Id, d.DisplayName, MatsuriEntryKind.Facility, d.BuildCost));
                }
            }
            _facilityEntries = facilityList.ToArray();

            var decorationList = new List<CatalogEntry>();
            if (Decorations != null)
            {
                for (int i = 0; i < Decorations.Length; i++)
                {
                    var d = Decorations[i];
                    if (d == null) continue;
                    Register(_decorationIndex, d.Id, d.DisplayName, d.Aliases, d);
                    decorationList.Add(new CatalogEntry(d.Id, d.DisplayName, MatsuriEntryKind.Decoration, d.BuildCost));
                }
            }
            _decorationEntries = decorationList.ToArray();

            var eventList = new List<CatalogEntry>();
            if (Events != null)
            {
                for (int i = 0; i < Events.Length; i++)
                {
                    var d = Events[i];
                    if (d == null) continue;
                    Register(_eventIndex, d.Id, d.DisplayName, d.Aliases, d);
                    eventList.Add(new CatalogEntry(d.Id, d.DisplayName, MatsuriEntryKind.Event, d.Cost));
                }
            }
            _eventEntries = eventList.ToArray();

            if (Archetypes != null)
            {
                for (int i = 0; i < Archetypes.Length; i++)
                {
                    var a = Archetypes[i];
                    if (a == null || string.IsNullOrEmpty(a.Id)) continue;
                    _archetypeIndex[NameUtility.Normalize(a.Id)] = a;
                }
            }
        }

        /// <summary>ID・表示名・別表記をすべて同じデータに向ける。先勝ち（先に登録した物を優先）。</summary>
        private static void Register<T>(Dictionary<string, T> index, string id, string displayName,
            string[] aliases, T data) where T : Object
        {
            AddKey(index, id, data);
            AddKey(index, displayName, data);
            if (aliases == null) return;
            for (int i = 0; i < aliases.Length; i++) AddKey(index, aliases[i], data);
        }

        private static void AddKey<T>(Dictionary<string, T> index, string key, T data)
        {
            if (string.IsNullOrEmpty(key)) return;
            string normalized = NameUtility.Normalize(key);
            if (normalized.Length == 0) return;
            if (!index.ContainsKey(normalized)) index[normalized] = data;
        }

        private void EnsureIndex()
        {
            if (_stallIndex == null || _facilityIndex == null || _decorationIndex == null
                || _eventIndex == null || _archetypeIndex == null)
            {
                RebuildIndex();
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ID / 名前からの取得
        // ─────────────────────────────────────────────────────────────────

        public StallData GetStall(string id)
        {
            EnsureIndex();
            return Lookup(_stallIndex, id);
        }

        public FacilityData GetFacility(string id)
        {
            EnsureIndex();
            return Lookup(_facilityIndex, id);
        }

        public DecorationData GetDecoration(string id)
        {
            EnsureIndex();
            return Lookup(_decorationIndex, id);
        }

        public FestivalEventData GetEvent(string id)
        {
            EnsureIndex();
            return Lookup(_eventIndex, id);
        }

        public VisitorArchetype GetArchetype(string id)
        {
            EnsureIndex();
            return Lookup(_archetypeIndex, id);
        }

        private static T Lookup<T>(Dictionary<string, T> index, string key) where T : Object
        {
            if (index == null || string.IsNullOrEmpty(key)) return null;
            return index.TryGetValue(NameUtility.Normalize(key), out var value) ? value : null;
        }

        /// <summary>SpawnWeight による重み付き抽選 (§27)。決定論のため Unity.Mathematics.Random を使う。</summary>
        public VisitorArchetype PickArchetype(ref Unity.Mathematics.Random rng)
        {
            if (Archetypes == null || Archetypes.Length == 0) return null;

            float total = 0f;
            for (int i = 0; i < Archetypes.Length; i++)
            {
                var a = Archetypes[i];
                if (a == null) continue;
                total += Mathf.Max(0f, a.SpawnWeight);
            }

            if (total <= 0f)
            {
                // 重みが全部 0 なら均等に選ぶ。
                for (int guard = 0; guard < Archetypes.Length; guard++)
                {
                    int index = rng.NextInt(0, Archetypes.Length);
                    if (Archetypes[index] != null) return Archetypes[index];
                }
                return null;
            }

            float pick = rng.NextFloat() * total;
            for (int i = 0; i < Archetypes.Length; i++)
            {
                var a = Archetypes[i];
                if (a == null) continue;
                float w = Mathf.Max(0f, a.SpawnWeight);
                if (pick < w) return a;
                pick -= w;
            }

            // 浮動小数の誤差で落ちたとき用のフォールバック。
            for (int i = Archetypes.Length - 1; i >= 0; i--)
                if (Archetypes[i] != null) return Archetypes[i];
            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // IMatsuriCatalog
        // ─────────────────────────────────────────────────────────────────

        public long InitialBudget => Balance != null ? Balance.InitialBudget : 1000000L;

        public GroundBounds Bounds
        {
            get
            {
                if (Balance == null) return GroundBounds.Default;
                return new GroundBounds(GroundMinX, GroundMaxX, GroundMinZ, GroundMaxZ);
            }
        }

        public bool TryResolve(string writtenName, MatsuriEntryKind kind, out CatalogEntry entry)
        {
            EnsureIndex();
            entry = CatalogEntry.None;
            if (string.IsNullOrEmpty(writtenName)) return false;

            switch (kind)
            {
                case MatsuriEntryKind.Stall:
                {
                    var d = Lookup(_stallIndex, writtenName);
                    if (d == null) return false;
                    entry = new CatalogEntry(d.Id, d.DisplayName, MatsuriEntryKind.Stall,
                        d.BuildCost, d.DefaultPrice, d.MinPrice, d.MaxPrice);
                    return true;
                }
                case MatsuriEntryKind.Facility:
                {
                    var d = Lookup(_facilityIndex, writtenName);
                    if (d == null) return false;
                    entry = new CatalogEntry(d.Id, d.DisplayName, MatsuriEntryKind.Facility, d.BuildCost);
                    return true;
                }
                case MatsuriEntryKind.Decoration:
                {
                    var d = Lookup(_decorationIndex, writtenName);
                    if (d == null) return false;
                    entry = new CatalogEntry(d.Id, d.DisplayName, MatsuriEntryKind.Decoration, d.BuildCost);
                    return true;
                }
                case MatsuriEntryKind.Event:
                {
                    var d = Lookup(_eventIndex, writtenName);
                    if (d == null) return false;
                    entry = new CatalogEntry(d.Id, d.DisplayName, MatsuriEntryKind.Event, d.Cost);
                    return true;
                }
                default:
                    return false;
            }
        }

        public bool TryResolveAny(string writtenName, out CatalogEntry entry)
        {
            if (TryResolve(writtenName, MatsuriEntryKind.Stall, out entry)) return true;
            if (TryResolve(writtenName, MatsuriEntryKind.Facility, out entry)) return true;
            if (TryResolve(writtenName, MatsuriEntryKind.Decoration, out entry)) return true;
            if (TryResolve(writtenName, MatsuriEntryKind.Event, out entry)) return true;
            entry = CatalogEntry.None;
            return false;
        }

        public IReadOnlyList<CatalogEntry> GetAll(MatsuriEntryKind kind)
        {
            EnsureIndex();
            switch (kind)
            {
                case MatsuriEntryKind.Stall:      return _stallEntries ?? EmptyEntries;
                case MatsuriEntryKind.Facility:   return _facilityEntries ?? EmptyEntries;
                case MatsuriEntryKind.Decoration: return _decorationEntries ?? EmptyEntries;
                case MatsuriEntryKind.Event:      return _eventEntries ?? EmptyEntries;
                default:                          return EmptyEntries;
            }
        }

        /// <summary>タイプミス時の「もしかして」候補 (§41)。近い順に最大 count 件。</summary>
        public IReadOnlyList<string> SuggestNames(string writtenName, MatsuriEntryKind kind, int count = 3)
        {
            if (count <= 0 || string.IsNullOrEmpty(writtenName)) return EmptyNames;
            EnsureIndex();

            var candidates = new List<Suggestion>();
            switch (kind)
            {
                case MatsuriEntryKind.Stall:
                    if (Stalls != null)
                        foreach (var d in Stalls)
                            if (d != null) Consider(candidates, writtenName, d.DisplayName, d.Id, d.Aliases);
                    break;
                case MatsuriEntryKind.Facility:
                    if (Facilities != null)
                        foreach (var d in Facilities)
                            if (d != null) Consider(candidates, writtenName, d.DisplayName, d.Id, d.Aliases);
                    break;
                case MatsuriEntryKind.Decoration:
                    if (Decorations != null)
                        foreach (var d in Decorations)
                            if (d != null) Consider(candidates, writtenName, d.DisplayName, d.Id, d.Aliases);
                    break;
                case MatsuriEntryKind.Event:
                    if (Events != null)
                        foreach (var d in Events)
                            if (d != null) Consider(candidates, writtenName, d.DisplayName, d.Id, d.Aliases);
                    break;
            }

            if (candidates.Count == 0) return EmptyNames;

            candidates.Sort((a, b) => a.Distance != b.Distance
                ? a.Distance.CompareTo(b.Distance)
                : string.CompareOrdinal(a.Name, b.Name));

            int take = Mathf.Min(count, candidates.Count);
            var result = new string[take];
            for (int i = 0; i < take; i++) result[i] = candidates[i].Name;
            return result;
        }

        private readonly struct Suggestion
        {
            public readonly string Name;
            public readonly int Distance;
            public Suggestion(string name, int distance) { Name = name; Distance = distance; }
        }

        /// <summary>
        /// 表示名・ID・別表記のうち一番近い物との距離で候補に入れる。
        /// 遠すぎる（名前の長さの半分を超える）物は「もしかして」に出さない。
        /// </summary>
        private static void Consider(List<Suggestion> into, string written,
            string displayName, string id, string[] aliases)
        {
            if (string.IsNullOrEmpty(displayName)) displayName = id;
            if (string.IsNullOrEmpty(displayName)) return;

            int best = int.MaxValue;
            int bestLength = 0;

            TryCandidate(written, displayName, ref best, ref bestLength);
            TryCandidate(written, id, ref best, ref bestLength);
            if (aliases != null)
                for (int i = 0; i < aliases.Length; i++)
                    TryCandidate(written, aliases[i], ref best, ref bestLength);

            if (best == int.MaxValue) return;

            int allowed = Mathf.Max(1, bestLength / 2);
            if (best > allowed) return;

            into.Add(new Suggestion(displayName, best));
        }

        private static void TryCandidate(string written, string candidate, ref int best, ref int bestLength)
        {
            if (string.IsNullOrEmpty(candidate)) return;
            int d = NameUtility.Distance(written, candidate);
            if (d >= best) return;
            best = d;
            bestLength = NameUtility.Normalize(candidate).Length;
        }
    }
}
