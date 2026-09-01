using UnityEngine;

namespace Matsuri.Core
{
    /// <summary>
    /// ログの入口を1本にする。バッチモードでの自動検証 (§67 テスト) では
    /// [MATSURI] 接頭辞で grep する。
    /// </summary>
    public static class MatsuriLog
    {
        public const string Prefix = "[MATSURI]";

        public static bool Verbose = false;

        public static void Info(string message)
        {
            if (Verbose) Debug.Log($"{Prefix} {message}");
        }

        public static void Always(string message) => Debug.Log($"{Prefix} {message}");

        public static void Warn(string message) => Debug.LogWarning($"{Prefix} {message}");

        public static void Error(string message) => Debug.LogError($"{Prefix} {message}");

        /// <summary>バッチモードの検証スクリプトが読む行。</summary>
        public static void Build(string message) => Debug.Log($"[MATSURI-BUILD] {message}");
    }
}
