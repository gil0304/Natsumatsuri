# ringo_ame_stall — りんご飴屋台

## Prompt
- A Japanese candy apple stall, the glossiest and most colorful booth in the lane
- Tiered wooden display rack on the counter, three shelves, each holding rows of candy apples on wooden sticks
- Candy apples with a mirror-glossy crimson sugar shell, highlights wrapping the curve, sticks pushed through paper collars
- A copper pot of molten red sugar syrup on a small burner at the back, syrup slowly bubbling, a thermometer clipped to the rim
- Cellophane wrappers and twist ties in a shallow box, a few apples already bagged
- Pink and white striped awning, narrow and tall, trimmed with a scalloped valance
- Short indigo noren with a hand-painted apple mark and the word ringo-ame
- Mirror-backed panel behind the rack so the candy apples reflect and multiply
- Small warm spotlights aimed down at the rack, making the sugar shells glow like glass beads
- Wooden counter with a sticky sugar sheen near the pot, a damp cloth folded at the corner
- Faint sweet steam curling off the syrup pot

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 10,000 - 15,000 (LOD0)。りんご飴は1個 200 tris 程度で複製
- Texture: 2048x2048 PBR。飴は Clear Coat 相当の高 Smoothness
- Pivot: 接地面の中心
- Scale: W 2.8m x D 1.8m x H 2.6m。+Z が客側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/ringo_ame_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/ringo_ame_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/RingoAme.asset` の `StallData.Prefab`
- 必須の子オブジェクト名: `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter` / `FoodProps` /
  `LightBulbs` / `SteamVFX` / `StaffPosition` / `CustomerPosition` /
  `QueuePoint01` 〜 `QueuePoint06` / `AudioSource`
