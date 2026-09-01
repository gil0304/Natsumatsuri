# shateki_stall — 射的屋台

## Prompt
- A Japanese cork gun shooting gallery stall, deep booth with a lit prize wall at the back
- Tiered shelving at the rear, three levels, lined with prizes: cigarette-box-sized packages, candy boxes, small plush toys, a toy robot, a snow globe
- Prizes balanced deliberately on their edges so a cork hit will topple them
- Two cork rifles with dark wooden stocks resting on a padded rail at the front, muzzle end pointing into the booth
- Small tray of cork bullets and a tin cup of spares on the rail
- Red and white striped side curtains framing the shooting lane, cutting off the view from the side
- Netting stretched across the top of the lane to catch stray corks
- Bright warm spotlights clamped to the top rail, aimed at the prize wall so the prizes are the brightest thing in the scene
- Hand-lettered price board hanging from the top rail, black brush characters on cream paper
- Worn wooden rail with dents and finger-polished patches from years of use
- Sawdust and a fallen cork or two on the booth floor

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 14,000 - 20,000 (LOD0)。景品はモジュール化して使い回す
- Texture: 2048x2048 PBR + 景品用アトラス 1024x1024
- Pivot: 接地面の中心
- Scale: W 3.0m x D 3.0m x H 2.6m。+Z が客側（撃つ方向は -Z）

## Where it goes
- 出力: `Assets/Art/Models/Stalls/shateki_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/shateki_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/Shateki.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter`（撃つ台） /
  `FoodProps`（景品棚） / `LightBulbs` / `SteamVFX`（発射煙） / `StaffPosition` /
  `CustomerPosition` / `QueuePoint01` 〜 `QueuePoint06` / `AudioSource`
