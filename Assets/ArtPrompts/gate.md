# gate — 会場ゲート（入口・出口）

## Prompt
- A temporary festival entrance gate built from scaffolding pipe and cloth, spanning a walking path
- Two vertical steel pipe towers braced with diagonal clamps, joined overhead by a horizontal truss
- A wide cloth banner stretched across the truss, off-white with a red border, large characters announcing the festival entrance
- The banner tensioned with rope through brass eyelets, one corner rippling loose in the wind
- Bamboo and pine sakaki bundles lashed to both towers as a traditional greeting decoration
- Two large red paper lanterns hanging from the truss, one at each end, glowing
- A pair of low rope stanchions funnelling people toward the middle of the opening
- Steel pipes with scuffed silver galvanizing, orange clamp handles, a coil of spare rope hooked on one tower
- Sandbag ballast at each tower foot, printed with a rental company logo
- Trodden dirt path leading through the gate, footprints going in
- Night, warm light from the lanterns spilling on the ground under the gate

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 5,000 - 8,000 (LOD0)
- Texture: 2048x2048 PBR。幕の文字は差し替え用に「入口」「出口」2種のテクスチャを書き出す
- Pivot: 通路中央の接地点。くぐる方向は Z 軸
- Scale: W 5.0m x H 4.0m x D 1.0m

## Where it goes
- 出力: `Assets/Art/Models/Facilities/gate.fbx`
- Prefab: `Assets/Prefabs/Facilities/gate_entrance.prefab` / `gate_exit.prefab`（幕マテリアルのみ差し替え）
- 差し込み先: `Assets/ScriptableObjects/Facilities/Entrance.asset` と `Exit.asset` の `FacilityData.Prefab`
- 子オブジェクト名: `Body` / `Banner`（`SwayAnimator` 対象） / `Lantern_L` / `Lantern_R` /
  `Light` / `SpawnPoint`（`VisitorManager.EntrancePosition` / `ExitPosition` に使う基準点）
