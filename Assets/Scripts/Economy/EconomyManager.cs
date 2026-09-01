using System;
using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Data;
using UnityEngine;

namespace Matsuri.Economy
{
    /// <summary>
    /// 仕様書 §31 / §32。祭りのお金をすべてここで管理する。
    /// 建設費は <see cref="TrySpend"/> を通してのみ減り、
    /// 売上は <see cref="AddRevenue"/> を通してのみ増える。
    /// 数値は BalanceConfig から取り、ハードコードしない (§31)。
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class EconomyManager : MonoBehaviour
    {
        [Tooltip("バランス設定。未設定なら GameManager から取得する。")]
        public BalanceConfig Balance;

        long _initialBudget;
        long _budget;
        long _spent;
        long _revenue;
        int _salesCount;
        bool _unlimited;

        readonly Dictionary<string, long> _revenueByStall = new Dictionary<string, long>(16);
        readonly Dictionary<string, int> _salesByStall = new Dictionary<string, int>(16);

        /// <summary>残っている予算。</summary>
        public long Budget => _budget;

        /// <summary>建設に使った合計金額。</summary>
        public long Spent => _spent;

        /// <summary>祭りの売上合計 (§32)。</summary>
        public long Revenue => _revenue;

        /// <summary>売れた回数の合計。</summary>
        public int SalesCount => _salesCount;

        /// <summary>FREE MODE の予算無制限フラグ (§46)。</summary>
        public bool Unlimited => _unlimited;

        /// <summary>開始時の予算。結果画面の「使った金額」表示に使う。</summary>
        public long InitialBudget => _initialBudget;

        /// <summary>残額が変わった。HUD が受ける。</summary>
        public event Action<long> BudgetChanged;

        /// <summary>売上が変わった。HUD が受ける。</summary>
        public event Action<long> RevenueChanged;

        void Awake()
        {
            if (Balance == null && GameManager.Instance != null) Balance = GameManager.Instance.Balance;
            if (_initialBudget == 0 && Balance != null) Initialize(Balance, GameManager.Instance != null ? GameManager.Instance.Mode : GameMode.Free);
        }

        /// <summary>
        /// モードに応じて予算を決める。
        /// FREE モードで BalanceConfig.FreeModeBudget が負なら予算無制限として扱う。
        /// </summary>
        public void Initialize(BalanceConfig balance, GameMode mode)
        {
            if (balance != null) Balance = balance;

            long budget = Balance != null ? Balance.InitialBudget : 1000000L;
            _unlimited = false;

            if (mode == GameMode.Free && Balance != null)
            {
                if (Balance.FreeModeBudget < 0)
                {
                    _unlimited = true;
                    budget = long.MaxValue / 4;   // 表示用。実質無制限
                }
                else
                {
                    budget = Balance.FreeModeBudget;
                }
            }

            _initialBudget = budget;
            ResetAll();

            MatsuriLog.Info(_unlimited
                ? "予算: 無制限（FREE MODE）"
                : $"予算: ¥{_initialBudget:N0}");
        }

        /// <summary>チャレンジモードなど、外から予算を直接指定する (§47)。</summary>
        public void SetBudget(long budget, bool unlimited = false)
        {
            _unlimited = unlimited;
            _initialBudget = unlimited ? long.MaxValue / 4 : budget;
            _budget = _initialBudget;
            _spent = 0;
            BudgetChanged?.Invoke(_budget);
        }

        public bool CanAfford(long cost) => _unlimited || cost <= _budget;

        /// <summary>
        /// 建設費を払う。足りなければ何も減らさず false を返す。
        /// プレイヤー向けのメッセージ表示は呼び出し側（FestivalManager）が行う。
        /// </summary>
        public bool TrySpend(long cost, string what)
        {
            if (cost < 0) cost = 0;

            if (!CanAfford(cost))
            {
                MatsuriLog.Info($"予算不足: 「{what}」に ¥{cost:N0} 必要ですが、残り ¥{_budget:N0} しかありません。");
                return false;
            }

            if (!_unlimited)
            {
                _budget -= cost;
                _spent += cost;
                BudgetChanged?.Invoke(_budget);
            }
            else
            {
                _spent += cost;
            }

            MatsuriLog.Info($"支出: 「{what}」 ¥{cost:N0} → 残り ¥{(_unlimited ? -1 : _budget):N0}");
            return true;
        }

        /// <summary>屋台で1つ売れた。売上と予算の両方に反映する (§32)。</summary>
        public void AddRevenue(long amount, string stallId)
        {
            if (amount <= 0) return;

            _revenue += amount;
            _salesCount++;

            if (!_unlimited)
            {
                _budget += amount;
                BudgetChanged?.Invoke(_budget);
            }

            if (!string.IsNullOrEmpty(stallId))
            {
                _revenueByStall.TryGetValue(stallId, out long current);
                _revenueByStall[stallId] = current + amount;

                _salesByStall.TryGetValue(stallId, out int count);
                _salesByStall[stallId] = count + 1;
            }

            RevenueChanged?.Invoke(_revenue);
        }

        /// <summary>屋台の種類ごとの売上 (§36 の「人気No.1」判定に使う)。</summary>
        public long GetStallRevenue(string stallId)
        {
            if (string.IsNullOrEmpty(stallId)) return 0;
            return _revenueByStall.TryGetValue(stallId, out long v) ? v : 0;
        }

        /// <summary>屋台の種類ごとの販売数。</summary>
        public int GetStallSalesCount(string stallId)
        {
            if (string.IsNullOrEmpty(stallId)) return 0;
            return _salesByStall.TryGetValue(stallId, out int v) ? v : 0;
        }

        /// <summary>売上のある屋台IDの一覧。</summary>
        public IEnumerable<string> EarningStallIds => _revenueByStall.Keys;

        /// <summary>最初の状態に戻す。RUN のたびに呼ばれる。</summary>
        public void ResetAll()
        {
            _budget = _initialBudget;
            _spent = 0;
            _revenue = 0;
            _salesCount = 0;
            _revenueByStall.Clear();
            _salesByStall.Clear();

            BudgetChanged?.Invoke(_budget);
            RevenueChanged?.Invoke(_revenue);
        }
    }
}
