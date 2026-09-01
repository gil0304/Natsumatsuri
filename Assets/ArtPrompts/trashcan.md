# trashcan — ゴミ箱

## Prompt
- A pair of festival waste bins standing side by side on a shared metal frame
- Two open-topped galvanized steel drums, one with a blue plastic collar for burnables, one green for cans and bottles
- Large hand-lettered category signs taped to the front of each drum, characters written thick with marker on white card, one sign peeling at a corner
- Clear plastic bin liners folded over the rims and held with bungee cords, liners bulging and half full
- Crushed paper trays, a bent skewer, an empty cup and a folded flyer visible at the top of the burnables bin
- Galvanized surface with a spangled zinc pattern, dented near the base, dull scratches around the rim
- Rust blooming at the bottom seam and around one dent
- A small stack of spare liners tied to the frame leg
- Damp patch on the ground beside the bins with a couple of stray wrappers
- Lit from one side by nearby stall bulbs, the interior of the bins in deep shadow
- Functional and slightly untidy, not pristine

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 2,000 - 3,000 (LOD0)
- Texture: 1024x1024 PBR (BaseColor / Normal / MaskMap)
- Pivot: 接地面の中心。+Z が投入口の正面
- Scale: W 1.2m x D 0.6m x H 1.0m

## Where it goes
- 出力: `Assets/Art/Models/Facilities/trashcan.fbx`
- Prefab: `Assets/Prefabs/Facilities/trashcan.prefab`
- 差し込み先: `Assets/ScriptableObjects/Facilities/TrashCan.asset` の `FacilityData.Prefab`
- 子オブジェクト名: `Body` / `Signs` / `UsePoint`（NPC が立ってゴミを捨てる位置）
