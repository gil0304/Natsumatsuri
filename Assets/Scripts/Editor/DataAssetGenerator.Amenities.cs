using System.Collections.Generic;
using Matsuri.Data;
using Matsuri.Festival;

namespace Matsuri.EditorTools
{
    /// <summary>
    /// 「居場所」になる施設のデータ。
    ///
    /// 屋台は買ったら終わりだが、祭りに長く居てもらうには
    /// 踊る場所・休む場所・お参りする場所が要る。
    /// ここで作る施設は満足度を直接押し上げ、結果として
    /// 客がもう1軒まわるようになる (§34)。
    ///
    /// 滞在時間や効き目そのものは <see cref="Matsuri.Festival.AmenityProfile"/> が
    /// 効果種別と EffectStrength から決めるので、ここでは
    /// 「何を・いくらで・何人ぶん」だけを決める。
    /// </summary>
    public static partial class DataAssetGenerator
    {
        private static List<FacilityData> BuildAmenities()
        {
            return new List<FacilityData>
            {
                // 盆踊り場：祭りの中心。やぐらを囲んで踊り、楽しさが大きく上がる。
                // 高いが、置くと会場全体の滞在時間が伸びる。
                Facility(AmenityIds.BonOdoriGround, "盆踊り場",
                    new[] { "ぼんおどりば", "盆踊り場", "盆おどり場", "盆踊り広場", "やぐら", "櫓",
                            "踊り場", "bon_odori", "bonodori", "dance_ground" },
                    FacilityVisualKind.Bench, 250000, FacilityEffect.Dance,
                    radius: 18f, strength: 85f, capacity: 24),

                // 休憩所：座って体力を回復する。
                // これが無いと、疲れた客が屋台を回りきらずに帰ってしまう。
                Facility(AmenityIds.RestArea, "休憩所",
                    new[] { "きゅうけいじょ", "休憩場", "休憩所", "休憩スペース", "休み処", "休憩処",
                            "縁台", "ベンチ広場", "rest", "rest_area", "restarea" },
                    FacilityVisualKind.Bench, 60000, FacilityEffect.Rest,
                    radius: 10f, strength: 60f, capacity: 12),

                // 神社：参道の奥の目的地。参拝すると満足度が大きく上がる。
                Facility(AmenityIds.ShrineGround, "神社",
                    new[] { "じんじゃ", "お社", "おやしろ", "お宮", "おみや", "参拝所", "社殿",
                            "本殿", "shrine", "shrine_ground" },
                    FacilityVisualKind.Bench, 200000, FacilityEffect.Worship,
                    radius: 16f, strength: 90f, capacity: 8),

                // 手水舎：神社の手前に置くと参道らしくなる。効果は小さいが安い。
                Facility(AmenityIds.Temizuya, "手水舎",
                    new[] { "てみずや", "ちょうずや", "手水", "水盤", "temizuya", "chozuya" },
                    FacilityVisualKind.Bench, 40000, FacilityEffect.Purify,
                    radius: 6f, strength: 35f, capacity: 4),
            };
        }
    }
}
