using System.Collections.Generic;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §37。ランキングの保存先。
    /// 初期版はローカルJSON (<see cref="LocalJsonRanking"/>)。
    /// 将来オンライン化するときは、この interface を実装した
    /// RemoteRanking を作って <see cref="RankingService.Current"/> に差し替えるだけでよい。
    ///
    /// メインランキングの並び順は §35 に従い「売上の降順」。
    /// </summary>
    public interface IRankingBackend
    {
        /// <summary>この保存先の表示名（UIに出す。「ローカル」「オンライン」など）。</summary>
        string DisplayName { get; }

        /// <summary>
        /// いまオンラインとして機能しているか。
        /// ローカルJSONは常に false。オンライン版は「最後の通信が成功したか」で決まる。
        /// </summary>
        bool IsOnline { get; }

        /// <summary>結果を登録する。</summary>
        void Submit(FestivalResult result);

        /// <summary>売上の高い順に上位 count 件を返す。件数が足りなければあるだけ返す。</summary>
        List<FestivalResult> GetTop(int count);

        /// <summary>
        /// この結果が何位になるかを返す（1始まり）。
        /// まだ登録していない結果を渡しても「登録したら何位か」を計算して返す。
        /// 判定できない場合は 0。
        /// </summary>
        int GetRank(FestivalResult result);
    }
}
