# MK-88 Hydra Unity Bake

1. Open this folder in Unity **2022.3.62f3** (same as Nuclear Option).
2. Wait for FBX import (`Assets/MissilePack/RealtorpedoTransformMK54L.fbx`).
3. Menu: **Hydra → Build Nobp Bundle**.
4. Output: `UnityBake/Build/MK88Hydra.nobp` (+ copy to `MK88Hydra/Resources/` and game plugins).

The `.nobp` is a Unity AssetBundle loaded by **Blueprinter**. It must contain TextAsset `patch_manifest`.
