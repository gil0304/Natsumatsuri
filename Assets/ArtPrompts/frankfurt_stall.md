# frankfurt_stall — フランクフルト屋台

## Prompt
- A Japanese festival stall grilling frankfurter sausages on sticks
- Narrow charcoal grill box on the counter, glowing embers visible through the grate slots
- Eight fat sausages on bamboo skewers laid across the grate, skin split and browned, fat glistening and dripping
- A slanted warming rack at the back holding already-grilled sausages under a heat lamp
- Mustard and ketchup dispensers with pump nozzles, a paper napkin box, a bucket of sticks
- Dark green canvas gable roof with a white painted stripe along the ridge, slightly grimy from smoke
- Wooden sign board with the price hand-brushed in black on a yellow field, screwed above the counter
- Thick chopping board and long tongs hanging from a hook on the post
- Smoke and heat shimmer rising off the charcoal, orange light bouncing up onto the sausages
- Sooty patina on the grill box and the nearest post
- Compact footprint, tight and functional, the smallest food booth in the row

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 9,000 - 14,000 (LOD0)
- Texture: 2048x2048 PBR。炭は Emissive 強め
- Pivot: 接地面の中心
- Scale: W 2.4m x D 1.8m x H 2.5m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/frankfurt_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/frankfurt_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/Frankfurt.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter` / `FoodProps` /
  `LightBulbs` / `SteamVFX` / `StaffPosition` / `CustomerPosition` /
  `QueuePoint01` 〜 `QueuePoint06` / `AudioSource`
