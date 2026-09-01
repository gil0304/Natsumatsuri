using System;
using System.Collections.Generic;
using System.Globalization;
using Matsuri.Script;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §46 / §77 BATTLE MODE の参加者1人ぶん。
    /// 同じお題・同じ予算・同じ乱数種で作った祭りを、最後に売上で比べる。
    /// JsonUtility でそのまま保存できるよう、フィールドはすべて public。
    /// </summary>
    [Serializable]
    public sealed class BattleEntry
    {
        /// <summary>参加者名。</summary>
        public string PlayerName = "プレイヤー";

        /// <summary>その人が書いた Matsuri Script。あとで見せ合えるように残す (§54)。</summary>
        public string SourceCode = "";

        /// <summary>その人の成績。</summary>
        public FestivalResult Result = new FestivalResult();

        /// <summary>投稿日時。"yyyy-MM-dd HH:mm:ss" 形式。</summary>
        public string SubmittedAt = "";

        public BattleEntry() { }

        public BattleEntry(string playerName, FestivalResult result)
            : this(playerName, result != null ? result.SourceCode : null, result)
        {
        }

        public BattleEntry(string playerName, string sourceCode, FestivalResult result)
        {
            if (!string.IsNullOrEmpty(playerName)) PlayerName = playerName;
            SourceCode = sourceCode ?? (result != null ? result.SourceCode : null) ?? "";
            Result = result ?? new FestivalResult();
            StampSubmittedAt();
        }

        /// <summary>この参加者の売上 (§35 の比較軸)。</summary>
        public long Revenue => Result != null ? Result.Revenue : 0L;

        /// <summary>この参加者の総合スコア。売上が同点のときの比較に使う。</summary>
        public long TotalScore => Result != null ? Result.TotalScore : 0L;

        /// <summary>来場者数。</summary>
        public int VisitorCount => Result != null ? Result.VisitorCount : 0;

        /// <summary>平均満足度（0〜100 に正規化した値）。</summary>
        public float SatisfactionPercent
        {
            get
            {
                if (Result == null) return 0f;
                float v = Result.AverageSatisfaction;
                return v <= 1.0001f ? v * 100f : v;
            }
        }

        /// <summary>SubmittedAt に「今」を入れる。すでに入っていれば何もしない。</summary>
        public void StampSubmittedAt(bool overwrite = false)
        {
            if (!overwrite && !string.IsNullOrEmpty(SubmittedAt)) return;
            SubmittedAt = DateTime.Now.ToString(FestivalResult.DateFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>壊れた値を埋め直す。読み込み直後に呼ぶ。</summary>
        public void EnsureValid()
        {
            if (string.IsNullOrEmpty(PlayerName)) PlayerName = "プレイヤー";
            SourceCode ??= "";
            Result ??= new FestivalResult();
            if (string.IsNullOrEmpty(SubmittedAt)) StampSubmittedAt();
        }

        public override string ToString() => $"{PlayerName}　売上 {Revenue:N0}円";
    }

    /// <summary>
    /// 仕様書 §46 / §77。BATTLE MODE の1回ぶんの勝負。
    ///
    /// 公平性が肝なので、参加者全員が
    /// 「同じお題」「同じ予算」「同じ乱数種 (<see cref="Seed"/>)」で祭りを作る。
    /// Seed は VisitorManager の乱数に渡され、来場の並びも好みも同じ列になる。
    /// </summary>
    [Serializable]
    public sealed class BattleSession
    {
        /// <summary>セッションID。保存ファイル名にもなる。</summary>
        public string Id = "";

        /// <summary>全員共通のお題ID。</summary>
        public string ChallengeId = "";

        /// <summary>画面に出すお題名。</summary>
        public string ChallengeName = "";

        /// <summary>全員共通の予算。</summary>
        public long Budget = 1000000L;

        /// <summary>全員共通の乱数種。これが同じなら来場者の列も好みも同じになる。</summary>
        public int Seed;

        /// <summary>会場の範囲（メートル）。お題の敷地をそのまま持つ。</summary>
        public float MinX = -60f;
        public float MaxX = 60f;
        public float MinZ = -60f;
        public float MaxZ = 60f;

        /// <summary>制限時間（ゲーム内の分）。0以下なら制限なし。</summary>
        public int TimeLimitMinutes = 300;

        /// <summary>作成日時。"yyyy-MM-dd HH:mm:ss" 形式。</summary>
        public string CreatedAt = "";

        /// <summary>参加者（投稿順）。</summary>
        public List<BattleEntry> Entries = new List<BattleEntry>();

        public BattleSession() { }

        public BattleSession(string id, ChallengeDefinition challenge, int seed)
        {
            Id = id ?? "";
            Seed = seed;
            CreatedAt = DateTime.Now.ToString(FestivalResult.DateFormat, CultureInfo.InvariantCulture);
            ApplyChallenge(challenge);
        }

        /// <summary>お題の条件（予算・敷地・制限時間）を取り込む。</summary>
        public void ApplyChallenge(ChallengeDefinition challenge)
        {
            if (challenge == null) return;

            ChallengeId = challenge.Id ?? "";
            ChallengeName = string.IsNullOrEmpty(challenge.DisplayName) ? ChallengeId : challenge.DisplayName;
            Budget = challenge.Budget;
            TimeLimitMinutes = challenge.TimeLimitMinutes;
            MinX = challenge.MinX;
            MaxX = challenge.MaxX;
            MinZ = challenge.MinZ;
            MaxZ = challenge.MaxZ;
        }

        /// <summary>
        /// このセッションの条件を <see cref="ChallengeDefinition"/> に戻す。
        /// 元のプリセットが見つかればそれを土台にし、予算と敷地はセッションの値で上書きする。
        /// </summary>
        public ChallengeDefinition ToChallenge()
        {
            var preset = ChallengePresets.Find(ChallengeId);

            var challenge = new ChallengeDefinition(
                string.IsNullOrEmpty(ChallengeId) ? "battle" : ChallengeId,
                string.IsNullOrEmpty(ChallengeName) ? "BATTLE" : ChallengeName,
                preset != null ? preset.Description : "同じ条件で祭りを作り、売上で勝負する。",
                Budget)
            {
                TimeLimitMinutes = TimeLimitMinutes,
                MinX = MinX,
                MaxX = MaxX,
                MinZ = MinZ,
                MaxZ = MaxZ
            };

            if (preset != null)
            {
                challenge.AllowedStallIds = preset.AllowedStallIds;
                challenge.RequiredStallIds = preset.RequiredStallIds;
                challenge.RequireFireworks = preset.RequireFireworks;
            }

            return challenge;
        }

        /// <summary>会場の範囲。</summary>
        public GroundBounds ToBounds() => new GroundBounds(MinX, MaxX, MinZ, MaxZ);

        /// <summary>参加者数。</summary>
        public int EntryCount => Entries != null ? Entries.Count : 0;

        /// <summary>壊れた値を埋め直す。読み込み直後に呼ぶ。</summary>
        public void EnsureValid()
        {
            if (string.IsNullOrEmpty(Id)) Id = "battle";
            Entries ??= new List<BattleEntry>();

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i] == null) Entries.RemoveAt(i);
                else Entries[i].EnsureValid();
            }

            if (string.IsNullOrEmpty(CreatedAt))
                CreatedAt = DateTime.Now.ToString(FestivalResult.DateFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>画面上部に出す1行の説明。</summary>
        public string Describe()
        {
            string name = string.IsNullOrEmpty(ChallengeName) ? "BATTLE" : ChallengeName;
            return $"{name}　予算 {Budget:N0}円　乱数種 {Seed}　参加 {EntryCount}人";
        }

        public override string ToString() => Describe();
    }
}
