# visitor_adult — 来場者：大人

## Prompt
- A Japanese adult office worker who stopped by the festival on the way home, full body, T-pose reference sheet front side and back
- Around forty, average build with a slight softness at the waist, tired but relaxed posture
- Wearing a short-sleeved white dress shirt with the collar open, tie loosened and pushed to one side, sleeves rolled to the elbow
- Dark grey suit trousers with a belt, creases behind the knees, plain black leather shoes dulled by dust
- A worn shoulder bag hanging low on one hip, strap darkened where it rubs
- A folded suit jacket carried over one forearm in the idle variant, and an empty-handed variant for prop attachment
- Short black hair with grey at the temples, a five o clock shadow, glasses with thin metal frames
- A can of beer would be held in the right hand at runtime, so the right hand is posed slightly cupped
- Clearly distinct from the yukata-wearing crowd, giving the festival a realistic mixed audience
- Clean rig-friendly topology, shirt modelled with thickness at the collar and cuffs
- Even flat reference lighting, no baked shadows

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 7,000 - 10,000 (LOD0) / 3,500 (LOD1) / 1,400 (LOD2)
- Texture: 2048x2048 PBR。シャツ・ズボン・鞄をマスクで分離して色替え可能にする
- Pivot: 両足の中間の接地点。+Z が正面
- Scale: 身長 1.72m
- Rig: Unity Humanoid

## Where it goes
- 出力: `Assets/Art/Models/Visitors/visitor_adult.fbx`
- Prefab: `Assets/Prefabs/Visitors/visitor_adult.prefab`
- 差し込み先: `Assets/ScriptableObjects/Visitors/Adult.asset`（`VisitorArchetype`）。
  `ART_PIPELINE.md`「NPCモデルの差し替え」を参照。
- 子オブジェクト名: `Body` / `Head` / `RightHandProp` / `LeftHandProp` / `NameTagAnchor`
