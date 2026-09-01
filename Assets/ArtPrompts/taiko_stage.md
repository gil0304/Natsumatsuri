# taiko_stage — 太鼓の舞台

## Prompt
- A low performance stage for taiko drumming, set at the side of the festival ground
- Raised plank platform about half a metre high on a stout timber frame, planks slightly gapped, front edge trimmed with a dark board
- Red and white striped cloth skirting tacked around the platform edge, hiding the frame, a little slack in the middle
- One large nagado-daiko drum on an angled wooden stand at centre stage, cowhide head with a ring of round tacks around the rim
- Two smaller shime-daiko on low stands flanking it, ropes tensioning the heads in a tight lattice
- A rack of thick wooden bachi sticks standing upright beside the main drum
- A short backdrop screen of dark cloth on a frame at the rear, with a large brush-painted circle
- Four simple stage lights on short stands at the corners, aimed inward, warm and slightly amber
- Coiled cable taped down along the stage edge and a small folded towel on the platform
- Drum bodies of dark lacquered wood with a deep grain, heads pale and slightly worn at the strike centre
- Night, the stage brightly lit and the surroundings dark, dust hanging in the light beams

## Shared Style Tokens
- Japanese Summer Festival
- Stylized Realistic
- Warm Lighting
- Wood Material
- Traditional Japanese Details
- Modern High Quality Game Asset

## Target
- Tris: 10,000 - 15,000 (LOD0)
- Texture: 2048x2048 PBR（太鼓の皮は別マテリアルで Smoothness を落とす）
- Pivot: 舞台の接地中心。+Z が客席側
- Scale: W 4.5m x D 3.0m x H 2.4m（背景幕込み）

## Where it goes
- 出力: `Assets/Art/Models/Events/taiko_stage.fbx`
- Prefab: `Assets/Prefabs/Events/taiko_stage.prefab`
- 差し込み先: `Assets/ScriptableObjects/Events/Taiko.asset`（`FestivalEventData`）。
  現状 `FestivalEventData` に `Prefab` 欄が無いため、`ART_PIPELINE.md` の
  「イベントモデルの差し替え」に従い `FestivalEventData.Prefab` を追加して差す。
- 子オブジェクト名: `Body` / `Stage` / `Drum_Main` / `Drum_L` / `Drum_R` / `Backdrop` /
  `LightBulbs` / `Light` / `PerformerPosition01` 〜 `PerformerPosition03` /
  `AudiencePoint01` 〜 `AudiencePoint08`（NPC が見物する位置）
