# yakisoba_stall — 焼きそば屋台

## Prompt
- A Japanese festival stall for yakisoba fried noodles, three-quarter front view
- Wider and shallower booth than a standard stall, single long teppan griddle dominating the counter
- Large flat steel griddle, blackened and oil-glazed, a mound of noodles pushed to one side with two metal spatulas resting on it
- Cabbage shreds, pork slices and bean sprouts piled in stainless trays behind the griddle
- Squeeze bottle of thick brown sauce, red pickled ginger in a small tub, a stack of white plastic trays with rubber bands
- Corrugated steel shed roof sloping to the back, single pitch, edges rusted slightly, supported by round steel poles
- Off-white noren with a bold red character panel reading yakisoba, wind-lifted at one corner
- A propane cylinder and a bundle of hose tucked under the counter, partially hidden by a hanging cloth skirt
- A line of clear bulbs above the griddle, casting hot highlights on the steel
- Heavy steam and a faint smoke haze over the griddle surface
- Warm night lighting, grease and steel catching the light

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 12,000 - 18,000 (LOD0)
- Texture: 2048x2048 PBR (BaseColor / Normal / MaskMap / Emissive)
- Pivot: 接地面の中心
- Scale: W 3.6m x D 2.0m x H 2.6m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/yakisoba_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/yakisoba_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/Yakisoba.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter` / `FoodProps` /
  `LightBulbs` / `SteamVFX` / `StaffPosition` / `CustomerPosition` /
  `QueuePoint01` 〜 `QueuePoint08` / `AudioSource`
