# visitor_couple — 来場者：カップル

## Prompt
- Two Japanese young adults in their early twenties attending a festival together, presented as a matched pair on one reference sheet, each in T-pose front side and back
- Both in yukata chosen to complement each other: one deep navy with a white hemp-leaf pattern, one soft coral with small white hydrangea flowers
- Coordinated obi in the opposite colors, tied neatly, one in a drum knot at the back
- Matching wooden geta with black thongs, worn correctly, slightly stiff walking stance
- One carries a folding paper fan tucked into the back of the obi, the other a small woven kinchaku pouch on a wrist cord
- Hair worn up on one figure with a simple hairpin and a small flower ornament, short and neat on the other
- Slightly formal and self-conscious posture, both a little more dressed up than the crowd around them
- Same body proportions family as the other adult visitors so animation retargets cleanly
- Clean rig-friendly geometry, sleeves modelled with enough depth that arms do not clip
- Hands relaxed and empty, ready for props
- Even flat reference lighting, no baked shadows

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 7,500 - 10,000 (LOD0) x 2体 / 3,500 (LOD1) / 1,400 (LOD2)
- Texture: 2048x2048 PBR を2体で共有（片方ずつ UV 半分を使う）
- Pivot: 各体とも両足の中間の接地点。+Z が正面
- Scale: 身長 1.60m と 1.72m
- Rig: Unity Humanoid。2体とも同じボーン構成にする

## Where it goes
- 出力: `Assets/Art/Models/Visitors/visitor_couple_a.fbx` / `visitor_couple_b.fbx`
- Prefab: `Assets/Prefabs/Visitors/visitor_couple_a.prefab` / `visitor_couple_b.prefab`
- 差し込み先: `Assets/ScriptableObjects/Visitors/Couple.asset`（`VisitorArchetype`）。
  2体で1組として並んで歩かせる。`ART_PIPELINE.md`「NPCモデルの差し替え」を参照。
- 子オブジェクト名: `Body` / `Head` / `RightHandProp` / `LeftHandProp` / `NameTagAnchor` /
  `PartnerAnchor`（相方が並ぶ位置）
