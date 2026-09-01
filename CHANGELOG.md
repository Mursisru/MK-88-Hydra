# Changelog

## [1.0.3] - 2026-09-01

### Changed

- **Blueprinter 2.0.1:** bootstrap waits `PatchRunner.ApplyAllOps` instead of removed `PatchingComplete`; build references `Blueprinter_2.0.1.dll`.

> [!IMPORTANT]
> Requires **[Blueprinter 2.0.1+](https://github.com/nikkorap/NOBlueprinter-Releases)**. Remove legacy `BepInEx/plugins/Blueprinter.dll` (1.8.x) if both are installed.

## [1.0.2] - 2026-08-27

### Fixed

- Hydra no longer intermittently becomes MK-65 Crosswim when both mods are installed: spawn Claim only on the Hydra `bomb_glide1` shell (sibling Pending tokens ignored); refuse Claim if a foreign owner tag is present

## [1.0.1] - 2026-08-27

### Fixed

- Internal weapon bay no longer adds Hydra RCS to the aircraft (shared mount still counts on external pylons) — closes [#1](https://github.com/Mursisru/MK-88-Hydra/issues/1)
- Intermittent drop as PAB-80 / `bomb_glide1`: stamp Hydra definition before Instantiation, rescue Claim on pending race, PersistentUnit kill-feed identity — closes [#2](https://github.com/Mursisru/MK-88-Hydra/issues/2)

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
