# toilet — 仮設トイレ

## Prompt
- A row of two portable festival toilet cabins standing on a plywood base
- Tall narrow moulded plastic cabins, pale grey-blue, with vertical rib mouldings down the panels
- Doors with a small frosted vent panel at the top and a red and green occupancy indicator beside the handle, one showing occupied
- Roof panel translucent white so the cabins glow faintly from a small interior light
- Pictogram signs screwed to each door, one male one female, printed on scuffed white plastic
- Hand sanitizer bottle bracketed to the outer wall, a roll of paper towels in a plastic hood beside it
- Scuff marks around the door handles, a boot mark low on one door, dust film across the lower panels
- Grounding straps and a service hatch at the back with a warning label
- A short queue barrier of rope and two posts marking where people wait
- Small step and a rubber mat in front of each door
- Placed slightly away from the food stalls, lit by a single cool utility lamp on a pole rather than warm festival bulbs

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 3,000 - 5,000 (LOD0)
- Texture: 2048x2048 PBR (BaseColor / Normal / MaskMap) + 小さめ Emissive（室内灯）
- Pivot: 接地面の中心。+Z が扉の正面
- Scale: W 2.4m x D 1.3m x H 2.4m

## Where it goes
- 出力: `Assets/Art/Models/Facilities/toilet.fbx`
- Prefab: `Assets/Prefabs/Facilities/toilet.prefab`
- 差し込み先: `Assets/ScriptableObjects/Facilities/Toilet.asset` の `FacilityData.Prefab`
- 子オブジェクト名: `Body` / `Door_L` / `Door_R` / `Light` /
  `UsePoint01` 〜 `UsePoint02`（NPC が使う位置。`FacilityData.Capacity` と一致させる） /
  `QueuePoint01` 〜 `QueuePoint04`
