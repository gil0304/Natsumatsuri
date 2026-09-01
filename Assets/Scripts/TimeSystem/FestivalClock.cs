using System;

namespace Matsuri.TimeSystem
{
    /// <summary>
    /// ゲーム内時刻 (§7)。17:00 開始、22:00 終了。
    /// 「その日の 0:00 からの分」で保持する。17:00 = 1020分。
    /// </summary>
    [Serializable]
    public struct FestivalClock : IEquatable<FestivalClock>
    {
        public const int StartMinutes = 17 * 60;   // 17:00
        public const int EndMinutes   = 22 * 60;   // 22:00
        public const int SpanMinutes  = EndMinutes - StartMinutes;

        /// <summary>0:00 からの経過分。小数を含む。</summary>
        public float MinutesOfDay;

        public FestivalClock(float minutesOfDay) => MinutesOfDay = minutesOfDay;

        public int Hour   => ((int)MinutesOfDay / 60) % 24;
        public int Minute => (int)MinutesOfDay % 60;

        /// <summary>祭りの進行度 0(17:00) 〜 1(22:00)。</summary>
        public float Normalized
        {
            get
            {
                float t = (MinutesOfDay - StartMinutes) / SpanMinutes;
                return t < 0f ? 0f : (t > 1f ? 1f : t);
            }
        }

        public bool IsBefore(int minutesOfDay) => MinutesOfDay < minutesOfDay;
        public bool IsAfter(int minutesOfDay)  => MinutesOfDay >= minutesOfDay;

        public static FestivalClock AtStart => new FestivalClock(StartMinutes);
        public static FestivalClock AtEnd   => new FestivalClock(EndMinutes);

        /// <summary>"19:24" 形式。</summary>
        public override string ToString() => $"{Hour:00}:{Minute:00}";

        /// <summary>"17:00" のような文字列を分に変換する。失敗したら false。</summary>
        public static bool TryParse(string text, out int minutesOfDay)
        {
            minutesOfDay = 0;
            if (string.IsNullOrEmpty(text)) return false;
            int colon = text.IndexOf(':');
            if (colon < 0) colon = text.IndexOf('：');   // 全角コロンも許容（IME入力対策）
            if (colon <= 0) return false;

            if (!int.TryParse(text.Substring(0, colon).Trim(), out int h)) return false;
            if (!int.TryParse(text.Substring(colon + 1).Trim(), out int m)) return false;
            if (h < 0 || h > 47 || m < 0 || m > 59) return false;

            minutesOfDay = h * 60 + m;
            return true;
        }

        public bool Equals(FestivalClock other) => Math.Abs(MinutesOfDay - other.MinutesOfDay) < 0.0001f;
        public override bool Equals(object obj) => obj is FestivalClock c && Equals(c);
        public override int GetHashCode() => MinutesOfDay.GetHashCode();
    }
}
