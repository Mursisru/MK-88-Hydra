# MK-88 Hydra

[![Version](https://img.shields.io/badge/version-1.1.0-blue)](https://github.com/Mursisru)
[![BepInEx](https://img.shields.io/badge/BepInEx-5-green)](https://docs.bepinex.dev/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

BepInEx plugin that adds the **MK-88 Hydra** air-drop anti-ship torpedo to [Nuclear Option](https://store.steampowered.com/app/2654120/Nuclear_Option/).

> [!IMPORTANT]
> **Requires [Blueprinter](https://github.com/nikkorap/NOBlueprinter-Releases)** (`com.nikkorap.blueprinter`). Install `Blueprinter.dll` into `BepInEx/plugins/` before this mod.

## Features

- Glide (vanilla bomb physics) → shed cover → parachute → underwater sonar run
- Add-only loadout on Darkreach / Alkyon HE Piledriver pylons
- Encyclopedia identity: 5.65 m, 2350 kg launch, 450 kg HE, $3.9m
- Content bundle `MK88Hydra.nobp` (`TorpedoVisual`)

Json keys stay `missilepack_mk54_torpedo` / `MissilePack_MK54_Torpedo_single` for existing loadouts.

## Install

1. Install BepInEx 5 and Blueprinter.
2. Copy the `MK-88-Hydra/` folder into `BepInEx/plugins/MK-88-Hydra/`:
   - `MK88Hydra.dll`
   - `MK88Hydra.nobp`
   - `Textures/` and `KozuchTorpedoTexture.png` if present
3. Launch the game and select **MK-88 Hydra** on Piledriver HE pylons.

## Build

```powershell
dotnet build .\MK88Hydra\MK88Hydra.csproj -c Release
```

Release output auto-deploys to `BepInEx/plugins/MK-88-Hydra/`.

Unity bake (mesh `.nobp`):

```text
Open UnityBake/ in Unity 2022.3.62f3 → Hydra → Build Nobp Bundle
```

## Model source

Blender export (canonical): [`Models/RealtorpedoTransformMK54L.fbx`](Models/RealtorpedoTransformMK54L.fbx)  
Unity import copy: `UnityBake/Assets/MissilePack/RealtorpedoTransformMK54L.fbx`

## License

MIT — see [LICENSE](LICENSE).
