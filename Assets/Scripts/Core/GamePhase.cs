namespace Matsuri.Core
{
    /// <summary>ゲーム全体の進行フェーズ。</summary>
    public enum GamePhase
    {
        /// <summary>コードを書いている状態。まだ祭りは始まっていない。</summary>
        Editing,

        /// <summary>RUN 直後。建設演出 (§39) が走っている。</summary>
        Building,

        /// <summary>祭り開催中。17:00〜22:00 が進行する。</summary>
        Running,

        /// <summary>22:00 を過ぎ、結果画面 (§36)。</summary>
        Finished
    }

    /// <summary>ゲームモード (§46)。</summary>
    public enum GameMode
    {
        /// <summary>予算無制限。自由に祭りを作る。</summary>
        Free,

        /// <summary>条件付き。予算・制限・お題がある。</summary>
        Challenge,

        /// <summary>同一条件で作り、最後に売上を比較する。</summary>
        Battle
    }
}
