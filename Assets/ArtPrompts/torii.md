# torii — 鳥居

## Prompt
- A large vermilion torii gate marking the entrance to the festival ground, seen straight on
- Two round columns leaning very slightly inward, thicker at the base, set into carved stone footings
- Curved top lintel with upswept ends, a second straight tie beam below it, and a short vertical strut between them
- Deep vermilion lacquer, glossy on the upper surfaces, worn to matte and chalky where hands and weather reach
- Black lacquered caps on the column tops and black bands at the bases
- A small wooden name plaque mounted on the vertical strut with dark carved characters
- Paint chipped along the lower 40cm of the columns showing pale timber beneath
- Rain streaks running down from the lintel ends, dust settled in the top curve
- Two paper lanterns hung from the lower beam, one on each side, glowing warm
- A rope of shimenawa is NOT present, keep the silhouette clean
- Night, the vermilion catching warm lantern light and reading almost orange at the edges

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 4,000 - 7,000 (LOD0)
- Texture: 2048x2048 PBR (BaseColor / Normal / MaskMap)
- Pivot: 2本の柱の中間の接地点。くぐる方向は Z 軸
- Scale: W 5.0m x H 5.5m x D 0.8m

## Where it goes
- 出力: `Assets/Art/Models/Decorations/torii.fbx`
- Prefab: `Assets/Prefabs/Decorations/torii.prefab`
- 差し込み先: `Assets/ScriptableObjects/Decorations/Torii.asset` の `DecorationData.Prefab`
- 子オブジェクト名: `Body` / `Plaque` / `Lantern_L` / `Lantern_R` /
  `Light`（Light + HDAdditionalLightData。入口を照らす）
