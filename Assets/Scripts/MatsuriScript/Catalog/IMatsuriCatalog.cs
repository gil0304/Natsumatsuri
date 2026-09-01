using System.Collections.Generic;

namespace Matsuri.Script
{
    public enum MatsuriEntryKind
    {
        Stall,
        Decoration,
        Facility,
        Event
    }

    /// <summary>
    /// カタログ1件ぶんの、言語処理系が必要とする最小情報。
    /// 実体は ScriptableObject (StallData など) 側にあり、これはその読み取りビュー。
    /// </summary>
    public readonly struct CatalogEntry
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly MatsuriEntryKind Kind;
        public readonly long BuildCost;
        public readonly int DefaultPrice;
        public readonly int MinPrice;
        public readonly int MaxPrice;

        public CatalogEntry(string id, string displayName, MatsuriEntryKind kind,
            long buildCost, int defaultPrice = 0, int minPrice = 0, int maxPrice = 0)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            BuildCost = buildCost;
            DefaultPrice = defaultPrice;
            MinPrice = minPrice;
            MaxPrice = maxPrice;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id);
        public static readonly CatalogEntry None = default;
    }

    /// <summary>
    /// Validator / Interpreter / コード補完 (§43) が参照する、
    /// 「この祭りで使える物」の一覧。ゲーム側は ScriptableObject 群から実装する。
    /// テストでは軽量なフェイク実装を差し込む。
    /// </summary>
    public interface IMatsuriCatalog
    {
        /// <summary>表記ゆれ（「たこ焼き」「たこやき」「takoyaki」）を吸収して引く。</summary>
        bool TryResolve(string writtenName, MatsuriEntryKind kind, out CatalogEntry entry);

        /// <summary>種別を問わず引く。「花火」のようにイベントにも屋台にも見える語で使う。</summary>
        bool TryResolveAny(string writtenName, out CatalogEntry entry);

        IReadOnlyList<CatalogEntry> GetAll(MatsuriEntryKind kind);

        /// <summary>タイプミス時の「もしかして」候補 (§41)。近い順に最大 count 件。</summary>
        IReadOnlyList<string> SuggestNames(string writtenName, MatsuriEntryKind kind, int count = 3);

        /// <summary>初期予算 (§31)。</summary>
        long InitialBudget { get; }

        /// <summary>会場の敷地範囲。範囲外の「場所」は Validator が弾く。</summary>
        GroundBounds Bounds { get; }
    }

    /// <summary>会場の敷地範囲（メートル）。</summary>
    public readonly struct GroundBounds
    {
        public readonly float MinX, MaxX, MinZ, MaxZ;

        public GroundBounds(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ;
        }

        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        public static readonly GroundBounds Default = new GroundBounds(-60f, 60f, -60f, 60f);

        public override string ToString() => $"X:{MinX}〜{MaxX} / Z:{MinZ}〜{MaxZ}";
    }
}
