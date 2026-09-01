# nobori — のぼり旗

## Prompt
- A tall Japanese nobori banner on a bamboo pole, planted in the ground beside a festival lane
- Narrow vertical cloth, much taller than wide, attached to the pole by cloth loops along the left edge and the top
- Deep red field with a wide white border stripe down the outer edge
- Large black brush characters running top to bottom, ink thick at the stroke starts and dry-brushed at the ends
- Cloth caught mid-wave, an S-curve running down the fabric, one corner curling forward
- Weave visible up close, edges hemmed with a slightly darker thread, hem fraying a little at the bottom corner
- Split bamboo pole with visible nodes, natural yellow-green fading to grey at the base
- Simple black iron cross-arm at the top and a weighted metal foot stand at the bottom
- Dust and mud splash on the lowest 20cm of both cloth and pole
- Backlit by a lantern behind it so the characters read as silhouettes through the cloth
- Night festival, warm rim light along the waving edge

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 900 - 1,600 (LOD0)。布は 6x14 の格子メッシュで揺れに耐える分割を持たせる
- Texture: 1024x2048 PBR (BaseColor / Normal / MaskMap)。布は両面表示
- Pivot: 支柱の接地点
- Scale: W 0.6m x H 3.0m（ポール込み）

## Where it goes
- 出力: `Assets/Art/Models/Decorations/nobori.fbx`
- Prefab: `Assets/Prefabs/Decorations/nobori.prefab`
- 差し込み先: `Assets/ScriptableObjects/Decorations/Nobori.asset` の `DecorationData.Prefab`
- 子オブジェクト名: `Pole` / `Cloth`（`SwayAnimator` を付ける対象。頂点カラーの R に
  「揺れやすさ」を焼き込み、ポール側を 0、外端を 1 にする） / `Base`
