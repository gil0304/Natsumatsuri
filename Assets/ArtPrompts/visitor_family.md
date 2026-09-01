# visitor_family — 来場者：家族連れ

## Prompt
- A Japanese parent attending a festival with a small child, reference sheet with both figures in T-pose front side and back
- The parent in their thirties, sturdier build, wearing a plain cotton yukata in muted brown with a simple check, sleeves rolled once
- A wide practical obi with a small towel tucked into it, and a cloth shopping bag hanging from one hand
- A folded stroller sunshade strapped across the back, and a water bottle in a mesh side pocket
- The child around four years old, very small, chubby, in a bright red jinbei with a fish print, wearing soft sandals
- The child has a paper mask pushed up on top of the head and a balloon cord tied to the wrist
- Parent posture slightly protective, one hand held low and open at child height so a hand-hold can be rigged
- Both share the same proportion family and rig as the other visitor archetypes for retargeting
- Practical, worn, everyday clothing rather than formal festival dress
- Clean joint geometry, sleeves and hems modelled with thickness
- Even flat reference lighting, no baked shadows

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 8,000 (親) + 5,500 (子) (LOD0) / 合計 4,000 (LOD1) / 1,600 (LOD2)
- Texture: 2048x2048 PBR を親子で共有
- Pivot: 各体とも両足の中間の接地点。+Z が正面
- Scale: 親 1.68m / 子 1.05m
- Rig: Unity Humanoid

## Where it goes
- 出力: `Assets/Art/Models/Visitors/visitor_family_parent.fbx` / `visitor_family_kid.fbx`
- Prefab: `Assets/Prefabs/Visitors/visitor_family_parent.prefab` / `visitor_family_kid.prefab`
- 差し込み先: `Assets/ScriptableObjects/Visitors/Family.asset`（`VisitorArchetype`）。
  `ART_PIPELINE.md`「NPCモデルの差し替え」を参照。
- 子オブジェクト名: `Body` / `Head` / `RightHandProp` / `LeftHandProp` / `NameTagAnchor` /
  `ChildHandAnchor`（子どもが手をつなぐ位置。親側に置く）
