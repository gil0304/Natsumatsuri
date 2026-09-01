using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace Matsuri.Core
{
    /// <summary>
    /// 日本語フォントの供給元。
    /// Unity の既定フォント (LiberationSans) は CJK グリフを持たず、
    /// 本作の UI・コード・屋台名はすべて日本語なので全部が豆腐(□)になる。
    /// そのため Noto Sans JP (SIL OFL 1.1) を同梱し、実行時に FontAsset を作る。
    ///
    /// フォント本体: Assets/UI/Fonts/Resources/NotoSansJP.ttf
    /// ライセンス:   Assets/UI/Fonts/OFL.txt
    /// </summary>
    public static class MatsuriFontProvider
    {
        const string ResourcePath = "NotoSansJP";

        static FontAsset _fontAsset;
        static Font _rawFont;
        static bool _attempted;

        /// <summary>UI Toolkit / TextMeshPro に渡す FontAsset。失敗したら null。</summary>
        public static FontAsset JapaneseFontAsset
        {
            get
            {
                EnsureLoaded();
                return _fontAsset;
            }
        }

        /// <summary>素の Font。FontAsset の生成に失敗した場合のフォールバックに使う。</summary>
        public static Font JapaneseFont
        {
            get
            {
                EnsureLoaded();
                return _rawFont;
            }
        }

        public static bool IsAvailable => JapaneseFontAsset != null || JapaneseFont != null;

        static void EnsureLoaded()
        {
            if (_attempted) return;
            _attempted = true;

            _rawFont = Resources.Load<Font>(ResourcePath);
            if (_rawFont == null)
            {
                MatsuriLog.Warn(
                    "日本語フォントが見つかりません (Assets/UI/Fonts/Resources/NotoSansJP.ttf)。" +
                    "日本語が □ で表示される場合はこのファイルを確認してください。");
                return;
            }

            try
            {
                // Dynamic なので、使われた文字だけがその都度アトラスに焼かれる。
                // 常用漢字を先読みしないぶん起動が速く、未知の漢字も表示できる。
                _fontAsset = FontAsset.CreateFontAsset(
                    _rawFont,
                    samplingPointSize: 90,
                    atlasPadding: 9,
                    renderMode: GlyphRenderMode.SDFAA,
                    atlasWidth: 1024,
                    atlasHeight: 1024,
                    atlasPopulationMode: AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);

                if (_fontAsset != null)
                {
                    _fontAsset.name = "NotoSansJP (Matsuri)";
                    _fontAsset.hideFlags = HideFlags.DontSave;
                }
            }
            catch (System.Exception e)
            {
                MatsuriLog.Warn($"FontAsset の生成に失敗しました: {e.Message} — 素の Font で代用します。");
                _fontAsset = null;
            }
        }

        /// <summary>テスト用。キャッシュを捨てて次回作り直す。</summary>
        public static void Reset()
        {
            _fontAsset = null;
            _rawFont = null;
            _attempted = false;
        }
    }
}
