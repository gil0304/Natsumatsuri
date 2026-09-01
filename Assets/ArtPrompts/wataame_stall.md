# wataame_stall — わたあめ屋台

## Prompt
- A Japanese cotton candy stall, soft and pastel, aimed at children
- Round stainless steel spinning bowl set into the counter, chrome rim, sugar dust settled around the edge
- A half-formed cloud of pink cotton candy wound on a paper stick, wisps trailing from the bowl rim
- Overhead hanging line of finished cotton candy bags printed with cartoon character faces, clipped by clothespins, swaying
- Jars of colored sugar: white, pink, pale blue, arranged behind the machine with a scoop
- Pale yellow and white awning with rounded corners, cheerful and clean
- Short pastel pink noren with a fluffy cloud illustration and the word wataame
- Bundle of blank paper sticks standing in a tin can
- Sugar haze in the air around the machine, catching the light as fine sparkle
- Rounded wooden counter with softened edges, painted cream, child-height at the left end
- Warm bulbs plus one small pink accent bulb over the machine

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 10,000 - 15,000 (LOD0)。綿あめ本体は半透明カード + アルファで表現
- Texture: 2048x2048 PBR + 綿あめ用 1024x1024 アルファテクスチャ
- Pivot: 接地面の中心
- Scale: W 2.8m x D 1.8m x H 2.5m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/wataame_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/wataame_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/Wataame.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter` / `FoodProps` /
  `LightBulbs` / `SteamVFX`（砂糖の粉じん） / `StaffPosition` / `CustomerPosition` /
  `QueuePoint01` 〜 `QueuePoint06` / `AudioSource`
