# stall_light — 屋台用ライト（裸電球の連なり）

## Prompt
- A festival lighting fixture: a strand of bare incandescent bulbs on a black rubber cable, strung between two short poles
- Twelve clear glass bulbs spaced evenly, each screwed into a black bakelite socket with a small drip loop of cable below it
- Visible tungsten filaments inside the glass, coiled and glowing orange, slightly different brightness bulb to bulb
- Cable sagging in a natural catenary between the poles, with a second slack loop near one end
- One bulb has failed and sits dark and slightly smoked, adding believability
- Galvanized steel poles with guy wires and a cable tie bundle at the top
- Small metal reflector shades on three of the bulbs, tin, dented, painted white inside
- Dust and moth wings on the glass, one moth mid-flight near a bulb
- Strong warm emissive core with a soft halo, the poles and cable in near-silhouette
- Deep night background so the strand reads purely as light
- No lantern, no paper, this is a bare electric fixture

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 1,500 - 2,500 (LOD0)
- Texture: 1024x1024 PBR + 512x512 Emissive（電球のみ）
- Pivot: 支柱の接地点（左側の柱の足元）
- Scale: W 4.0m x H 2.8m

## Where it goes
- 出力: `Assets/Art/Models/Decorations/stall_light.fbx`
- Prefab: `Assets/Prefabs/Decorations/stall_light.prefab`
- 差し込み先: `Assets/ScriptableObjects/Decorations/StallLight.asset` の `DecorationData.Prefab`
- 子オブジェクト名: `Poles` / `Cable` / `LightBulbs`（電球の Mesh をまとめる。
  コードが Emissive の点灯・消灯を制御する） / `Light`（Light + HDAdditionalLightData）
