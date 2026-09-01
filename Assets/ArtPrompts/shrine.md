# shrine — 小さな神社（社殿）

## Prompt
- A small wooden Shinto shrine building at the far end of a festival ground, the visual anchor of the venue
- Raised on a stone platform with three worn steps, moss in the joints between the stones
- Dark stained cypress pillars and a deep overhanging hip-and-gable roof of grey layered tiles
- Ornamental crossed chigi finials on the ridge and short katsuogi logs laid across it
- Thick shimenawa straw rope hung across the front with white folded shide paper strips
- A large brass suzu bell above the offering box, with a striped rope hanging down from it
- Wooden offering box with vertical slats across the top, coins visible in the gaps
- Pair of stone komainu guardian statues at the foot of the steps, one mouth open one closed, weathered and lichen-spotted
- Two stone lanterns flanking the path, small flames inside
- Vermilion accents on the beam ends contrasting with the dark timber
- Night, lit warmly from the front by the festival lights, dark forest behind

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 20,000 - 30,000 (LOD0)。会場のランドマークなので他より予算を多く取る
- Texture: 4096x4096 PBR（屋根瓦・木部・石を1枚にアトラス化）
- Pivot: 石段を含む基礎の接地中心。+Z が参道側（正面）
- Scale: W 6.0m x D 5.0m x H 5.5m

## Where it goes
- 出力: `Assets/Art/Models/Decorations/shrine.fbx`
- Prefab: `Assets/Prefabs/Decorations/shrine.prefab`
- 差し込み先: `Assets/ScriptableObjects/Decorations/Shrine.asset` の `DecorationData.Prefab`
- 子オブジェクト名: `Body` / `Roof` / `Steps` / `Shimenawa`（`SwayAnimator` 対象） /
  `Light`（Light + HDAdditionalLightData。参道を照らす暖色） / `Komainu_L` / `Komainu_R`
