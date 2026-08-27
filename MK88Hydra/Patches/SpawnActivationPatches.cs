using HarmonyLib;
using Hydra.Blueprinter;
using Hydra.Bootstrap;
using Hydra.Runtime;
using UnityEngine;

namespace Hydra.Patches
{
    /// <summary>
    /// Template briefly activated so Instantiate yields an active child with Weapons registered.
    /// Mount prefab is local (no NetworkIdentity) — unlike the fly prefab.
    /// </summary>
    [HarmonyPatch(typeof(Hardpoint), nameof(Hardpoint.SpawnMount))]
    internal static class HardpointSpawnMountPatch
    {
        private static void Prefix(WeaponMount weaponMount)
        {
            if (!IsOurs(weaponMount) || weaponMount.prefab == null)
                return;

            // Keep shared WeaponInfo so multi-pylon loads stack into one station.
            WeaponInfo? shared = TorpedoBootstrap.TorpedoInfo ?? weaponMount.info;
            if (shared != null)
            {
                weaponMount.info = shared;
                weaponMount.sortWeapons = true;
                foreach (MountedMissile mm in weaponMount.prefab.GetComponentsInChildren<MountedMissile>(true))
                {
                    if (mm != null)
                        mm.info = shared;
                }
            }

            PrefabFactory.FreezeTemplatePhysics(weaponMount.prefab);
            weaponMount.prefab.SetActive(true);
        }

        private static void Postfix(Hardpoint __instance, WeaponMount weaponMount, GameObject __result)
        {
            if (!IsOurs(weaponMount))
                return;

            if (weaponMount.prefab != null)
            {
                PrefabFactory.FreezeTemplatePhysics(weaponMount.prefab);
                weaponMount.prefab.SetActive(false);
            }

            if (__result == null)
                return;

            bool internalBay = __instance != null &&
                               __instance.bayDoors != null &&
                               __instance.bayDoors.Length > 0;
            PrefabFactory.ActivateMountedInstance(__result, internalBay);
            int doorN = (__instance != null && __instance.bayDoors != null) ? __instance.bayDoors.Length : 0;
            HydraPlugin.ModLog?.LogInfo($"MK54 SpawnMount bay={internalBay} doors={doorN}");
        }

        private static bool IsOurs(WeaponMount? weaponMount)
        {
            return weaponMount != null &&
                   (string.Equals(weaponMount.jsonKey, TorpedoConstants.MountJsonKey, System.StringComparison.Ordinal) ||
                    string.Equals(weaponMount.jsonKey, TorpedoConstants.MountJsonKeyDouble, System.StringComparison.Ordinal));
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile), new[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit) })]
    internal static class SpawnerSpawnMissileGoPatch
    {
        private static void Prefix(GameObject missile, out bool __state)
        {
            if (Mk54SpawnGate.IsOurFlyPrefab(missile) &&
                (Mk54SpawnGate.Pending > 0 || Mk54SpawnGate.HasRecentFire()))
                Mk54SpawnGate.BeginPrefabStamp(missile);
            __state = Mk54SpawnGate.TryBegin();
        }

        private static void Postfix(bool __state, GameObject missile, Unit target, Missile __result)
        {
            try
            {
                Mk54SpawnGate.EndPrefabStamp();
                if (__result == null)
                    return;

                bool rescue = !__state && Mk54SpawnGate.ShouldRescueClaim(missile);
                if (!__state && !rescue)
                    return;

                if (rescue)
                    HydraPlugin.ModLog?.LogWarning(
                        $"MK54 rescue Claim on '{__result.name}' (bomb_glide1 shell, pending race)");

                // Capture Fire target BEFORE Claim clears SyncVar for HUD/seeker.
                Mk54SpawnGate.Claim(__result, target);
                Mk54SpawnGate.FinishVisual(__result);

                Rigidbody? rb = __result.rb != null ? __result.rb : __result.GetComponent<Rigidbody>();
                Mk54FireLock? fireLock = __result.GetComponent<Mk54FireLock>();
                HydraPlugin.ModLog?.LogInfo(
                    $"SpawnMissile OK '{__result.name}' rescue={rescue} fire={(fireLock != null ? fireLock.DebugName : "none")} pos={__result.transform.position} rb={(rb != null ? $"kin={rb.isKinematic} g={rb.useGravity} v={rb.velocity}" : "NULL")}");
            }
            finally
            {
                Mk54SpawnGate.EndPrefabStamp();
                if (__state)
                    Mk54SpawnGate.End();
            }
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile), new[] { typeof(MissileDefinition), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit) })]
    internal static class SpawnerSpawnMissileDefPatch
    {
        private static void Prefix(MissileDefinition missile, out bool __state)
        {
            if (missile == null)
            {
                __state = false;
                return;
            }
            __state = string.Equals(missile.jsonKey, TorpedoConstants.MissileJsonKey, System.StringComparison.Ordinal);
            if (!__state)
                return;
            Mk54SpawnGate.InFlight = true;
            Mk54SpawnGate.BeginPrefabStamp(missile.unitPrefab);
        }

        private static void Postfix(bool __state, Unit target, Missile __result)
        {
            try
            {
                Mk54SpawnGate.EndPrefabStamp();
                if (!__state || __result == null)
                    return;
                Mk54SpawnGate.Claim(__result, target);
                Mk54SpawnGate.FinishVisual(__result);
            }
            finally
            {
                Mk54SpawnGate.EndPrefabStamp();
                if (__state)
                    Mk54SpawnGate.End();
            }
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissileEncyclopedia))]
    internal static class SpawnerSpawnMissileEncyclopediaPatch
    {
        private static void Prefix(MissileDefinition missile, out bool __state)
        {
            if (missile == null)
            {
                __state = false;
                return;
            }
            __state = string.Equals(missile.jsonKey, TorpedoConstants.MissileJsonKey, System.StringComparison.Ordinal);
            if (__state)
                Mk54SpawnGate.BeginPrefabStamp(missile.unitPrefab);
        }

        private static void Postfix(bool __state, MissileDefinition missile, Missile __result)
        {
            try
            {
                Mk54SpawnGate.EndPrefabStamp();
                if (!__state || missile == null || __result == null)
                    return;

                NobpContent.TryLoad();
                if (missile.unitPrefab != null)
                    VisualMaterials.PrimeShaderFrom(missile.unitPrefab);

                Mk54SpawnGate.Claim(__result);
                Mk54Mass.ApplyLaunch(__result);

                if (NobpContent.VisualPrefab != null)
                {
                    PrefabFactory.StampVisualLive(__result.gameObject, NobpContent.VisualPrefab);
                    PrefabFactory.HideStockRenderers(__result.gameObject);
                }
                else
                    HydraPlugin.ModLog?.LogWarning("Encyclopedia MK54: VisualPrefab null");
            }
            finally
            {
                Mk54SpawnGate.EndPrefabStamp();
            }
        }
    }

    [HarmonyPatch(typeof(MountedMissile), nameof(MountedMissile.Fire))]
    internal static class MountedMissileFirePatch
    {
        private static void Prefix(MountedMissile __instance, Unit target)
        {
            if (__instance == null || __instance.info == null)
                return;
            if (__instance.info.weaponName != TorpedoConstants.WeaponInfoName &&
                __instance.info.shortName != TorpedoConstants.ShortName &&
                __instance.info.shortName != TorpedoConstants.ShortNameLegacy)
                return;

            if (TorpedoBootstrap.TorpedoDefinition?.unitPrefab != null)
                __instance.info.weaponPrefab = TorpedoBootstrap.TorpedoDefinition.unitPrefab;

            Mk54SpawnGate.NoteFire(__instance);
            Transform? vis = PrefabFactory.FindTorpedoVisual(__instance.transform);
            HydraPlugin.ModLog?.LogInfo(
                $"MK54 Fire: target={(target != null ? target.name : "none")} prefab={(__instance.info.weaponPrefab != null ? __instance.info.weaponPrefab.name : "NULL")} visParent={vis?.parent?.name ?? "none"} pending={Mk54SpawnGate.Pending}");
        }
    }
}
