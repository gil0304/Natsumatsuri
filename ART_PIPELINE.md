# ART_PIPELINE.md — MATSURI.exe アートパイプライン

仕様書 §5 / §70 / §71 に対応する制作フローと、
**現在の手続き生成アセットを AI 生成モデルへ差し替える具体的手順**をまとめる。

本作のアートは 2 層構造になっている。

| 層 | 実体 | 状態 |
| --- | --- | --- |
| PROC（手続き生成） | `Matsuri.Art.Procedural*Factory` が C# でメッシュ・マテリアル・テクスチャを生成 | **実装済み。今すぐ動く** |
| AI 生成モデル | `Assets/ArtPrompts/*.md` のプロンプトから作った FBX + PBR テクスチャ | 差し替え待ち |

`StallData.Prefab` などの `Prefab` 欄が **空なら PROC、入っていれば AI 生成モデル**が使われる。
つまり差し替えは「Prefab 欄にドラッグする」だけで完了する。コードの変更は不要。

---

## 1. 制作フロー (§5)

```
Assets/ArtPrompts/xxx.md
   ↓ ① 画像生成（プロンプトそのまま）
コンセプト画像 1〜4枚
   ↓ ② Image to 3D（Meshy / Tripo / Rodin 等）
生の3Dメッシュ + PBR Texture
   ↓ ② Remesh / Retopo
ゲーム用トポロジ（Target の Tris に収める）
   ↓ ③ Blender で修正（軸・スケール・ピボット・不要面の削除）
xxx.fbx
   ↓ ④ Assets/Art/Models/ にインポート
Unity上のModel Asset
   ↓ ⑤ Prefab化 + §23 の子オブジェクトを同名で用意
Assets/Prefabs/**/xxx.prefab
   ↓ ⑥ 対応する ScriptableObject の Prefab 欄に差す
手続き生成が自動的に使われなくなる
   ↓ ⑦ LodBuilder で LOD を付ける
完成
```

---

## 2. 手順の詳細

### ① プロンプトで画像生成

- `Assets/ArtPrompts/<asset>.md` の `## Prompt` をそのまま画像生成に渡す。
- `## Shared Style Tokens` の 6 語（§71）を**必ず末尾に足す**。
  これを省くとアセット間で画風が揃わず、祭り全体がちぐはぐになる。

```
<Prompt の箇条書きをカンマ区切りに> ,
Japanese Summer Festival, Stylized Realistic, Warm Lighting,
Wood Material, Traditional Japanese Details, Modern High Quality Game Asset
```

- 3/4 前方からのビューを 1 枚、正面・側面・背面を各 1 枚出す。
  Image to 3D は 3/4 ビューが最も安定する。
- 背景は黒か濃紺（`#0B0E1A`）。祭りは夜のゲームなので、
  白背景で作るとアルベドが明るくなりすぎて夜景に馴染まない。

### ② Image to 3D → PBR Texture → Remesh

- 生成サービス（Meshy 等）に 3/4 ビュー画像を入れ、Texture 付きでメッシュ化する。
- 出てくる生メッシュは 100k tris を超えることが多い。
  各 `.md` の `## Target` に書いた **Tris の上限まで Remesh** する。
- テクスチャは BaseColor / Normal / Metallic / Roughness / AO を書き出す。
  Unity(HDRP) では **MaskMap（R=Metallic, G=AO, B=Detail, A=Smoothness）** に統合する。
  Roughness → Smoothness は反転が必要（`Smoothness = 1 - Roughness`）。
- 発光部（電球・提灯の内側・炭火）は **Emissive を別テクスチャに分ける**。
  §59「夜は光源が主役」なので、ここを分けないと夜の絵が死ぬ。

### ③ Blender での修正

必ず確認する 4 点。

| 項目 | あるべき状態 | 直し方 |
| --- | --- | --- |
| 軸 | Unity は Y-up / 左手系。**+Z が正面**（屋台なら客が並ぶ側） | Blender で -Y forward, Z up にして FBX 書き出し |
| スケール | 1 unit = 1m。`## Target` の Scale に合わせる | `Object > Apply > Scale` を必ず実行 |
| ピボット | `## Target` の Pivot（多くは接地面の中心。提灯は吊り下げ点） | 3D カーソルを移動して `Set Origin to 3D Cursor` |
| 法線 | 外向き。裏返りなし | `Recalculate Outside`、布は両面表示にする |

追加で、

- 見えない面（地面に接する底面、密着する内側）は削除する。
- UV は 0〜1 に収める。ライトマップは使わない（全て動的ライト §59）ので UV2 は不要。
- 布（暖簾・のぼり・幕）は `SwayAnimator` で揺らすため、
  **縦横 6x14 程度に分割**し、頂点カラー R に「揺れやすさ」（固定端 0 / 自由端 1）を焼く。

### ④ Unity へのインポート

配置先。

```
Assets/Art/Models/Stalls/       屋台11種
Assets/Art/Models/Decorations/  提灯・のぼり・神社・鳥居・木・ライト・看板
Assets/Art/Models/Facilities/   ベンチ・ゴミ箱・トイレ・ゲート・案内板
Assets/Art/Models/Visitors/     NPC 5アーキタイプ
Assets/Art/Models/Events/       櫓・太鼓舞台
Assets/Art/Textures/            テクスチャ
```

Model Import Settings。

| 設定 | 値 | 理由 |
| --- | --- | --- |
| Scale Factor | 1 | Blender 側で Apply 済み |
| Convert Units | ON | |
| Read/Write Enabled | OFF | メモリ二重持ちを避ける（衝突判定に Mesh を読まない） |
| Mesh Compression | Medium | |
| Generate Colliders | OFF | Collider は Prefab 側で Box/Capsule を手で付ける |
| Materials > Location | Use External Materials | マテリアルを Assets/Art/Materials/ に出して手で調整する |
| Rig（NPCのみ） | Humanoid | Mixamo 等のアニメを後から流用できるようにする |
| Rig（NPC以外） | None | |

マテリアルは **HDRP/Lit** を使う。`Shader.Find("HDRP/Lit")` が null を返す環境では
`MatsuriMaterials` が `Standard` にフォールバックするが、
インポートしたモデルは手動で HDRP/Lit を割り当てること。

### ⑤ Prefab 化 —— §23 の子オブジェクト名を必ず同名で用意する

**ここが最重要。コードはこの名前で `transform.Find` する。名前が違うと機能が死ぬ。**

屋台 (`Stall`) の必須階層 (§23):

```
takoyaki_stall (root, Stall コンポーネント)
├─ MainStructure      柱・土台・側板
├─ Roof               屋根
├─ Noren              暖簾（SwayAnimator を付ける）
├─ Sign               看板
├─ Counter            カウンター天板
├─ FoodProps          商品・調理器具（種類ごとの Prop）
├─ LightBulbs         電球群。建設演出 (§39) で点灯する Emissive
├─ SteamVFX           湯気・煙・水しぶきの VisualEffect / ParticleSystem
├─ StaffPosition      店員が立つ位置（空 GameObject）
├─ CustomerPosition   接客中の客が立つ位置（空 GameObject）
├─ QueuePoint01       行列1人目
├─ QueuePoint02       行列2人目
│   …（StallVisualRecipe.QueuePointCount と同数まで）
└─ AudioSource        屋台の環境音 (§24)
```

守るべき規則。

- `QueuePointNN` は **2桁ゼロ埋め**（`QueuePoint01`）。1始まり、連番に穴を空けない。
- `QueuePoint01` が屋台に一番近い。以降 `StallVisualRecipe.QueueSpacing`（既定 0.9m）ずつ
  客の並ぶ方向（+Z）へ離す。
- `StaffPosition` / `CustomerPosition` / `QueuePointNN` は **空の GameObject**。
  メッシュを持たせない。回転は +Z が屋台を向く向きにする。
- `LightBulbs` は Emissive マテリアルを持つ Renderer をぶら下げる。
  建設演出が `Renderer.material` の `_EmissiveIntensity` を上げて点灯させる。
- `SteamVFX` は `StallData.HasSteam` が false の屋台でも空の GameObject として置いておく。

装飾・設備・イベントの子オブジェクト名は各 `.md` の `## Where it goes` に書いてある。
共通するものは次の通り。

| 名前 | 用途 |
| --- | --- |
| `Body` | 本体メッシュ。建設演出のせり上がり対象 |
| `Light` | `Light` + `HDAdditionalLightData`。`DecorationData.EmitsLight` で制御 |
| `LightBulbs` | 発光メッシュ |
| `SwayAnimator` を付ける対象 | 布・葉・提灯（`Cloth` / `Foliage` / `Noren` / `Banner`） |
| `UsePoint` / `SeatPointNN` | NPC が使う位置 |

Collider は Prefab 側で付ける。
屋台は `BoxCollider` を本体に 1 つ。NavMesh を塞ぐのはこの Collider なので、
**客が並ぶ側（+Z）にはみ出さない**サイズにする。

### ⑥ ScriptableObject の Prefab 欄に差す

| アセット種別 | 差し込み先 | 効果 |
| --- | --- | --- |
| 屋台 | `Assets/ScriptableObjects/Stalls/*.asset` の `StallData.Prefab` | `StallVisualRecipe` による手続き生成が使われなくなる |
| 装飾 | `Assets/ScriptableObjects/Decorations/*.asset` の `DecorationData.Prefab` | `ProceduralDecorationFactory` が使われなくなる |
| 設備 | `Assets/ScriptableObjects/Facilities/*.asset` の `FacilityData.Prefab` | `ProceduralFacilityFactory` が使われなくなる |

`Prefab` が入っていれば `Instantiate`、null なら手続き生成、という分岐なので、
**1 種類ずつ順に差し替えられる**。たこ焼きだけ AI モデル、他は PROC、という状態で普通に遊べる。

差し替え後の確認。

1. Play して `たこ焼きを 0, 0 に置く` だけのコードを RUN する。
2. 建設演出（光 → せり上がり → 電球点灯）が最後まで走るか。
3. NPC が `QueuePoint01` に正しく整列するか。ずれていたら子オブジェクトの位置を直す。
4. 22:00 まで回して売上が立つか。

### ⑦ LOD を付ける (§58)

```csharp
Matsuri.Art.LodBuilder.AddLod(prefabRoot, new[] { 0.5f, 0.22f, 0.06f });
```

- LOD0 = `## Target` の Tris、LOD1 = その 50%、LOD2 = 20% を目安に Remesh で作る。
- NPC は数が多いので LOD を必ず付ける（§56 で最終目標 1000 人）。
- 提灯・電球のような小物は LOD1 まででよい。
- LOD を付けたら Editor の Statistics で、屋台 20 軒 + NPC 300 人の状態で
  フレーム内バッチ数が跳ね上がっていないか確認する。

---

## 3. まだ `Prefab` 欄が無いアセットの差し替え

NPC (`VisitorArchetype`) とイベント (`FestivalEventData`) には、現時点で `Prefab` 欄が無い。
モデルが用意でき次第、次のようにして差し替える。

### NPCモデルの差し替え

1. `Assets/Scripts/Data/VisitorArchetype.cs` に 1 行足す。

   ```csharp
   [Tooltip("完成モデルの Prefab。null の場合は ProceduralVisitorFactory で手続き生成する。")]
   public GameObject Prefab;
   ```

2. `ProceduralVisitorFactory.Build` の先頭で、`a.Prefab != null` なら
   `Object.Instantiate(a.Prefab, parent)` して即 return する。
3. Humanoid Rig で取り込み、`ProceduralWalkAnimator` を外して `Animator` に差し替える。
   歩行速度は `VisitorAgent` 側が `Animator` の `Speed` パラメータに渡す。
4. 服の色は `VisitorArchetype.OutfitColors` から `MaterialPropertyBlock` で上書きする。
   **モデル側でアルベドに色を焼き込まない**（§79「NPCの見た目をばらけさせる」ため）。

### イベントモデルの差し替え

1. `Assets/Scripts/Data/FestivalEventData.cs` に `public GameObject Prefab;` を足す。
2. `EventManager.StartBonOdori` / `StartTaiko` が、`Prefab` があればそれを `Instantiate` する。
3. 櫓は `DancerCircle` の半径を見て NPC が円を描いて踊る。空オブジェクトを忘れない。

---

## 4. 命名規約

- ファイル名・GameObject 名は **snake_case の英語**（`takoyaki_stall`）。
  `MatsuriIds` の定数と一致させる。
- 子オブジェクト名だけは §23 の **PascalCase**（`QueuePoint01`）。コードが探す名前なので変更禁止。
- マテリアルは `M_<asset>_<part>`（`M_takoyaki_stall_roof`）。
- テクスチャは `T_<asset>_<part>_<type>`（`T_takoyaki_stall_roof_BC` / `_N` / `_MASK` / `_EM`）。

---

## 5. 差し替えチェックリスト

1 アセット差し替えるごとに、この 10 項目を確認する。

- [ ] 1 unit = 1m になっている（Blender で Apply Scale した）
- [ ] ピボットが `## Target` の指定どおり
- [ ] +Z が正面（屋台なら客の並ぶ側）
- [ ] Tris が `## Target` の上限以内
- [ ] マテリアルが HDRP/Lit
- [ ] Emissive が発光部にだけ入っている
- [ ] §23 の子オブジェクトが**全部**同じ名前で存在する
- [ ] `QueuePointNN` が 1 始まりの連番で、01 が屋台に一番近い
- [ ] Collider が客の通り道を塞いでいない
- [ ] `LodBuilder.AddLod` を通した

---

## 6. アセット一覧と対応プロンプト

| アセット | プロンプト | 差し込み先 |
| --- | --- | --- |
| たこ焼き | `ArtPrompts/takoyaki_stall.md` | `StallData(takoyaki).Prefab` |
| 焼きそば | `ArtPrompts/yakisoba_stall.md` | `StallData(yakisoba).Prefab` |
| かき氷 | `ArtPrompts/kakigori_stall.md` | `StallData(kakigori).Prefab` |
| りんご飴 | `ArtPrompts/ringo_ame_stall.md` | `StallData(ringo_ame).Prefab` |
| わたあめ | `ArtPrompts/wataame_stall.md` | `StallData(wataame).Prefab` |
| フランクフルト | `ArtPrompts/frankfurt_stall.md` | `StallData(frankfurt).Prefab` |
| 金魚すくい | `ArtPrompts/kingyosukui_stall.md` | `StallData(kingyosukui).Prefab` |
| 射的 | `ArtPrompts/shateki_stall.md` | `StallData(shateki).Prefab` |
| ヨーヨー釣り | `ArtPrompts/yoyo_tsuri_stall.md` | `StallData(yoyo_tsuri).Prefab` |
| スーパーボールすくい | `ArtPrompts/superball_stall.md` | `StallData(superball).Prefab` |
| 型抜き | `ArtPrompts/katanuki_stall.md` | `StallData(katanuki).Prefab` |
| 提灯 | `ArtPrompts/lantern.md` | `DecorationData(lantern).Prefab` |
| のぼり | `ArtPrompts/nobori.md` | `DecorationData(nobori).Prefab` |
| 神社 | `ArtPrompts/shrine.md` | `DecorationData(shrine).Prefab` |
| 鳥居 | `ArtPrompts/torii.md` | `DecorationData(torii).Prefab` |
| 木 | `ArtPrompts/tree.md` | `DecorationData(tree).Prefab` |
| 屋台用ライト | `ArtPrompts/stall_light.md` | `DecorationData(stall_light).Prefab` |
| 夏祭り看板 | `ArtPrompts/festival_sign.md` | `DecorationData(festival_sign).Prefab` |
| ベンチ | `ArtPrompts/bench.md` | `FacilityData(bench).Prefab` |
| ゴミ箱 | `ArtPrompts/trashcan.md` | `FacilityData(trashcan).Prefab` |
| トイレ | `ArtPrompts/toilet.md` | `FacilityData(toilet).Prefab` |
| ゲート | `ArtPrompts/gate.md` | `FacilityData(entrance/exit).Prefab` |
| 案内板 | `ArtPrompts/signboard.md` | `FacilityData(signboard).Prefab` |
| 子ども | `ArtPrompts/visitor_child.md` | `VisitorArchetype(child)` ※§3 の手順 |
| 高校生 | `ArtPrompts/visitor_highschool.md` | `VisitorArchetype(highschool)` ※§3 |
| カップル | `ArtPrompts/visitor_couple.md` | `VisitorArchetype(couple)` ※§3 |
| 家族連れ | `ArtPrompts/visitor_family.md` | `VisitorArchetype(family)` ※§3 |
| 大人 | `ArtPrompts/visitor_adult.md` | `VisitorArchetype(adult)` ※§3 |
| 櫓 | `ArtPrompts/yagura.md` | `FestivalEventData(bon_odori)` ※§3 |
| 太鼓の舞台 | `ArtPrompts/taiko_stage.md` | `FestivalEventData(taiko)` ※§3 |
