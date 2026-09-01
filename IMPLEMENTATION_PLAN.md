# MATSURI.exe — IMPLEMENTATION_PLAN.md

> 仕様書 §83 に基づく実装計画書。
> 本作は「夏祭りを題材にしたプログラミング教材」ではなく、
> **「高品質な夏祭り経営シミュレーションゲームを、プログラミングによって操作するゲーム」** である (§82)。

---

## 1. 現在のUnityプロジェクト状況

| 項目 | 内容 |
| --- | --- |
| プロジェクト名 | `Natsumatsuri` |
| 場所 | `/Users/gilryogo/Developer/gil/Natsumatsuri` |
| 元テンプレート | `com.unity.template.hdrp-blank-17.1.1` (HDRP Blank) |
| 既存Assets | `OutdoorsScene.unity` / `Settings/` (HDRP Asset x3 + Volume Profile) / `TutorialInfo/` / `InputSystem_Actions.inputactions` |
| 既存スクリプト | `TutorialInfo/Scripts/Readme.cs` のみ（テンプレート由来。ゲームコードはゼロ） |
| Git | 未初期化 → `.gitignore` を追加し `git init` 済み（コミットはユーザー判断） |
| Editor状態 | Unity Editor が起動中（プロジェクトロック保持） |

つまり **完全な白紙のHDRPプロジェクト**。ゲーム実装はゼロから行う。

---

## 2. 使用Unity Version

```
6000.4.8f1  (Unity 6.4)
```

`ProjectSettings/ProjectVersion.txt` で確認。仕様書 §4「Unity 6」に合致するため変更しない。

---

## 3. 使用Render Pipeline

```
HDRP (com.unity.render-pipelines.high-definition) 17.4.0
```

- `GraphicsSettings.m_CustomRenderPipeline` に `HDRenderPipelineAsset` 設定済み
- 仕様書 §4 / §68 により **URPへの変更は禁止**。HDRPのまま進める
- 夜祭りの「暗闇の中で屋台の光が浮かぶ」表現 (§59) はHDRPの物理ベースライティング + Emission + Bloom で実現する

---

## 4. 必要Package

### 既存（テンプレート同梱・変更なし）

| Package | Version | 用途 |
| --- | --- | --- |
| `com.unity.render-pipelines.high-definition` | 17.4.0 | HDRP本体 |
| `com.unity.shadergraph` | 17.4.0 | 水面Shader (§62)、布/紙Shader |
| `com.unity.visualeffectgraph` | 17.4.0 | 花火VFX (§61)、湯気・煙 |
| `com.unity.inputsystem` | 1.19.0 | カメラ操作 (§38) |
| `com.unity.modules.ai` | 1.0.0 | `NavMeshAgent` (§12 移動) |
| `com.unity.ugui` (TextMeshPro含む) | 2.0.0 | 日本語フォント描画基盤 |
| `com.unity.modules.uielements` | 1.0.0 | UI Toolkit (§4, §64) |
| `com.unity.test-framework` | 1.6.0 | Lexer/Parserのユニットテスト (§67) |

### 追加（本計画で `Packages/manifest.json` に追記済み）

| Package | Version | 追加理由 |
| --- | --- | --- |
| `com.unity.ai.navigation` | 2.0.14 | Unity 6 では NavMesh のベイクにこのパッケージが必須。屋台はコード実行で**実行時に生成される**ため、`NavMeshSurface.BuildNavMesh()` による**ランタイム再ベイク**が必要 (§28 移動, §30 行列) |
| `com.unity.cinemachine` | 3.1.7 | §4 指定。§38 の3カメラモード切替と、§80 の「祭り開催の瞬間」の演出ブレンドに使用 |

> 両パッケージともローカルUPMキャッシュ + `packages.unity.com` から解決可能なことを確認済み。

### 追加アセット（コードではないが必要）

| 物 | 入手方法 | ライセンス |
| --- | --- | --- |
| 日本語フォント Noto Sans JP | Google Fonts より取得し `Assets/UI/Fonts/` に配置 | SIL OFL 1.1（再配布可） |

Unityの既定フォント (LiberationSans) はCJKグリフを持たず、日本語UIが全て豆腐(□)になる。
本作はUI・コード・屋台名がすべて日本語であるため、**日本語フォントの導入は必須要件**として扱う。

---

## 5. Scene構成

### 方針：シーンは「コードで組み立てる」

Unity Editor のGUI操作に依存したシーン構築は、AI駆動開発 (§3, §82) と相性が悪く、
`.unity` YAMLの手書きは壊れやすい。そこで本作は次の構造を採る。

```
Assets/Scenes/Festival.unity          ← 中身はほぼ空
└─ [MatsuriBootstrap]                 ← GameObject 1個だけ
   └─ MatsuriBootstrap.cs (Awake)     ← ここから全世界を生成する
```

`MatsuriBootstrap` が実行時に生成するもの：

```
FESTIVAL_ROOT
├─ Managers/
│  ├─ GameManager
│  ├─ TimeManager        (17:00→22:00, 1秒=1分 §7)
│  ├─ EconomyManager     (予算/売上 §31,§32)
│  ├─ FestivalManager    (コマンド実行・建設演出 §39)
│  ├─ StallManager       (屋台台帳・行列 §30)
│  ├─ VisitorManager     (NPCプール・スポーン §25,§57)
│  ├─ EventManager       (花火/盆踊り/太鼓 §22)
│  ├─ AudioManager       (手続き生成オーディオ §24)
│  ├─ CameraManager      (Cinemachine 3カメラ §38)
│  ├─ UIManager          (UI Toolkit §9)
│  └─ ScriptManager      (Matsuri Script 実行 §40)
├─ World/
│  ├─ Ground             (会場の地面 + 参道)
│  ├─ NavMeshSurface     (ランタイムベイク)
│  ├─ Environment/       (鳥居・神社・木・外周)
│  ├─ Lighting/          (Moon / Ambient / Sky&Fog Volume §59)
│  └─ PostProcess Volume (Bloom/Tonemap/CA控えめ §60)
├─ Built/                (Matsuri Scriptで建った物すべて)
│  ├─ Stalls/
│  ├─ Decorations/
│  └─ Facilities/
├─ Visitors/             (NPCプール §57)
└─ Cameras/
   ├─ Main Camera (CinemachineBrain)
   ├─ CM_Build
   ├─ CM_Free
   └─ CM_Visitor
```

**利点**：シーンファイルの差分が発生せず、全構成がC#コードとしてレビュー・テスト可能。
Editorスクリプト `MatsuriProjectBuilder` は、このシーン資産とScriptableObject群を生成するだけでよい。

---

## 6. Script構成

`Assets/Scripts/` 以下（§55 準拠）。アセンブリは4つに分割（§66 責務分離）。

| Assembly | 場所 | 依存 | 役割 |
| --- | --- | --- | --- |
| `Matsuri.Script` | `Scripts/MatsuriScript/` | なし | Matsuri Script 言語処理系。Unity APIに非依存＝**単体テスト可能** |
| `Matsuri.Runtime` | `Scripts/` | `Matsuri.Script`, InputSystem, Cinemachine, AI.Navigation, HDRP, VFXGraph, TMP | ゲーム本体 |
| `Matsuri.Editor` | `Scripts/Editor/` | 上記全部 | シーン/SO/プレハブ/フォントの**自動生成ツール** |
| `Matsuri.Tests.EditMode` | `Tests/EditMode/` | `Matsuri.Script`, `Matsuri.Runtime` | Lexer/Parser/Validator/経済ロジックのテスト |

### Namespace

`Matsuri.Core` / `.Festival` / `.Economy` / `.Visitors` / `.Stalls` / `.Events` /
`.TimeSystem` / `.CameraSystem` / `.UI` / `.Save` / `.Art` / `.Audio` /
`Matsuri.Script.{Lexing,Parsing,Ast,Validation,Interpreting,Commands}` / `Matsuri.EditorTools`

> `Matsuri.Time` / `Matsuri.Camera` は `UnityEngine.Time` / `UnityEngine.Camera` と衝突するため
> `TimeSystem` / `CameraSystem` を使う。

---

## 7. データ構造

### 7.1 Matsuri Script パイプライン (§51)

```
Source (.matsuri)
  ↓ Lexer          … 日本語/英語トークン、IME全角記号も許容
Token[]
  ↓ Parser         … 再帰下降。エラーは行/列つきで収集し、続行する
FestivalProgram (AST §52)
  ↓ Validator      … 未知の屋台名、場所未設定、予算超過、範囲外座標を検出
Diagnostic[]       … §41「12行目 『場所』が設定されていません」形式の日本語メッセージ
  ↓ Interpreter
FestivalPlan
  ├─ ImmediateCommands : IFestivalCommand[]   … RUN時に即実行
  └─ Rules : TriggerRule[]                    … 祭り開催中に毎tick評価 (§14,§15,§16,§17)
  ↓
IFestivalCommandSink (= FestivalManager)      … §53 ParserはGameObjectに一切触れない
```

`IFestivalCommand` 実装：
`CreateStallCommand` / `CreateDecorationCommand` / `CreateFacilityCommand` /
`SetPriceCommand` / `StartFireworksCommand` / `StartBonOdoriCommand` / `StartTaikoCommand`

`TriggerRule` = `{ ICondition Condition; IFestivalCommand[] Body; bool Once; }`
`ICondition` = `TimeCondition(20:00)` / `MetricCondition(来場者数 > 500)` / `StallQueueCondition(たこ焼き.待ち人数 > 20)`

### 7.2 ScriptableObject (§48)

```
StallData          : ID, DisplayName, Aliases[], Category(食べ物/遊び),
                     BuildCost, DefaultPrice, MinPrice, MaxPrice,
                     ServiceTime, Capacity, BasePopularity,
                     SatisfactionValue, VisualRecipe, Prefab(任意)
FacilityData       : ID, DisplayName, Aliases[], BuildCost, Effect(満足度/休憩/導線)
DecorationData     : ID, DisplayName, Aliases[], BuildCost, AmbienceRadius, AmbienceValue
FestivalEventData  : ID, DisplayName, Cost, Duration, SatisfactionBurst, VfxRecipe
VisitorArchetype   : ID(子ども/高校生/カップル/家族/大人), 出現重み,
                     Money/Hunger/Fun/Energy/Patience/WalkSpeed の範囲,
                     PreferenceFood[], PreferenceGame[], FireworksInterest
BalanceConfig      : 初期予算, 開始/終了時刻, 時間倍率, 来場カーブ, 混雑係数,
                     価格弾力性, 満足度重み, スコア係数    ← §31「ハードコードしない」
```

すべて `Assets/ScriptableObjects/` にEditorスクリプトで**自動生成**する。

### 7.3 FestivalObject 階層 (§49)

```
FestivalObject (abstract MonoBehaviour)
├─ Stall           … 行列/接客/売上/人気度
├─ Decoration      … 周囲NPCの満足度ボーナス
├─ Facility        … ベンチ/ゴミ箱/トイレ/入口/出口/案内板
└─ EventObject     … 花火/盆踊り/太鼓
```

### 7.4 NPC (§26–§29)

`VisitorAgent` はMonoBehaviour一つに閉じ、状態は構造体 `VisitorState` に保持。
行動は `IVisitorBehaviour` のステートマシン
（`Entering → Browsing → MovingTo → Queueing → BeingServed → Enjoying → Leaving`）。
目的地選択は §29 のスコア式：

```
Score = Preference*w1 + Need*w2 - Distance*w3 - QueuePenalty*w4 - PricePenalty*w5 + Popularity*w6
```

`VisitorManager` が **時間分散更新 (§57)**：全NPCを N バケットに分け、毎フレーム1バケットのみ思考させる。
遠距離NPCは NavMeshAgent を切って簡易移動 + アニメ停止 (LOD)。

---

## 8. MVP実装順 (§84)

| # | 内容 | 完了条件 |
| --- | --- | --- |
| 01 | プロジェクト基盤（asmdef/フォルダ/パッケージ/コンパイルゲート） | batchmodeでコンパイル成功 |
| 02 | `GameManager` / `TimeManager` | 17:00→22:00が5分で進行しイベント発火 |
| 03 | `StallData` ほかSO + 自動生成 | 11屋台/6設備/7装飾のSOが生成される |
| 04 | `FestivalObject` 階層 | Stall/Decoration/Facility/EventObject が動く |
| 05 | Lexer | 日本語トークン列を生成、テスト通過 |
| 06 | Parser + Validator | ASTを生成、§41形式のエラーを返す、テスト通過 |
| 07 | `CreateStallCommand` ほかCommand群 | Interpreterがコマンド列を出す |
| 08 | `FestivalManager` | コマンドを受けて世界を変更 |
| 09 | 屋台配置 + 建設演出 (§39) | 光→せり上がり→電球点灯→営業開始 |
| 10 | `EconomyManager` | 予算減算・不足時は日本語エラー |
| 11 | `VisitorAgent` | NPCが生成され入場する |
| 12 | NavMesh移動 | ランタイムベイク後、屋台を避けて歩く |
| 13 | Queue (§30) | QueuePointに整列し順番待ち |
| 14 | Purchase | ServiceTime経過で購入成立 |
| 15 | Revenue | 売上・所持金・人気度が更新される |
| 16 | Festival Start / End | 22:00で客が帰る |
| 17 | Result (§36) | 結果画面が出る |
| 18 | UI (§9,§10,§42,§43,§64) | エディタ/HUD/結果/補完/エラー表示 |
| 19 | 高品質Prefab (§23,§24,§58) | 手続き生成の多パーツ屋台 + LOD + 差し替え口 |
| 20 | Lighting / VFX / Audio (§59〜§63) | 提灯・花火・祭囃子・湯気 |

MVP完成条件は §73 の一連の流れが通ること。
Phase 2〜5 (§74〜§77) は MVP 後に同ファイルへ追記して進行する。

---

## 9. テスト方法

| 層 | 手段 |
| --- | --- |
| コンパイル | `scratchpad/gate.sh` — 実プロジェクトをAPFSクローンしたサンドボックスで `Unity -batchmode -quit` を実行。**ユーザーが開いているEditorを一切邪魔しない** |
| 言語処理系 | EditMode テスト（`Matsuri.Tests.EditMode`）。字句・構文・検証・インタプリタを網羅 |
| 経済/スコア | EditMode テスト。純関数として切り出した `EconomyRules` / `ScoreRules` を検証 |
| NPC意思決定 | EditMode テスト。`DestinationScorer` を純関数として検証 |
| 統合 | Editorスクリプト `MatsuriSmokeTest` を batchmode `-executeMethod` で実行し、<br>「祭りを5分ぶん高速シミュレート → 売上>0 / 例外0」をログ判定 |
| 目視 | Play Mode。§80 の「開催の瞬間」を人が確認 |

---

## 10. 実装状況（実測）

MVP (§72 / §73) に加え、Phase 2〜5 (§74〜§77) の内容もほぼ入っている。

| 指標 | 実測値 |
| --- | --- |
| C# ファイル | 168 |
| ScriptableObject | 50（屋台11 / 設備10 / 装飾7 / イベント3 / NPC5 / 見た目11 ほか） |
| 生成プロンプト (§70) | 30 |
| EditMode テスト | 229 / 229 通過 |
| PlayMode テスト | 28 / 28 通過（+ 計測用の Explicit 5件） |
| 祭りの長さ (§7) | 実時間 **120秒**（17:00〜22:00 を 2.5分/秒 で圧縮） |

### §73 MVP完成条件

`Assets/Tests/PlayMode/FestivalIntegrationTests.cs` が §73 をそのままテスト化している。
RUN → 建つ → 開催 → NPC入場 → 行列 → 購入 → 売上 → 22:00 → 結果、および
`時間 20:00 { 花火 }` の発火と `もし 来場者数 > 5 { }` の増築まで自動で検証する。

### バランス実測（demo_6_honki、実時間2分、屋台6軒＋盆踊り場＋休憩所2）

```
18:00  来場300  売上 ¥60,750   販売161   踊り9   休憩1
19:00  来場318  売上 ¥191,600  販売514   踊り13  休憩3
20:00  来場395  売上 ¥308,700  販売829   踊り9   休憩5
21:00  来場466  売上 ¥430,000  販売1157  踊り11  休憩4
22:00  来場537  売上 ¥546,850  販売1472  最高同時300人  平均満足度96%
```

居場所（盆踊り場・休憩所）を入れると、客が疲れて早く帰らなくなるぶん
売上が ¥509,800 → ¥546,850 に伸びる。

### 性能実測 (§56)

```
NPC  300人 : 平均 494fps / 最低 166fps / p95 2.75ms / 管理メモリ 39.7MB
NPC 1000人 : 平均 411fps / 最低 142fps / p95 3.25ms / 管理メモリ 45.6MB
             思考バケット 12→25 に自動追従、NavMeshAgent は 220本に制限
```

**注意**: これはバッチモードでの CPU 側の数字。実ウィンドウでの HDRP 描画負荷は
含まれないため、実機の fps はこれより低い。1000人でもシミュレーションが
破綻しないことの確認として読むこと。

### 屋台の作り込み（§23 / §79）

`StallStructureTests` が11種すべてで §23 の子オブジェクト名の存在と
パーツ数・マテリアル数を検査している。

```
たこ焼き 100パーツ/46マテリアル    焼きそば 105/49    かき氷 101/54
りんご飴 95/46                     わたあめ 95/49     フランクフルト 95/48
金魚すくい 109/49                  射的 122/51        ヨーヨー釣り 108/51
スーパーボールすくい 128/52        型抜き 97/47
```

実光源は1軒につき1個に抑え、残りは Emission で表現している。

### 見つけて直した重大な不具合

| 症状 | 原因 | 対応 |
| --- | --- | --- |
| 地面が描画されず NavMesh が焼けない | `FaceNormal` が `Cross(c-a, b-a)` で Unity の表裏判定と逆。手続き生成の全面が裏返っていた | `Cross(b-a, c-a)` に修正 |
| 17:00 なのに空が真っ黒 | `GradientSky` の色は 0〜1 の値なので、物理ベースの固定露出下では黒に潰れる | 空の露出をカメラ露出に追従させた |
| 夕方でも夜のように暗い | `HDAdditionalLightData` を AddComponent しただけでは Directional の単位が Candela のまま | `SetIntensity(lux, LightUnit.Lux)` |
| NPC が屋台にたどり着けない | `PhysicsColliders` では実行時生成メッシュが収集されず NavMesh が空 | 地面のみ `RenderMeshes` で焼き、屋台は `NavMeshObstacle` でくり抜く |
| 誰も買わない | 同時接客数が1で、5時間でも1軒37人しか捌けなかった | ServiceTime (§30) は据え置き、同時接客数を実態に合わせた |
| **カメラが移動・回転しない** | 実装は正しく、コードエディターの TextField がキーボードフォーカスを握っていた | 文字入力中はキー操作を止め、会場クリックでフォーカス解放 |
| 中ボタン・ホイール前提の操作系 | ノートPCのトラックパッドには中ボタンが無く、右ドラッグもやりにくい | **トラックパッド＋キーボードだけで完結**する操作系へ。`Z`/`X` でのズーム、2本指スクロール（↕ズーム・↔回転）、1本指ドラッグで移動、`Option`+ドラッグで回転。ホイールの 120 とトラックパッドの連続値は `NormalizeScroll` で吸収 |
| **行番号がコード行とずれる** | 入力層・ハイライト層・ガター層で行高とパディングが一致していなかった | 3層を実測値で揃え、各行の実座標を突き合わせるテストを追加 |
| **盆踊り場に人が来ない** | `MovingToAmenity` / `Dancing` / `Praying` が毎フレームの状態分岐から漏れていた。立ち位置に着いても永久に「移動中」のままで枠だけ埋まっていた | 3状態を分岐に接続 |
| 居場所に入れない | 建物の NavMesh くり抜きが盆踊り場・休憩所にも掛かっていた | 滞在できる施設は掘らないようにした |
| 新しい施設が反映されない | カタログが `ScriptableObjects/` と `Resources/` に二重生成されていた | 出力先を Resources に統一 |
| 倒れかけの客が踊りに行く | 体力の欲求が線形で、盆踊り場の華やかさに負けていた | 体力切れを非線形（二乗）にした |
| 屋根が板に見える | 反り・棟木・垂木付きの屋根メッシュは実装済みだったが、ファクトリから一度も呼ばれていなかった | 屋根を Surface / Frame / Rafters / Stripes の4層で組み直した |

---

## 11. 未達項目（正直な記録）

| 項目 | 状況 | 到達方針 |
| --- | --- | --- |
| **AI生成3Dモデル (§5)** | **未達**。Meshy 等の Image-to-3D サービスへ接続する手段が本環境に無い | ①`Assets/ArtPrompts/*.md` に §70 準拠の生成プロンプトを30件コミット済み<br>②現状は手続き生成メッシュ（1軒95〜128パーツ、46〜54マテリアル）。暖簾・前掛け・看板の文字、店員、電球、背面の荷物まで作り込んである<br>③`StallData.Prefab` にモデルを差すだけで置き換わる。手順は `ART_PIPELINE.md` |
| **AI生成Texture** | 未達（同上） | 木目の年輪と節、和紙の繊維、布の織り目、日本語フォントの焼き込みを C# で生成。差し替え可能 |
| **音源アセット** | 未達 | `ProceduralAudioLibrary` が祭囃子・ざわめき・蝉・花火・調理音を C# 合成 |
| **キャラクターアニメーション** | クリップ無し | `ProceduralWalkAnimator` が Transform 直接制御で歩行・待機・見上げを行う |
| **VFX Graph の .vfx (§61)** | 未達 | `.vfx` はエディタGUIでの手作業が前提のため、同等の見た目を `ParticleSystem` のコード構築で実現 |
| **実機での 60fps 実測 (§56)** | 未達 | バッチモードでは CPU 側しか測れない。実ウィンドウでのプロファイリングが必要 |

### 既知の弱点

1. 夜の近景で、電球のすぐ横にある明るい面（見本ケース・前掛け）が白飛びすることがある。
   `LightingRig.ExposureBias` と `MatsuriMaterials.PrintedGlow` の強度で調整できる
2. 来場者総数が同時300人の上限に張り付き、§36 の例（2,341人）には届かない。
   滞在時間か上限の調整余地がある
3. 神社の利用者が少ない（収容8人・滞在6分）。参道の導線をもう少し引くと変わる
