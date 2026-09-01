# superball_stall — スーパーボールすくい屋台

## Prompt
- A Japanese bouncy ball scooping stall, the most saturated and toy-like booth of the row
- Shallow oval tub of water crowded with sixty rubber superballs, sizes from marble to golf ball
- Balls in clashing candy colors, some solid, some with glitter flecks, some with a marbled swirl, a few translucent
- Balls jostling at the surface, half-submerged, refracted and slightly enlarged by the water
- Wooden holder of poi scoops with thin paper membranes, plus a few metal-mesh scoops for the easier round
- Clear plastic bags and small nets hanging on a rail for carrying winnings
- Low booth with an open front, painted plywood side panels in bright orange
- Sign board with a cartoon bouncing ball and the word super ball sukui, comic-style lettering
- Small step platform along the front so children can reach the water
- Cool white bulbs above the tub to make the ball colors pop, warm bulbs at the sides for festival mood
- Wet patches and stray balls on the ground near the tub

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 10,000 - 15,000 (LOD0)。ボールは 80 tris の球を GPU Instancing で複製
- Texture: 2048x2048 PBR + ボール用カラーバリエーションアトラス
- Pivot: 接地面の中心。水面は Y=0.30m
- Scale: W 3.0m x D 2.2m x H 2.2m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/superball_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/superball_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/SuperBall.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter`（水槽枠） /
  `FoodProps`（ボール群） / `LightBulbs` / `SteamVFX`（水しぶき） / `StaffPosition` /
  `CustomerPosition` / `QueuePoint01` 〜 `QueuePoint06` / `AudioSource`
