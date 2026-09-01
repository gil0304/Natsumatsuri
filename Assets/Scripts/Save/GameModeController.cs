using System;
using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Economy;
using Matsuri.Script;
using UnityEngine;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §46 / §47。ゲームモードとお題を実際のゲームに反映する。
    ///
    /// - 予算       … EconomyManager を、お題の予算で初期化し直す
    /// - 会場の広さ … <see cref="ActiveBounds"/> に反映する（配置の検証はこれを見る）
    /// - 屋台の制限 … <see cref="IsStallAllowed"/> で判定できるようにする
    /// - 制限時間   … 実行中の BalanceConfig の EndMinutes に反映する
    ///
    /// BalanceConfig はプロジェクトのアセットなので直接書き換えず、
    /// 実行時に複製した <see cref="EffectiveBalance"/> を GameManager に差し替える。
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class GameModeController : MonoBehaviour
    {
        /// <summary>シーンに1つだけ置く想定。</summary>
        public static GameModeController Instance { get; private set; }

        [Header("状態 (§46)")]
        [SerializeField] GameMode _mode = GameMode.Free;

        [Tooltip("BATTLE MODE で自分の結果に付ける名前。")]
        public string LocalPlayerName = "プレイヤー1";

        /// <summary>現在のモード。</summary>
        public GameMode Mode => _mode;

        /// <summary>現在のお題。FREE / BATTLE では null のことがある。</summary>
        public ChallengeDefinition CurrentChallenge { get; private set; }

        /// <summary>このモードで実際に使われている会場範囲。</summary>
        public GroundBounds ActiveBounds { get; private set; } = GroundBounds.Default;

        /// <summary>このモード用に複製した BalanceConfig。モードを適用していなければ元のアセット。</summary>
        public BalanceConfig EffectiveBalance { get; private set; }

        /// <summary>モードが切り替わったときに通知する。</summary>
        public event Action<GameMode> ModeChanged;

        BalanceConfig _originalBalance;   // 差し替え前のアセット
        BalanceConfig _clonedBalance;     // 自分で作った複製（破棄が必要）

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DisposeClone();
        }

        // ── モードの適用 ──────────────────────────────────────

        /// <summary>
        /// モードとお題をゲームに反映する。
        /// challenge が null の CHALLENGE は §47 の先頭のお題を使う。
        /// </summary>
        public void ApplyMode(GameMode mode, ChallengeDefinition challenge)
        {
            _mode = mode;

            if (mode == GameMode.Challenge)
                CurrentChallenge = challenge ?? ChallengePresets.Default;
            else if (mode == GameMode.Battle)
                CurrentChallenge = ResolveBattleChallenge(challenge);
            else
                CurrentChallenge = challenge;

            var game = GameManager.Instance;
            var sourceBalance = ResolveSourceBalance(game);

            ActiveBounds = CurrentChallenge != null ? CurrentChallenge.ToBounds() : GroundBounds.Default;

            BalanceConfig runtimeBalance = BuildRuntimeBalance(sourceBalance);
            EffectiveBalance = runtimeBalance;

            if (game != null)
            {
                game.Mode = mode;
                if (runtimeBalance != null) game.Balance = runtimeBalance;
            }

            var economy = ResolveEconomy(game);
            if (economy != null && runtimeBalance != null)
            {
                economy.Initialize(runtimeBalance, mode);
            }
            else if (economy == null)
            {
                MatsuriLog.Warn("EconomyManager が見つからないため、予算を反映できませんでした。");
            }

            MatsuriLog.Always($"モードを適用しました: {DescribeMode()}");
            ModeChanged?.Invoke(mode);
        }

        /// <summary>お題を指定せず FREE MODE に戻す。</summary>
        public void ApplyFreeMode() => ApplyMode(GameMode.Free, null);

        /// <summary>IDでお題を選んで CHALLENGE MODE にする。</summary>
        public bool ApplyChallengeById(string challengeId)
        {
            var challenge = ChallengePresets.Find(challengeId);
            if (challenge == null)
            {
                MatsuriLog.Warn($"お題が見つかりませんでした: {challengeId}");
                return false;
            }
            ApplyMode(GameMode.Challenge, challenge);
            return true;
        }

        BalanceConfig ResolveSourceBalance(GameManager game)
        {
            if (_originalBalance != null) return _originalBalance;

            var balance = game != null ? game.Balance : null;
            if (balance == null) balance = UnityEngine.Object.FindFirstObjectByType<GameManager>()?.Balance;

            _originalBalance = balance;
            return balance;
        }

        EconomyManager ResolveEconomy(GameManager game)
        {
            if (game != null && game.Economy != null) return game.Economy;
            return UnityEngine.Object.FindFirstObjectByType<EconomyManager>();
        }

        /// <summary>
        /// お題の数値を載せた BalanceConfig を作る。
        /// お題が無ければ元のアセットをそのまま使う（複製しない）。
        /// </summary>
        BalanceConfig BuildRuntimeBalance(BalanceConfig source)
        {
            if (source == null)
            {
                MatsuriLog.Warn("BalanceConfig が無いため、モードの数値を反映できませんでした。");
                return null;
            }

            if (CurrentChallenge == null)
            {
                DisposeClone();
                return source;
            }

            DisposeClone();

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = $"{source.name} ({CurrentChallenge.Id})";
            clone.hideFlags = HideFlags.HideAndDontSave;

            clone.InitialBudget = CurrentChallenge.Budget;
            clone.FreeModeBudget = CurrentChallenge.Budget;

            if (CurrentChallenge.TimeLimitMinutes > 0)
                clone.EndMinutes = clone.StartMinutes + CurrentChallenge.TimeLimitMinutes;

            _clonedBalance = clone;
            return clone;
        }

        void DisposeClone()
        {
            if (_clonedBalance == null) return;

            if (Application.isPlaying) Destroy(_clonedBalance);
            else DestroyImmediate(_clonedBalance);

            _clonedBalance = null;
        }

        // ── 制限の判定 ────────────────────────────────────────

        /// <summary>この屋台を建ててよいか (§47)。</summary>
        public bool IsStallAllowed(string stallId)
        {
            if (_mode != GameMode.Challenge || CurrentChallenge == null) return true;
            return CurrentChallenge.IsStallAllowed(stallId);
        }

        /// <summary>その座標が今の会場の中か。</summary>
        public bool IsPositionAllowed(float x, float z) => ActiveBounds.Contains(x, z);

        /// <summary>今のモードで使える予算。</summary>
        public long CurrentBudget
        {
            get
            {
                if (CurrentChallenge != null) return CurrentChallenge.Budget;
                if (EffectiveBalance != null) return EffectiveBalance.InitialBudget;
                return 0L;
            }
        }

        /// <summary>結果に載せるモード名。"FREE" / "CHALLENGE: FOOD FESTIVAL" / "BATTLE"。</summary>
        public string DescribeMode()
        {
            switch (_mode)
            {
                case GameMode.Challenge:
                    return CurrentChallenge != null
                        ? $"CHALLENGE: {CurrentChallenge.DisplayName}"
                        : "CHALLENGE";
                case GameMode.Battle:
                    return CurrentChallenge != null
                        ? $"BATTLE: {CurrentChallenge.DisplayName}"
                        : "BATTLE";
                default:
                    return "FREE";
            }
        }

        // ── BATTLE MODE (§46 / §77) ──────────────────────────
        // 記録の実体は BattleMode（セッション + 乱数種）が持つ。
        // ここはゲーム側からの入口をまとめるだけにする。

        /// <summary>いま進行中の勝負。BATTLE でなければ null のことがある。</summary>
        public BattleSession CurrentBattle => BattleMode.Current;

        /// <summary>
        /// 勝負の乱数種。セッション中だけ値を持つ。
        /// VisitorManager はこれ（= <see cref="BattleMode.CurrentSeed"/>）を読んで
        /// 来場者の乱数の種にする。種が同じなら全員まったく同じ来場の列になる。
        /// </summary>
        public int? BattleSeed => BattleMode.CurrentSeed;

        /// <summary>参加者一覧（登録順）。</summary>
        public IReadOnlyList<BattleEntry> BattleEntries
            => BattleMode.Current != null ? (IReadOnlyList<BattleEntry>)BattleMode.Current.Entries : EmptyEntries;

        static readonly BattleEntry[] EmptyEntries = Array.Empty<BattleEntry>();

        /// <summary>
        /// BATTLE で使うお題を決める。
        /// すでに同じお題のセッションがあればそれを使い、無ければ新しく始める。
        /// </summary>
        ChallengeDefinition ResolveBattleChallenge(ChallengeDefinition requested)
        {
            var session = BattleMode.Current;

            bool sameChallenge = session != null
                && (requested == null || string.Equals(session.ChallengeId, requested.Id, StringComparison.Ordinal));

            if (!sameChallenge)
                session = BattleMode.StartSession(requested ?? ChallengePresets.Default);

            return session.ToChallenge();
        }

        /// <summary>参加者の結果を追加する。同じ名前があれば良い方（売上の高い方）を残す。</summary>
        public void AddBattleEntry(string playerName, FestivalResult result)
        {
            if (result == null)
            {
                MatsuriLog.Warn("BATTLE に登録する結果がありません。");
                return;
            }

            if (BattleMode.Current == null)
                BattleMode.StartSession(CurrentChallenge ?? ChallengePresets.Default);

            string name = string.IsNullOrEmpty(playerName) ? LocalPlayerName : playerName;
            BattleMode.Submit(name, result.SourceCode, result);
        }

        /// <summary>売上の高い順に並べた参加者一覧 (§46)。</summary>
        public List<BattleEntry> GetBattleRanking() => new List<BattleEntry>(BattleMode.GetRanking());

        /// <summary>その参加者の順位（1始まり。いなければ 0）。</summary>
        public int GetBattleRank(string playerName) => BattleMode.GetRank(playerName);

        /// <summary>BATTLE の記録を消す（お題と乱数種はそのまま。§37「同じ条件でもう一度」）。</summary>
        public void ClearBattle()
        {
            if (BattleMode.Current == null) return;
            BattleMode.RestartSameConditions();
        }

        /// <summary>勝負を終える。記録を残したいときは先に <see cref="BattleMode.SaveSession()"/> を呼ぶ。</summary>
        public void EndBattle() => BattleMode.EndSession();

        /// <summary>
        /// 祭りが終わったときに呼ぶ。モード名を結果に書き込み、
        /// ランキング (§37) に送り、BATTLE ならセッションにも積む。
        /// 戻り値はランキング順位（判定できなければ 0）。
        /// </summary>
        public int RegisterResult(FestivalResult result, string playerName = null)
        {
            if (result == null) return 0;

            result.ModeName = DescribeMode();
            result.StampCreatedDate();
            ScoreRules.ApplyTo(result, EffectiveBalance);

            if (_mode == GameMode.Battle)
                AddBattleEntry(playerName ?? LocalPlayerName, result);

            RankingService.SubmitBoth(result);
            return RankingService.GetRank(result);
        }
    }
}
