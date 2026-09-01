using System;
using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.Save;
using UnityEngine;
using UnityEngine.UIElements;

namespace Matsuri.UI
{
    /// <summary>
    /// 仕様書 §46 / §77 BATTLE MODE の画面。
    /// 「複数人が同じ条件で祭りを作り、最後に売上を比較する」ための一覧を出す。
    ///
    /// 他のUIに依存しないよう、UIDocument と PanelSettings を自前で1枚だけ持つ。
    /// 表示は §37 の例に合わせて「1位　¥1,482,500」の形にする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattlePanel : MonoBehaviour
    {
        /// <summary>シーンに1つだけ置く想定。</summary>
        public static BattlePanel Instance { get; private set; }

        /// <summary>「同じ条件でもう一度」が押された。</summary>
        public event Action RetryRequested;

        /// <summary>「閉じる」が押された。</summary>
        public event Action Closed;

        /// <summary>勝負に結果を登録した直後。</summary>
        public event Action<BattleEntry> Submitted;

        PanelSettings _panelSettings;
        UIDocument _document;
        VisualElement _root;
        VisualElement _card;

        Label _titleLabel;
        Label _conditionLabel;
        Label _noticeLabel;
        TextField _nameField;
        Button _submitButton;
        ScrollView _list;

        FestivalResult _pendingResult;
        string _pendingSource = "";
        bool _built;

        /// <summary>参加者名。空なら「プレイヤー」。</summary>
        public string PlayerName
        {
            get => _nameField != null && !string.IsNullOrWhiteSpace(_nameField.value)
                ? _nameField.value.Trim()
                : "プレイヤー";
            set { if (_nameField != null) _nameField.value = value ?? ""; }
        }

        /// <summary>いま表示中か。</summary>
        public bool IsShown => _built && _root.style.display == DisplayStyle.Flex;

        /// <summary>BattlePanel を作って返す。すでにあればそれを返す。</summary>
        public static BattlePanel Create(Transform parent = null)
        {
            if (Instance != null) return Instance;

            var go = new GameObject("MatsuriBattlePanel");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<BattlePanel>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            Build();
            Hide();
            BattleMode.SessionChanged += Refresh;
        }

        void OnDestroy()
        {
            BattleMode.SessionChanged -= Refresh;
            if (Instance == this) Instance = null;
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        // ── 表示の制御 ────────────────────────────────────────

        /// <summary>画面を出す。</summary>
        public void Show()
        {
            if (!_built) Build();
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            Refresh();
        }

        /// <summary>画面を閉じる。</summary>
        public void Hide()
        {
            if (!_built) return;
            _root.style.display = DisplayStyle.None;
        }

        /// <summary>出ていれば閉じ、閉じていれば出す。</summary>
        public void Toggle()
        {
            if (IsShown) Hide(); else Show();
        }

        /// <summary>
        /// いま登録できる結果を渡す。祭りが終わったら呼ぶ。
        /// これを渡すまで「この祭りで勝負する」は押せない。
        /// </summary>
        public void SetResult(FestivalResult result, string sourceCode = null)
        {
            _pendingResult = result;
            _pendingSource = sourceCode ?? (result != null ? result.SourceCode : "") ?? "";
            Refresh();
        }

        /// <summary>一覧を作り直す。</summary>
        public void Refresh()
        {
            if (!_built) return;

            var session = BattleMode.Current;

            _conditionLabel.text = session != null
                ? session.Describe()
                : "勝負が始まっていません。BATTLE MODE を選ぶと同じ条件の勝負が始まります。";

            bool canSubmit = session != null && _pendingResult != null;
            MatsuriUiTheme.SetButtonEnabled(_submitButton, canSubmit, true);

            if (session == null) _noticeLabel.text = "BATTLE MODE を開始してください。";
            else if (_pendingResult == null) _noticeLabel.text = "祭りを最後まで実行すると、その結果で勝負できます。";
            else _noticeLabel.text = $"登録できる結果: 売上 {MatsuriUiTheme.FormatYen(_pendingResult.Revenue)}";

            BuildList(BattleMode.GetRanking());
        }

        // ── 画面の組み立て ────────────────────────────────────

        void Build()
        {
            if (_built) return;
            _built = true;

            CreateDocument();

            _root.style.position = Position.Absolute;
            _root.style.left = 0f;
            _root.style.right = 0f;
            _root.style.top = 0f;
            _root.style.bottom = 0f;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.86f);

            _card = new VisualElement();
            _card.style.width = 620f;
            _card.style.maxWidth = Length.Percent(94f);
            _card.style.maxHeight = Length.Percent(88f);
            _card.style.backgroundColor = MatsuriUiTheme.Hex("#0E1220FA");
            MatsuriUiTheme.SetRadius(_card, 16f);
            MatsuriUiTheme.SetBorder(_card, 1f, MatsuriUiTheme.Hex("#2E3650"));
            MatsuriUiTheme.SetPadding(_card, 26f, 30f, 22f, 30f);
            _root.Add(_card);

            BuildHeader();
            BuildSubmitRow();
            BuildListContainer();
            BuildFooter();
        }

        void CreateDocument()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "MatsuriBattlePanelSettings";
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = 140f;   // 通常のUIより手前
            _panelSettings.clearDepthStencil = true;
            _panelSettings.clearColor = false;
            _panelSettings.themeStyleSheet = FindTheme();

            var host = new GameObject("BattleUIDocument");
            host.transform.SetParent(transform, false);
            host.SetActive(false);
            _document = host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            host.SetActive(true);

            _root = _document.rootVisualElement;
            if (_root == null)
            {
                MatsuriLog.Error("BATTLE 画面の rootVisualElement を作れませんでした。");
                _root = new VisualElement();
            }
        }

        static ThemeStyleSheet FindTheme()
        {
            var theme = Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme");
            if (theme == null) theme = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            if (theme != null) return theme;

            var found = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>();
            return found != null && found.Length > 0 ? found[0] : null;
        }

        void BuildHeader()
        {
            _titleLabel = new Label("BATTLE MODE");
            _titleLabel.style.fontSize = 24f;
            _titleLabel.style.letterSpacing = 3f;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = MatsuriUiTheme.TextPrimary;
            MatsuriUiTheme.ApplyUiFont(_titleLabel);
            _card.Add(_titleLabel);

            var lead = new Label("同じお題・同じ予算・同じ乱数種で祭りを作り、売上で勝負する。");
            lead.style.fontSize = 12f;
            lead.style.color = MatsuriUiTheme.TextSecondary;
            lead.style.marginTop = 4f;
            MatsuriUiTheme.ApplyUiFont(lead);
            _card.Add(lead);

            _conditionLabel = new Label();
            _conditionLabel.style.fontSize = 12f;
            _conditionLabel.style.color = MatsuriUiTheme.AccentWarm;
            _conditionLabel.style.marginTop = 10f;
            _conditionLabel.style.whiteSpace = WhiteSpace.Normal;
            MatsuriUiTheme.ApplyUiFont(_conditionLabel);
            _card.Add(_conditionLabel);
        }

        void BuildSubmitRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 16f;
            _card.Add(row);

            _nameField = new TextField { value = "プレイヤー1" };
            _nameField.style.flexGrow = 1f;
            _nameField.style.height = 40f;
            _nameField.style.marginRight = 10f;
            _nameField.style.backgroundColor = MatsuriUiTheme.BgEditor;
            _nameField.style.color = MatsuriUiTheme.TextPrimary;
            MatsuriUiTheme.SetRadius(_nameField, MatsuriUiTheme.Radius);
            MatsuriUiTheme.SetBorder(_nameField, 1f, MatsuriUiTheme.BorderBright);
            MatsuriUiTheme.SetPadding(_nameField, 0f, 10f, 0f, 10f);
            MatsuriUiTheme.ApplyUiFont(_nameField);
            row.Add(_nameField);

            _submitButton = MatsuriUiTheme.CreateButton("この祭りで勝負する", SubmitCurrentResult, true);
            _submitButton.style.minWidth = 190f;
            row.Add(_submitButton);

            _noticeLabel = new Label();
            _noticeLabel.style.fontSize = 11f;
            _noticeLabel.style.color = MatsuriUiTheme.TextMuted;
            _noticeLabel.style.marginTop = 6f;
            _noticeLabel.style.whiteSpace = WhiteSpace.Normal;
            MatsuriUiTheme.ApplyUiFont(_noticeLabel);
            _card.Add(_noticeLabel);
        }

        void BuildListContainer()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.marginTop = 18f;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = MatsuriUiTheme.Border;
            MatsuriUiTheme.SetPadding(header, 0f, 4f, 6f, 4f);
            _card.Add(header);

            header.Add(CaptionLabel("順位", 58f, 0f, TextAnchor.MiddleLeft));
            header.Add(CaptionLabel("名前", 0f, 1f, TextAnchor.MiddleLeft));
            header.Add(CaptionLabel("売上", 140f, 0f, TextAnchor.MiddleRight));
            header.Add(CaptionLabel("来場者", 84f, 0f, TextAnchor.MiddleRight));
            header.Add(CaptionLabel("満足度", 78f, 0f, TextAnchor.MiddleRight));

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1f;
            _list.style.minHeight = 150f;
            _list.style.maxHeight = 320f;
            _card.Add(_list);
        }

        void BuildFooter()
        {
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.marginTop = 18f;
            _card.Add(footer);

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            footer.Add(left);
            left.Add(MatsuriUiTheme.CreateButton("同じ条件でもう一度", RetrySameConditions));
            left.Add(MatsuriUiTheme.CreateButton("勝負を保存", SaveSession));

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            footer.Add(right);
            right.Add(MatsuriUiTheme.CreateButton("閉じる", () =>
            {
                Hide();
                Closed?.Invoke();
            }));
        }

        static Label CaptionLabel(string text, float width, float grow, TextAnchor align)
        {
            var label = new Label(text);
            label.style.fontSize = MatsuriUiTheme.CaptionFontSize;
            label.style.letterSpacing = 2f;
            label.style.color = MatsuriUiTheme.TextSecondary;
            label.style.unityTextAlign = align;
            if (width > 0f) label.style.width = width;
            if (grow > 0f) label.style.flexGrow = grow;
            MatsuriUiTheme.ApplyUiFont(label);
            return label;
        }

        // ── 一覧 ──────────────────────────────────────────────

        void BuildList(IReadOnlyList<BattleEntry> ranking)
        {
            _list.Clear();

            if (ranking == null || ranking.Count == 0)
            {
                var empty = new Label("まだ誰も勝負していません。");
                empty.style.fontSize = 13f;
                empty.style.color = MatsuriUiTheme.TextMuted;
                MatsuriUiTheme.SetPadding(empty, 20f, 4f, 20f, 4f);
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                MatsuriUiTheme.ApplyUiFont(empty);
                _list.Add(empty);
                return;
            }

            for (int i = 0; i < ranking.Count; i++)
                _list.Add(CreateRow(i + 1, ranking[i]));
        }

        static VisualElement CreateRow(int rank, BattleEntry entry)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            MatsuriUiTheme.SetPadding(row, 9f, 4f, 9f, 4f);
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = MatsuriUiTheme.Hex("#1C2334");
            if (rank == 1) row.style.backgroundColor = new Color(1f, 0.76f, 0.29f, 0.07f);

            // §37 の表示例: 「1位　¥1,482,500」
            var rankLabel = new Label($"{rank}位");
            rankLabel.style.width = 58f;
            rankLabel.style.fontSize = 17f;
            rankLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rankLabel.style.color = RankColor(rank);
            MatsuriUiTheme.ApplyUiFont(rankLabel);
            row.Add(rankLabel);

            var nameLabel = new Label(entry != null ? entry.PlayerName : "—");
            nameLabel.style.flexGrow = 1f;
            nameLabel.style.fontSize = 14f;
            nameLabel.style.color = MatsuriUiTheme.TextPrimary;
            MatsuriUiTheme.ApplyUiFont(nameLabel);
            row.Add(nameLabel);

            row.Add(ValueLabel(MatsuriUiTheme.FormatYen(entry != null ? entry.Revenue : 0L),
                140f, 19f, MatsuriUiTheme.AccentYellow));
            row.Add(ValueLabel(MatsuriUiTheme.FormatCount(entry != null ? entry.VisitorCount : 0) + "人",
                84f, 13f, MatsuriUiTheme.AccentBlue));
            row.Add(ValueLabel((entry != null ? entry.SatisfactionPercent : 0f).ToString("0.0"),
                78f, 13f, MatsuriUiTheme.AccentGreen));

            return row;
        }

        static Label ValueLabel(string text, float width, float fontSize, Color color)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            label.style.color = color;
            MatsuriUiTheme.ApplyUiFont(label);
            return label;
        }

        static Color RankColor(int rank)
        {
            if (rank == 1) return MatsuriUiTheme.AccentYellow;
            if (rank == 2) return MatsuriUiTheme.TextPrimary;
            if (rank == 3) return MatsuriUiTheme.AccentWarm;
            return MatsuriUiTheme.TextSecondary;
        }

        // ── ボタンの中身 ──────────────────────────────────────

        void SubmitCurrentResult()
        {
            if (BattleMode.Current == null)
            {
                _noticeLabel.text = "勝負が始まっていないため登録できません。";
                return;
            }
            if (_pendingResult == null)
            {
                _noticeLabel.text = "先に祭りを実行してください。";
                return;
            }

            string name = PlayerName;
            BattleMode.Submit(name, _pendingSource, _pendingResult);

            _pendingResult = null;
            _pendingSource = "";
            Refresh();

            int rank = BattleMode.GetRank(name);
            _noticeLabel.text = rank > 0 ? $"{name} は現在 {rank}位 です。" : $"{name} を登録しました。";

            var ranking = BattleMode.GetRanking();
            if (rank > 0 && rank <= ranking.Count) Submitted?.Invoke(ranking[rank - 1]);
        }

        void RetrySameConditions()
        {
            BattleMode.RestartSameConditions();
            _pendingResult = null;
            _pendingSource = "";
            Refresh();
            RetryRequested?.Invoke();
        }

        void SaveSession()
        {
            if (BattleMode.Current == null)
            {
                _noticeLabel.text = "保存する勝負がありません。";
                return;
            }

            _noticeLabel.text = BattleMode.SaveSession()
                ? $"勝負を保存しました: {BattleMode.Current.Id}"
                : "勝負を保存できませんでした。";
        }
    }
}
