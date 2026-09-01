# takoyaki_stall — たこ焼き屋台

## Prompt
- A Japanese street food stall specialized in takoyaki, seen in three-quarter front view
- Timber frame booth with four square posts and diagonal braces, planks visibly nailed, slight weathering at the feet
- Deep red and white vertically striped canvas gable roof, canvas sagging a little between the ridge and the eaves
- Dark navy noren curtain hanging from the roof beam, split into three panels, white brush-written characters reading takoyaki
- Long wooden counter across the front, worn lacquer sheen, stacked paper trays and toothpick cups on the right end
- Two cast-iron takoyaki griddle plates set into the counter top, half-spheres of batter browning, one row already turned
- Squeeze bottles of sauce, a jar of green aonori, a shaker of katsuobushi and a bamboo brush arranged behind the griddle
- A row of small bare bulbs strung under the eaves, warm orange filaments, cables sagging naturally
- One red paper lantern tied at the front left post
- Steam rising from the griddle, faint oil sheen on the iron
- Night festival mood, the stall glowing from inside against darkness

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 12,000 - 18,000 (LOD0)
- Texture: 2048x2048 PBR (BaseColor / Normal / MaskMap(Metallic,AO,Detail,Smoothness) / Emissive for bulbs)
- Pivot: 接地面の中心。原点 (0,0,0) が地面
- Scale: W 3.2m x D 2.0m x H 2.6m（1 unit = 1m）。+Z が客の並ぶ側

## Where it goes
- 出力: `Assets/Art/Models/Stalls/takoyaki_stall.fbx`
- Prefab: `Assets/Prefabs/Stalls/takoyaki_stall.prefab`
- 差し込み先: `Assets/ScriptableObjects/Stalls/Takoyaki.asset` の `StallData.Prefab`
- 必須の子オブジェクト名 (§23。コードがこの名前で探すため必ず同名で用意する):
  `MainStructure` / `Roof` / `Noren` / `Sign` / `Counter` / `FoodProps` /
  `LightBulbs` / `SteamVFX` / `StaffPosition` / `CustomerPosition` /
  `QueuePoint01` 〜 `QueuePoint08` / `AudioSource`
