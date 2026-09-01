using System.Collections.Generic;
using Matsuri.Script;

namespace Matsuri.Tests
{
    /// <summary>
    /// テスト用のカタログ。ScriptableObject を作らずに言語処理系だけを試せるようにする。
    /// 中身は仕様書 §19〜§22 と同じ構成（屋台11 / 設備6 / 装飾7 / イベント3）。
    /// </summary>
    public sealed class FakeCatalog : IMatsuriCatalog
    {
        sealed class Row
        {
            public CatalogEntry Entry;
            public string[] Aliases;
        }

        readonly List<Row> _rows = new List<Row>();

        public long InitialBudget { get; set; } = 1_000_000;
        public GroundBounds Bounds { get; set; } = GroundBounds.Default;

        public FakeCatalog()
        {
            // ── 屋台（食べ物） ──────────────────────────────
            Stall(MatsuriIds.Takoyaki,  "たこ焼き", 45000, 500, 100, 1200, "たこやき", "タコ焼き", "takoyaki");
            Stall(MatsuriIds.Yakisoba,  "焼きそば", 48000, 600, 150, 1500, "やきそば", "yakisoba");
            Stall(MatsuriIds.Kakigori,  "かき氷",   38000, 300, 100,  800, "かきごおり", "kakigori");
            Stall(MatsuriIds.RingoAme,  "りんご飴", 32000, 400, 100,  900, "りんごあめ", "ringoame");
            Stall(MatsuriIds.Wataame,   "わたあめ", 30000, 300, 100,  800, "綿あめ", "wataame");
            Stall(MatsuriIds.Frankfurt, "フランクフルト", 40000, 400, 150, 1000, "frankfurt");

            // ── 屋台（遊び） ────────────────────────────────
            Stall(MatsuriIds.Kingyosukui, "金魚すくい", 42000, 400, 100, 1000, "きんぎょすくい", "kingyosukui");
            Stall(MatsuriIds.Shateki,     "射的",       50000, 300, 100, 1000, "しゃてき", "shateki");
            Stall(MatsuriIds.YoyoTsuri,   "ヨーヨー釣り", 35000, 300, 100, 800, "よーよーつり", "yoyo");
            Stall(MatsuriIds.SuperBall,   "スーパーボールすくい", 33000, 300, 100, 800, "すーぱーぼーる", "superball");
            Stall(MatsuriIds.Katanuki,    "型抜き",     25000, 200,  50,  600, "かたぬき", "katanuki");

            // ── 設備 ────────────────────────────────────────
            Facility(MatsuriIds.Bench,     "ベンチ",  12000, "bench", "いす");
            Facility(MatsuriIds.TrashCan,  "ゴミ箱",   6000, "trashcan", "ごみばこ");
            Facility(MatsuriIds.Toilet,    "トイレ",  40000, "toilet", "お手洗い");
            Facility(MatsuriIds.Entrance,  "入り口",  20000, "入口", "entrance");
            Facility(MatsuriIds.Exit,      "出口",    20000, "exit");
            Facility(MatsuriIds.SignBoard, "案内板",  10000, "signboard", "看板");

            // ── 装飾 ────────────────────────────────────────
            Decoration(MatsuriIds.Lantern,      "提灯",         4000, "ちょうちん", "lantern");
            Decoration(MatsuriIds.Nobori,       "のぼり",       5000, "nobori", "旗");
            Decoration(MatsuriIds.Shrine,       "神社",        90000, "shrine", "お社");
            Decoration(MatsuriIds.Torii,        "鳥居",        60000, "torii");
            Decoration(MatsuriIds.Tree,         "木",          10000, "tree", "樹");
            Decoration(MatsuriIds.StallLight,   "屋台用ライト", 8000, "light");
            Decoration(MatsuriIds.FestivalSign, "夏祭り看板",  25000, "festivalsign");

            // ── イベント ────────────────────────────────────
            Event(MatsuriIds.Fireworks, "花火",     150000, "はなび", "fireworks");
            Event(MatsuriIds.BonOdori,  "盆踊り",    80000, "ぼんおどり", "bonodori");
            Event(MatsuriIds.Taiko,     "太鼓演奏",  60000, "太鼓", "たいこ", "taiko");
        }

        void Stall(string id, string name, long cost, int defaultPrice, int min, int max, params string[] aliases)
            => _rows.Add(new Row
            {
                Entry = new CatalogEntry(id, name, MatsuriEntryKind.Stall, cost, defaultPrice, min, max),
                Aliases = aliases
            });

        void Facility(string id, string name, long cost, params string[] aliases)
            => _rows.Add(new Row
            {
                Entry = new CatalogEntry(id, name, MatsuriEntryKind.Facility, cost),
                Aliases = aliases
            });

        void Decoration(string id, string name, long cost, params string[] aliases)
            => _rows.Add(new Row
            {
                Entry = new CatalogEntry(id, name, MatsuriEntryKind.Decoration, cost),
                Aliases = aliases
            });

        void Event(string id, string name, long cost, params string[] aliases)
            => _rows.Add(new Row
            {
                Entry = new CatalogEntry(id, name, MatsuriEntryKind.Event, cost),
                Aliases = aliases
            });

        static bool Matches(Row row, string written)
        {
            if (ScriptText.NameEquals(row.Entry.DisplayName, written)) return true;
            if (ScriptText.NameEquals(row.Entry.Id, written)) return true;
            if (row.Aliases == null) return false;
            for (int i = 0; i < row.Aliases.Length; i++)
                if (ScriptText.NameEquals(row.Aliases[i], written)) return true;
            return false;
        }

        public bool TryResolve(string writtenName, MatsuriEntryKind kind, out CatalogEntry entry)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Entry.Kind != kind) continue;
                if (!Matches(_rows[i], writtenName)) continue;
                entry = _rows[i].Entry;
                return true;
            }
            entry = CatalogEntry.None;
            return false;
        }

        public bool TryResolveAny(string writtenName, out CatalogEntry entry)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (!Matches(_rows[i], writtenName)) continue;
                entry = _rows[i].Entry;
                return true;
            }
            entry = CatalogEntry.None;
            return false;
        }

        public IReadOnlyList<CatalogEntry> GetAll(MatsuriEntryKind kind)
        {
            var list = new List<CatalogEntry>();
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Entry.Kind == kind) list.Add(_rows[i].Entry);
            return list;
        }

        public IReadOnlyList<string> SuggestNames(string writtenName, MatsuriEntryKind kind, int count = 3)
        {
            var scored = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Entry.Kind != kind) continue;

                int best = ScriptText.Distance(writtenName, _rows[i].Entry.DisplayName);
                if (_rows[i].Aliases != null)
                {
                    for (int a = 0; a < _rows[i].Aliases.Length; a++)
                    {
                        int d = ScriptText.Distance(writtenName, _rows[i].Aliases[a]);
                        if (d < best) best = d;
                    }
                }
                scored.Add(new KeyValuePair<int, string>(best, _rows[i].Entry.DisplayName));
            }

            scored.Sort((x, y) => x.Key.CompareTo(y.Key));

            var result = new List<string>();
            for (int i = 0; i < scored.Count && result.Count < count; i++)
            {
                if (scored[i].Key > 3) break;
                result.Add(scored[i].Value);
            }
            return result;
        }
    }
}
