# visitor_child — 来場者：子ども

## Prompt
- A Japanese elementary school child at a summer festival, full body, T-pose reference sheet with front side and back views
- Around eight years old, short and round-limbed, head large relative to the body, soft rounded shoulders
- Wearing a light cotton jinbei set, top and shorts, indigo blue with a small white goldfish pattern, sleeves a little too long
- Simple cloth tie belt knotted at the side, one end hanging loose
- Bare feet in wooden geta sandals a size too big, red cloth thongs
- Small drawstring pouch on a cord across the chest holding coins
- Short black hair, slightly messy, a cowlick at the crown, round cheeks, small nose
- Holding nothing in the reference sheet, hands open, so props can be attached at runtime
- Clean seams for rigging: neck, shoulders, elbows, wrists, hips, knees, ankles clearly defined
- Neutral face with a faint natural expression, eyes forward, not smiling hard
- Even flat lighting for the reference sheet, no strong shadows baked into the texture

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 6,000 - 9,000 (LOD0) / 3,000 (LOD1) / 1,200 (LOD2)
- Texture: 2048x2048 PBR (BaseColor / Normal / MaskMap)。服の色はマスクで塗り分け、
  `VisitorArchetype.OutfitColors` から実行時に着色できるようにする
- Pivot: 両足の中間の接地点。+Z が正面
- Scale: 身長 1.25m（`VisitorArchetype.BodyHeight` で 0.9〜1.1 倍される）
- Rig: Unity Humanoid。ボーン数 24 以下

## Where it goes
- 出力: `Assets/Art/Models/Visitors/visitor_child.fbx`
- Prefab: `Assets/Prefabs/Visitors/visitor_child.prefab`
- 差し込み先: `Assets/ScriptableObjects/Visitors/Child.asset`（`VisitorArchetype`）に対応。
  現状 `VisitorArchetype` に `Prefab` 欄が無いため、`ART_PIPELINE.md` の
  「NPCモデルの差し替え」に従い `VisitorArchetype.Prefab` を追加して差す。
  差した瞬間に `ProceduralVisitorFactory` の手続き生成は使われなくなる。
- 子オブジェクト名: `Body` / `Head` / `RightHandProp`（買った物を持たせる位置） /
  `LeftHandProp` / `NameTagAnchor`
