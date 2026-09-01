# kingyosukui_stall — 金魚すくい屋台

## Prompt
- A Japanese goldfish scooping game stall, built around a wide shallow water pool
- Large rectangular pool of clear water set low at the front, blue vinyl liner, water only ankle deep
- Dozens of small goldfish, red-and-white and solid orange, drifting in loose shoals, casting soft shadows on the liner floor
- Rippling water surface with caustics, light from the bulbs breaking into moving bright patterns on the pool bottom
- Rack of poi scoops with paper membranes stretched over red plastic rings, standing in a wooden holder
- Stack of round plastic bowls and a bundle of water-filled plastic bags with rubber bands, ready for winners
- Low timber frame with a shallow shed roof, deliberately open so players can kneel at the pool edge
- Indigo noren with a white goldfish silhouette and the word kingyo-sukui
- Small air pump with a clear hose bubbling at one corner of the pool
- Kneeling mats and a drain bucket beside the pool
- Warm bulbs strung low over the water so the whole pool glows in the dark

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 12,000 - 18,000 (LOD0)。金魚1匹 120 tris 程度で複製
- Texture: 2048x2048 PBR + 水面用 Shader Graph（法線スクロール + Caustics）
- Pivot: 接地面の中心。水面は Y=0.35m
- Scale: W 3.4m x D 2.6m x H 2.2m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/kingyosukui_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/kingyosukui_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/Kingyosukui.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter`（プール枠） /
  `FoodProps`（ポイ・器） / `LightBulbs` / `SteamVFX`（水しぶき） / `StaffPosition` /
  `CustomerPosition` / `QueuePoint01` 〜 `QueuePoint08` / `AudioSource`
