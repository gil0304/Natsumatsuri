# katanuki_stall — 型抜き屋台

## Prompt
- A Japanese katanuki stall where players carve a shape out of a brittle candy plate
- Long low table instead of a tall counter, players sit on benches along it and lean in close
- Flat pale-amber candy plates laid out in a grid on the table, each stamped with an outline: a star, a bird, an umbrella, a bell, a fish
- Fine steel picking needles set in short wooden handles, resting in a shallow tray
- A magnifying lamp on a jointed arm clamped to the table edge, its lens catching a highlight
- Crumbs and broken candy fragments swept into a small pile at the table corner, a few failed plates cracked in half
- Prize board mounted at the back listing payouts by shape difficulty, hand-brushed characters on wood
- Simple shed roof of woven bamboo matting, low and close over the table for a focused, intimate feel
- Short brown noren with the word katanuki in plain black brushwork
- Directional warm lamps aimed straight down at the table so the candy plates glow translucent from behind
- Quiet concentrated atmosphere, dark surroundings, only the tabletop lit

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 9,000 - 14,000 (LOD0)
- Texture: 2048x2048 PBR。飴板は Transmission を持つ別マテリアル
- Pivot: 接地面の中心
- Scale: W 3.0m x D 2.0m x H 2.1m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/katanuki_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/katanuki_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/Katanuki.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter`（作業机） /
  `FoodProps`（型抜き板・針） / `LightBulbs` / `SteamVFX` / `StaffPosition` /
  `CustomerPosition` / `QueuePoint01` 〜 `QueuePoint06` / `AudioSource`
