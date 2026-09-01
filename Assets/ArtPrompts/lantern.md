# lantern — 提灯

## Prompt
- A traditional Japanese chochin paper lantern hanging from a short cord, lit from within
- Barrel-shaped body, widest at the middle, tapering to flat wooden caps at top and bottom
- Fine bamboo ribs spiralling under the paper, each rib raising a subtle ridge and casting a soft shadow line
- Washi paper skin, warm off-white, thin enough that the ribs and the inner glow read through it
- A single bold red character brushed on the front face, ink bleeding very slightly into the paper fibres
- Red painted band around the upper and lower caps, paint slightly chipped at the rim
- Small black metal hanging ring on top with a twisted cord
- Interior emissive glow, warm amber, brightest at the centre of the barrel and falling off toward the caps
- A few tiny insects circling in the light
- Faint wrinkles and one small crease in the paper so it does not read as plastic
- Night background, the lantern is the light source

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 1,200 - 2,000 (LOD0)
- Texture: 1024x1024 PBR (BaseColor / Normal / MaskMap) + 512x512 Emissive
- Pivot: 吊り下げ点（上端の金具）。ここを支点に揺れる
- Scale: 直径 0.34m x 高さ 0.52m

## Where it goes
- 出力: `Assets/Art/Models/Decorations/lantern.fbx`
- Prefab: `Assets/Prefabs/Decorations/lantern.prefab`
- 差し込み先: `Assets/ScriptableObjects/Decorations/Lantern.asset` の `DecorationData.Prefab`
- 子オブジェクト名: `Body`（Mesh。`SwayAnimator` を付ける対象） / `Light`（Light + HDAdditionalLightData。
  `DecorationData.EmitsLight` が true のときコードが点灯を制御する） / `Cord`
