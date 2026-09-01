# kakigori_stall — かき氷屋台

## Prompt
- A Japanese shaved ice stall, cool and bright against the warm festival night
- Pale blue and white awning roof with a scalloped wavy edge, canvas stretched taut over a light wooden frame
- Hand-cranked cast iron ice shaver bolted to the counter, chrome handle, a clear ice block clamped in the cradle
- Row of tall glass syrup bottles: red strawberry, green melon, yellow lemon, blue hawaii, brown coffee, each with a metal pour spout
- Stack of paper cone cups and colorful plastic spoons in a bamboo basket
- A traditional white flag banner with the single red character for ice, hanging from a side pole and rippling
- Small chest cooler under the counter, lid ajar, cold vapor spilling over the rim
- Condensation beads and thin frost on the shaver body and the ice block
- Light wood counter with a shallow drip tray and a damp cloth
- Cool white bulbs mixed with warm ones under the eaves so the ice reads as cold
- A ceramic bowl of finished shaved ice with red syrup on the counter edge

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 11,000 - 16,000 (LOD0)
- Texture: 2048x2048 PBR (BaseColor / Normal / MaskMap / Emissive)。氷は Transmission 用に別マテリアル
- Pivot: 接地面の中心
- Scale: W 3.0m x D 2.0m x H 2.5m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/kakigori_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/kakigori_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/Kakigori.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter` / `FoodProps` /
  `LightBulbs` / `SteamVFX`（冷気に読み替え） / `StaffPosition` / `CustomerPosition` /
  `QueuePoint01` 〜 `QueuePoint08` / `AudioSource`
