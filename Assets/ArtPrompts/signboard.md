# signboard — 案内板

## Prompt
- A festival information board on a single post, the kind that tells visitors which way each stall row goes
- Rectangular board of pale plywood in a dark stained frame, mounted at reading height and tilted slightly back
- A hand-drawn site map pinned to the board: rough rectangles for stalls, a red circle marking you-are-here, arrows in blue marker
- Three wooden direction arms bolted to the post below the board, pointing different ways, each brush-lettered with a destination
- Drawing pins, a couple of empty pin holes, and one curled notice about lost children taped at the lower corner
- Square post with a chamfered top, planted in a small concrete collar, grain raised and grey from weather
- A small shelf at the base holding a stack of paper programmes weighted by a stone
- One clip-on lamp on a flexible neck aimed at the map, casting an oval of warm light on the paper
- Pencil scribbles and a smudged fingerprint on the map corner
- Fine dust and a few insects gathered around the lamp
- Night, the board bright, the surroundings dark

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 2,000 - 3,200 (LOD0)
- Texture: 2048x1024 PBR。地図面はゲーム内で書き換えられるよう別マテリアルに分ける
- Pivot: 支柱の接地点。+Z が案内面の正面
- Scale: W 1.4m x D 0.4m x H 2.0m

## Where it goes
- 出力: `Assets/Art/Models/Facilities/signboard.fbx`
- Prefab: `Assets/Prefabs/Facilities/signboard.prefab`
- 差し込み先: `Assets/ScriptableObjects/Facilities/SignBoard.asset` の `FacilityData.Prefab`
- 子オブジェクト名: `Post` / `Board` / `MapSurface`（動的テクスチャ差し替え用） /
  `Arrows` / `Light` / `UsePoint`（NPC が立ち止まって見る位置）
