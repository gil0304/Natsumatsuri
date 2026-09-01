# bench — ベンチ

## Prompt
- A simple outdoor wooden bench set out for festival visitors to rest on
- Four thick slats forming the seat with narrow gaps between them, two slats forming a low backrest
- Softwood with a warm honey tone, grain running along the slats, knots visible in two places
- Ends of the slats rounded and worn smooth, edges lighter where paint and dirt have rubbed away
- Black powder-coated steel frame legs with a cross brace, paint chipped to bare metal at the feet
- Bolt heads and washers where the slats meet the frame, one bolt slightly proud
- Faint ring stains on the seat from cold drinks and a small scorch mark from a sparkler
- A discarded paper fan tucked between two slats
- Dirt and grass caught around the feet, one leg sunk slightly into the ground so the bench sits a little uneven
- Warm festival light falling across the seat from one side, long shadow between the slats
- Empty, no people, so it can be populated by NPCs at runtime

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 1,200 - 2,000 (LOD0)
- Texture: 1024x1024 PBR (BaseColor / Normal / MaskMap)
- Pivot: 接地面の中心。+Z が座る人の正面
- Scale: W 1.8m x D 0.6m x H 0.85m

## Where it goes
- 出力: `Assets/Art/Models/Facilities/bench.fbx`
- Prefab: `Assets/Prefabs/Facilities/bench.prefab`
- 差し込み先: `Assets/ScriptableObjects/Facilities/Bench.asset` の `FacilityData.Prefab`
- 子オブジェクト名: `Body` / `SeatPoint01` 〜 `SeatPoint03`（NPC が座る位置。
  `FacilityData.Capacity` と数を合わせる）
