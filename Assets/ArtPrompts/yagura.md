# yagura — 櫓（盆踊り用）

## Prompt
- A bon-odori yagura tower standing at the centre of the festival ground, the tallest structure in the venue
- Square scaffold of heavy timber poles lashed with rope at every crossing, four legs splaying outward toward the base
- A railed platform at the top with a plank floor, reached by a steep ladder stair on one side with a rope handrail
- A single large taiko drum mounted on the platform under a small pitched canopy
- Red and white striped cloth wrapped around the platform railing, tied at intervals, hanging slightly unevenly
- Ropes radiating out and downward from the top corners like a maypole, strung with dozens of small paper lanterns in red, white and yellow
- Lantern strings sagging in shallow curves, each lantern lit and swaying independently
- Vertical banners hung on two faces of the tower with brush characters
- A ring of trodden earth around the base where dancers circle
- Rope lashings visible in detail, fibres frayed at the knots, timber darkened where the ropes bite
- Night, the tower reading as a glowing cone of lantern light with a dark timber skeleton inside

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 18,000 - 26,000 (LOD0)。提灯は 400 tris のものを Instancing で複製
- Texture: 4096x4096 PBR（木部・ロープ・布をアトラス化） + 1024x1024 Emissive（提灯）
- Pivot: 基礎の接地中心
- Scale: W 5.0m x D 5.0m x H 7.0m

## Where it goes
- 出力: `Assets/Art/Models/Events/yagura.fbx`
- Prefab: `Assets/Prefabs/Events/yagura.prefab`
- 差し込み先: `Assets/ScriptableObjects/Events/BonOdori.asset`（`FestivalEventData`）。
  現状 `FestivalEventData` に `Prefab` 欄が無いため、`ART_PIPELINE.md` の
  「イベントモデルの差し替え」に従い `FestivalEventData.Prefab` を追加して差す。
- 子オブジェクト名: `Body` / `Platform` / `Drum` / `LanternStrings`（`SwayAnimator` 対象） /
  `LightBulbs` / `Light` / `DancerCircle`（NPC が踊る円の半径基準となる空オブジェクト） /
  `PerformerPosition`（太鼓を叩く人の位置）
