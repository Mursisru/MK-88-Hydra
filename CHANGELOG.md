# Changelog

## [Unreleased]

### Added

- Combat HUD preview icon (`PreviewHydra.png`, contour line-art)
- Underwater stealth: submerged torpedo drops radar track / turret acquisition
- Carrier hydroacoustic decoy traps (Annex/Hyperion 130, Dynamo/Argus 70, Shard 30, Cursor 50; 7% redirect each)
- Dual torpedo mount (`MissilePack_MK54_Torpedo_double`) on Piledriver x2 hardpoints

### Changed

- Warhead FX via `Mk54WarheadFx` (vanilla TBM shockwave path)

## [1.1.0] - 2026-08-17

### Changed

- Split out of MissilePack into a standalone MK-88 Hydra plugin (`com.mursisru.mk88hydra`)
- Plugin folder `BepInEx/plugins/MK-88-Hydra/`, bundle `MK88Hydra.nobp`

Json keys are unchanged (`missilepack_mk54_torpedo`).

## [1.0.0] - 2026-08-14

### Added

- MK-88 Hydra air-drop torpedo for Darkreach / Alkyon Piledriver HE slots
- Blueprinter `.nobp` visual bundle and encyclopedia registration
- Torpedo lifecycle: glide → cover shed → parachute → underwater swim
