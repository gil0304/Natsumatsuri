# visitor_highschool — 来場者：高校生

## Prompt
- A Japanese high school student at a summer festival, full body, T-pose reference sheet front side and back
- Around sixteen, tall and lanky, narrow shoulders, long limbs, slightly slouched posture in the idle reference
- Wearing a modern yukata worn casually: pale grey with a thin vertical stripe, sleeves pushed back, hem a touch short
- Bright yellow obi tied in a quick simple knot at the back, deliberately imperfect
- White canvas sneakers instead of geta, a small crossbody sling bag with a phone pocket
- Wired earphones with one bud in, the cable running down inside the collar
- Dyed brown hair, longer fringe swept to one side, a couple of strands out of place
- Wristband and a simple ring, small personal details that read at close camera range
- Hands empty and relaxed, palms slightly turned in, ready for prop attachment
- Clean topology at the joints for humanoid rigging, no crossing geometry at the elbows or knees
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
- Texture: 2048x2048 PBR。浴衣・帯・髪をマスクで分離し実行時に色替え可能にする
- Pivot: 両足の中間の接地点。+Z が正面
- Scale: 身長 1.70m
- Rig: Unity Humanoid

## Where it goes
- 出力: `Assets/Art/Models/Visitors/visitor_highschool.fbx`
- Prefab: `Assets/Prefabs/Visitors/visitor_highschool.prefab`
- 差し込み先: `Assets/ScriptableObjects/Visitors/HighSchool.asset`（`VisitorArchetype`）。
  `ART_PIPELINE.md`「NPCモデルの差し替え」の手順で `ProceduralVisitorFactory` を置き換える。
- 子オブジェクト名: `Body` / `Head` / `RightHandProp` / `LeftHandProp` / `NameTagAnchor`
