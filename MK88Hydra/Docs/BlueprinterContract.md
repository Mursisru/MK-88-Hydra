# Blueprinter contract (MK-88 Hydra)

Source: official `com.nikkorap.blueprinter` 1.8.21 (NOBlueprinter-Releases). Not derived from third-party weapon addons.

## Load path

1. Blueprinter scans `BepInEx/plugins/**/*.nobp` and embedded `*.nobp` resources.
2. Each bundle must contain TextAsset named `patch_manifest` (JSON → `PatchManifest`).
3. On `Encyclopedia.AfterLoad`, Blueprinter runs patches then Ops.

## Manifest schema (`schemaVersion` 3)

```json
{
  "modName": "MK88Hydra",
  "schemaVersion": 3,
  "modVersion": "0.0.0",
  "Patches": [],
  "Ops": [
    {
      "opId": "OpAddToEncyclopedia",
      "payloadJson": "{ \"entries\": [ { \"locator\": \"MK54_MissileDefinition\", \"name\": \"MK54_MissileDefinition\", \"type\": \"MissileDefinition\" } ] }"
    },
    {
      "opId": "OpAddWeaponMountToWeaponManager",
      "payloadJson": "{ \"bundleAsset\": { \"locator\": \"MK54_WeaponMount\", \"name\": \"MK54_WeaponMount\", \"type\": \"WeaponMount\" }, \"weaponManagers\": [ { \"gameAsset\": { \"locator\": \"Darkreach\", \"name\": \"Darkreach\", \"type\": \"WeaponManager\" }, \"hardpointSetIndices\": [0,1,2,3] } ] }"
    }
  ],
  "Addressables": []
}
```

## Ops used

| opId | Payload | Effect |
|------|---------|--------|
| `OpAddToEncyclopedia` | `entries: AssetRef[]` | Adds `WeaponMount` / `UnitDefinition` (incl. `MissileDefinition`) from bundle into Encyclopedia + lookups |
| `OpAddWeaponMountToWeaponManager` | `bundleAsset` + `weaponManagers[]` | Appends mount to `HardpointSet.weaponOptions` |

`AssetRef`: `locator`, `name`, `type` (resolved via `Resources.FindObjectsOfTypeAll` for game assets, or `AssetBundle.LoadAsset` for bundle).

## Prefab hashes

Blueprinter assigns stable Mirage `NetworkIdentity.PrefabHash` for GameObjects inside `.nobp` before Ops.

## MissilePack strategy

- Ship `MissilePack.nobp` with mesh visual + `patch_manifest` (Ops may be empty until Unity bake produces typed SOs).
- Runtime bootstrap waits for `Blueprinter.Plugin.Instance.PatchingComplete`, then registers torpedo defs / Piledriver-slot inject with game API.
- When bake produces `MissileDefinition` / `WeaponMount` inside `.nobp`, Ops above take over registration; runtime skips duplicates by `jsonKey`.
