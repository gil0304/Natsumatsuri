# yoyo_tsuri_stall — ヨーヨー釣り屋台

## Prompt
- A Japanese water balloon yo-yo fishing stall, low and playful, aimed at small children
- Wide round inflatable tub of water at the front, edge padded and glossy, water shallow
- Forty small water balloons floating in the tub, translucent rubber in red, yellow, green, blue and pink, each with a painted swirl pattern
- Every balloon tied to a short rubber string with a loop, loops bobbing just above the water
- Bucket of paper twine hooks with a small wire clasp, standing on a stool beside the tub
- Reflections and refraction through the balloon skins, the water tinted by their colors
- Low timber railing around three sides to keep children from falling in
- Bright multicolor awning in wide vertical bands, cheerful and slightly faded
- Cloth banner with a bouncing yo-yo illustration and the word yo-yo tsuri
- Tiny plastic stools set out in a row for kneeling players
- Warm bulbs hung low, colored highlights scattered across the wet floor

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 10,000 - 15,000 (LOD0)。ヨーヨー1個 150 tris
- Texture: 2048x2048 PBR。風船は薄い Transmission 設定
- Pivot: 接地面の中心。水面は Y=0.30m
- Scale: W 3.0m x D 2.4m x H 2.2m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/yoyo_tsuri_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/yoyo_tsuri_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/YoyoTsuri.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter`（水槽枠） /
  `FoodProps`（ヨーヨー群） / `LightBulbs` / `SteamVFX`（水しぶき） / `StaffPosition` /
  `CustomerPosition` / `QueuePoint01` 〜 `QueuePoint06` / `AudioSource`
