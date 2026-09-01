# tree — 木（ケヤキ）

## Prompt
- A mature Japanese zelkova tree standing at the edge of the festival ground
- Broad vase-shaped crown spreading wide at the top, trunk dividing into several main limbs low down
- Smooth grey-brown bark with pale flaking patches and darker lenticel speckles, rougher and buttressed at the root flare
- Dense summer foliage in layered clusters, leaves small and serrated, deep green with yellow-green new growth at the branch tips
- Gaps in the canopy so lantern light from below breaks through in shafts
- Leaves translucent where backlit, showing veins
- Exposed surface roots spreading into packed dirt, a few fallen leaves and seed clusters on the ground
- A single paper lantern hung on a low branch by a cord
- Cicada shells clinging to the bark at eye height
- Gentle wind, the outer leaf clusters displaced and blurred slightly
- Night, warm light from below, cool moonlight rim on the upper canopy

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 8,000 - 14,000 (LOD0)。葉はアルファカード（1枚 2 tris x 数百枚）
- Texture: 2048x2048 幹用 PBR + 1024x1024 葉アトラス（BaseColor / Normal / Alpha / Translucency）
- Pivot: 幹の接地中心
- Scale: 高さ 7.0m x 樹冠幅 6.0m

## Where it goes
- 出力: `Assets/Art/Models/Decorations/tree.fbx`
- Prefab: `Assets/Prefabs/Decorations/tree.prefab`
- 差し込み先: `Assets/ScriptableObjects/Decorations/Tree.asset` の `DecorationData.Prefab`
- 子オブジェクト名: `Trunk` / `Foliage`（`SwayAnimator` 対象。頂点カラー R に揺れ強度を焼く） /
  `Roots`
