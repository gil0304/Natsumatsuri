using System;
using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Data;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §37。ランキングへの唯一の入口。
    /// 実体は <see cref="IRankingBackend"/> で、既定はローカルJSON。
    /// オンライン化するときは <see cref="UseRemote"/> を呼ぶだけでよい。
    ///
    /// ローカルの記録は「常に残る」ことを保証する。
    /// オンラインは設定されているときだけ使い、失敗しても
    /// ローカルの記録とゲームの進行には一切影響させない。
    /// </summary>
    public static class RankingService
    {
        static IRankingBackend _current;
        static LocalJsonRanking _local;

        /// <summary>ローカルJSONのランキング。オンラインでも必ずここには残す。</summary>
        public static IRankingBackend Local => LocalBackend;

        /// <summary>ローカルJSONの実体（送信キューを触るとき用）。</summary>
        public static LocalJsonRanking LocalBackend => _local ??= new LocalJsonRanking();

        /// <summary>現在の保存先。未設定ならローカルJSONを使う。</summary>
        public static IRankingBackend Current
        {
            get => _current ??= LocalBackend;
            set
            {
                _current = value;
                MatsuriLog.Always($"ランキングの保存先を切り替えました: {(value != null ? value.DisplayName : "ローカル（既定）")}");
                Updated?.Invoke();
            }
        }

        /// <summary>ランキングが切り替わった・更新されたときに通知する（UIの再描画用）。</summary>
        public static event Action Updated;

        /// <summary>いまオンラインに繋がっているか。</summary>
        public static bool IsOnline
        {
            get
            {
                var backend = Current;
                return backend != null && backend.IsOnline;
            }
        }

        // ── 保存先の切り替え ──────────────────────────────────

        /// <summary>ローカルJSONに戻す。</summary>
        public static void UseLocal() => Current = LocalBackend;

        /// <summary>ローカルJSONの実体を差し替える（保存先を変えたいとき・テスト用）。</summary>
        public static void UseLocal(LocalJsonRanking backend)
        {
            _local = backend ?? new LocalJsonRanking();
            Current = _local;
        }

        /// <summary>
        /// オンラインに切り替える。
        /// endpoint が空、または http/https でなければローカルのままにする
        /// （設定されていないのに勝手に通信しないため）。
        /// </summary>
        public static bool UseRemote(string endpoint, string apiKey = null)
        {
            if (!RankingSettings.IsValidEndpoint(endpoint))
            {
                MatsuriLog.Warn("オンラインランキングの送信先が正しくないため、ローカルのままにします。");
                UseLocal();
                return false;
            }

            RankingSettings.Endpoint = endpoint;
            if (apiKey != null) RankingSettings.ApiKey = apiKey;

            var remote = new RemoteRanking(RankingSettings.Endpoint, RankingSettings.ApiKey, LocalBackend);
            Current = remote;

            // 前回オフラインで貯まったぶんをまとめて送る。
            remote.FlushQueue();
            return true;
        }

        /// <summary>いま使っているオンライン保存先。ローカルなら null。</summary>
        public static RemoteRanking Remote => Current as RemoteRanking;

        // ── 登録・取得 ────────────────────────────────────────

        /// <summary>
        /// 結果を登録する。TotalScore が未計算なら balance から計算して埋める。
        /// 登録後の順位を返す（判定できなければ 0）。
        /// </summary>
        public static int Submit(FestivalResult result, BalanceConfig balance = null)
        {
            if (result == null)
            {
                MatsuriLog.Warn("ランキングへ送る結果がありません。");
                return 0;
            }

            if (result.TotalScore <= 0)
                ScoreRules.ApplyTo(result, balance);

            try
            {
                Current.Submit(result);
                Updated?.Invoke();
                return Current.GetRank(result);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"ランキングへの登録に失敗しました: {e.Message}");
                return 0;
            }
        }

        /// <summary>
        /// ローカルには必ず、オンラインは設定されているときだけ登録する (§37)。
        /// どちらが失敗しても例外は投げない。
        /// </summary>
        public static void SubmitBoth(FestivalResult result)
        {
            if (result == null)
            {
                MatsuriLog.Warn("ランキングへ送る結果がありません。");
                return;
            }

            if (result.TotalScore <= 0) ScoreRules.ApplyTo(result, null);

            try
            {
                LocalBackend.Submit(result);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"ローカルランキングへの登録に失敗しました: {e.Message}");
            }

            var backend = Current;
            if (backend != null && !ReferenceEquals(backend, LocalBackend))
            {
                try
                {
                    backend.Submit(result);
                }
                catch (Exception e)
                {
                    MatsuriLog.Warn($"オンラインランキングへの登録に失敗しました（ローカルには残っています）: {e.Message}");
                }
            }

            Updated?.Invoke();
        }

        /// <summary>上位 count 件（売上の降順 §35）。</summary>
        public static List<FestivalResult> GetTop(int count = 10)
        {
            try
            {
                return Current.GetTop(count) ?? new List<FestivalResult>();
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"ランキングの取得に失敗しました: {e.Message}");
                return new List<FestivalResult>();
            }
        }

        /// <summary>この結果の順位（1始まり。判定できなければ 0）。</summary>
        public static int GetRank(FestivalResult result)
        {
            if (result == null) return 0;
            try
            {
                return Current.GetRank(result);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"ランキング順位の取得に失敗しました: {e.Message}");
                return 0;
            }
        }

        /// <summary>「第3位 / 12件中」のような表示文 (§36 の結果画面用)。</summary>
        public static string DescribeRank(FestivalResult result)
        {
            int rank = GetRank(result);
            if (rank <= 0) return "順位なし";
            if (rank == 1) return "第1位　過去最高の売上です";
            return $"第{rank}位";
        }
    }
}
