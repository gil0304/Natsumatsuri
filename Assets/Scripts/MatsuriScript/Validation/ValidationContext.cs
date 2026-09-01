using System.Collections.Generic;
using Matsuri.Script.Ast;

namespace Matsuri.Script.Validation
{
    /// <summary>
    /// 検証中に持ち回る状態。予算の合計や、すでに埋まっている座標を覚えておく。
    /// </summary>
    internal sealed class ValidationContext
    {
        public readonly IMatsuriCatalog Catalog;
        public readonly List<Diagnostic> Diagnostics;

        /// <summary>祭りが始まる前に必ず建つ物の合計費用（「もし」の中は含めない）。</summary>
        public long UnconditionalCost;

        /// <summary>すでに使われた座標 → そこに置かれた物の名前。重ね置きの警告に使う。</summary>
        public readonly Dictionary<long, string> Occupied = new Dictionary<long, string>();

        public ValidationContext(IMatsuriCatalog catalog, List<Diagnostic> diagnostics)
        {
            Catalog = catalog;
            Diagnostics = diagnostics ?? new List<Diagnostic>();
        }

        public void Error(Node at, string message, string example = null, IReadOnlyList<string> suggestions = null)
            => Diagnostics.Add(Diagnostic.Error(at.Line, at.Column, at.Length <= 0 ? 1 : at.Length, message, example, suggestions));

        public void Warn(Node at, string message, string example = null, IReadOnlyList<string> suggestions = null)
            => Diagnostics.Add(Diagnostic.Warning(at.Line, at.Column, at.Length <= 0 ? 1 : at.Length, message, example, suggestions));

        /// <summary>座標を 0.5m 単位のキーに丸める。</summary>
        public static long PositionKey(double x, double z)
        {
            long gx = (long)System.Math.Round(x * 2.0);
            long gz = (long)System.Math.Round(z * 2.0);
            return (gx << 20) ^ (gz & 0xFFFFF);
        }

        /// <summary>座標の重なりを調べ、すでに何か置かれていればその名前を返す。</summary>
        public bool TryOccupy(double x, double z, string name, out string existing)
        {
            long key = PositionKey(x, z);
            if (Occupied.TryGetValue(key, out existing)) return false;
            Occupied[key] = name;
            existing = null;
            return true;
        }
    }
}
