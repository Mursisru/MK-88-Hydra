# Changelog

## [1.0.0] - 2026-08-20

First public release of the standalone MK-88 Hydra mod for Nuclear Option.

### Added

- Air-drop anti-ship torpedo for Darkreach / Alkyon HE Piledriver slots
- Lifecycle: glide → cover shed → parachute → underwater sonar run (GSN)
- Combat HUD preview icon (`PreviewHydra.png`)
- Underwater stealth (RCS / radar / turret ignore while submerged)
- Dual mount (`MissilePack_MK54_Torpedo_double`) on Piledriver x2 hardpoints; single mount on every HE Piledriver slot
- AI employment: vanilla GlideBombing, release ≤1 km AGL at 22–32 km over clear swim water
- Blueprinter bundle `MK88Hydra.nobp` (`TorpedoVisual`)

### Removed

- Carrier hydroacoustic decoy traps and seduction mechanics

Json keys unchanged (`missilepack_mk54_torpedo` / `MissilePack_MK54_Torpedo_single`).

## [1.1.0] - 2026-08-17

### Changed

- Split out of MissilePack into a standalone MK-88 Hydra plugin (`com.mursisru.mk88hydra`)
- Plugin folder `BepInEx/plugins/MK-88-Hydra/`, bundle `MK88Hydra.nobp`
