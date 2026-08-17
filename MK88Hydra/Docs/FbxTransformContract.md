# FBX transform contract — RealtorpedoTransformMK54L

## Required roles (EN Blender names)

| Role | Purpose | Names |
|------|---------|-------|
| Attach pylon | Suspend / bay rail pivot | `PlaceOfRocketLock` |
| Attach parachute | Line socket + deploy arrow (`transform.forward`) | `PlaceOfSpawnParachute` |
| Parachute cover | Jettison box mesh | `ParachuteBox` |
| Prop CW | Clockwise prop | `RotorWing1` |
| Prop CCW | Counter-clockwise prop | `RotorWing2` |
| Kozuch | Jettison cover | `KozuchTorpedos` |
| Body | Main hull | `TorpedoMain` |
| Fins | Fold animation | `WingL`, `WingR` |

## Textures

- Overlay: `KozuchTorpedoTexture.png` (plugin folder + bake)
- Do **not** export Blender Scene Light/Camera into FBX (causes white bloom)

## Runtime

`TransformBinder.FindByAliases` + `VisualFit` (uniform scale to 3.9 m) + `VisualMaterials` (strip lights, paint albedo).
