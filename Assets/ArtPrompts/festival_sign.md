# festival_sign — 夏祭り看板

## Prompt
- A large freestanding festival entrance sign board, the kind bolted together from timber for one night only
- Wide horizontal plank face made of five boards butted together, grain running horizontally, seams visible
- Painted cream base with a broad red border, the paint applied by hand and uneven at the corners
- Huge brush-written characters reading natsu-matsuri across the face, black sumi ink, confident strokes with visible bristle texture
- A smaller line of characters underneath giving the date and the hours, painted in blue
- Two stout angled legs at the back propping it up, cross-braced, sandbags weighting the feet
- Nail heads and two metal corner brackets, slightly rusted, streaking rust down the paint
- A short string of small bulbs tacked along the top edge, pointing down at the board
- A folded paper flyer stapled to the lower right corner, one edge lifting
- Splashes of mud along the bottom rail
- Night, the board lit from above by its own bulbs, everything behind it in darkness

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 2,500 - 4,000 (LOD0)
- Texture: 2048x1024 PBR。文字は BaseColor に焼き込み（差し替え用に別レイヤーも書き出す）
- Pivot: 脚の接地中心。+Z が看板の表
- Scale: W 4.0m x H 2.6m x D 1.0m

## Where it goes
- 出力: `Assets/Art/Models/Decorations/festival_sign.fbx`
- Prefab: `Assets/Prefabs/Decorations/festival_sign.prefab`
- 差し込み先: `Assets/ScriptableObjects/Decorations/FestivalSign.asset` の `DecorationData.Prefab`
- 子オブジェクト名: `Body` / `Legs` / `Sign`（文字面。差し替え時はこのマテリアルだけ交換する） /
  `LightBulbs` / `Light`
