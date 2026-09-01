using System;
using System.Collections.Generic;
using Matsuri.Audio;
using Matsuri.Core;
using Matsuri.Script;
using Matsuri.Script.Commands;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 仕様書 §40 / §51。RUN ボタンの受け皿。
    ///
    /// ここは「コンパイルして、結果を配る」だけの薄い層にする (§66)。
    ///   ・エラーがあれば <see cref="Compiled"/> で UI に渡すだけ。祭りは作らない (§42)
    ///   ・エラーが無ければ FestivalManager にプランを渡す
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class ScriptManager : MonoBehaviour
    {
        [Tooltip("起動時にサンプルコード (§44) を読み込むか。")]
        public bool LoadSampleOnStart = true;

        string _currentSource = string.Empty;

        /// <summary>最後に実行しようとしたソースコード。セーブ (§54) と結果画面 (§36) が使う。</summary>
        public string CurrentSource => _currentSource;

        /// <summary>最後にコンパイルした結果。エラーがあった場合も入っている。</summary>
        public FestivalPlan LastPlan { get; private set; }

        /// <summary>コンパイルが終わった。UI はこれを受けてエラー表示を更新する (§42)。</summary>
        public event Action<FestivalPlan> Compiled;

        void Start()
        {
            if (!LoadSampleOnStart) return;
            if (!string.IsNullOrEmpty(_currentSource)) return;

            try
            {
                _currentSource = MatsuriSamples.Starter ?? string.Empty;
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"サンプルコードの読み込みに失敗しました: {e.Message}");
                _currentSource = string.Empty;
            }
        }

        /// <summary>エディタの内容を差し替える（実行はしない）。</summary>
        public void SetSource(string source) => _currentSource = source ?? string.Empty;

        /// <summary>
        /// RUN。コードを解析して祭りを建てる。
        /// エラーが1つでもあれば、世界には一切触れない。
        /// </summary>
        public void RunCode(string source)
        {
            _currentSource = source ?? string.Empty;

            var game = GameManager.Instance;
            IMatsuriCatalog catalog = game != null ? game.Catalog : null;

            if (catalog == null)
            {
                var diagnostics = new List<Diagnostic>
                {
                    Diagnostic.Error(1, 1, 0,
                        "屋台のデータ（カタログ）が読み込まれていないため、コードを実行できません。",
                        "Unity のメニュー Matsuri/1. Generate Data Assets を実行してください。")
                };

                LastPlan = FestivalPlan.Failed(diagnostics);
                Compiled?.Invoke(LastPlan);
                MatsuriLog.Error("MatsuriCatalog が GameManager に設定されていません。");
                return;
            }

            FestivalPlan plan;
            try
            {
                plan = MatsuriCompiler.Compile(_currentSource, catalog);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"コードの解析中に予期しないエラーが起きました: {e}");

                var diagnostics = new List<Diagnostic>
                {
                    Diagnostic.Error(1, 1, 0, "コードの解析中に問題が起きました。書き方を見直してみてください。")
                };
                plan = FestivalPlan.Failed(diagnostics);
            }

            LastPlan = plan;
            Compiled?.Invoke(plan);

            if (plan.HasErrors)
            {
                int errors = 0;
                for (int i = 0; i < plan.Diagnostics.Count; i++)
                    if (plan.Diagnostics[i].Severity == DiagnosticSeverity.Error) errors++;

                MatsuriLog.Always($"コードに {errors}件 の直すところがあります。祭りはまだ作られていません。");

                var audio = game != null ? game.Audio : null;
                if (audio != null) audio.PlaySfx(MatsuriSfx.Error);

                var ui = game != null ? game.UI : null;
                if (ui != null) ui.ShowToast($"{errors}件のエラーがあります。左のエラー表示を見てください。", DiagnosticSeverity.Error);

                return;
            }

            var festival = game != null ? game.Festival : null;
            if (festival == null)
            {
                MatsuriLog.Error("FestivalManager が GameManager に設定されていません。");
                return;
            }

            festival.ApplyPlan(plan);
        }
    }
}
