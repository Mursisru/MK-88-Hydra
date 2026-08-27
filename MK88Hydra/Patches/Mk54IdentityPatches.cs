using System.Reflection;
using HarmonyLib;
using Hydra.Bootstrap;
using Hydra.Runtime;
using UnityEngine;

namespace Hydra.Patches
{
    /// <summary>Shared bomb_glide1 prefab would show Optical / shell yield. Override identity for MK54.</summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.GetYield))]
    internal static class MissileGetYieldPatch
    {
        private static void Postfix(Missile __instance, ref float __result)
        {
            if (TorpedoBootstrap.IsOurMissile(__instance))
                __result = TorpedoConstants.BlastYieldKg;
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.GetSeekerType))]
    internal static class MissileGetSeekerTypePatch
    {
        private static bool Prefix(Missile __instance, ref string __result)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return true;
            __result = TorpedoConstants.SeekerTypeName;
            return false;
        }
    }

    [HarmonyPatch(typeof(OpticalSeekerBomb), nameof(OpticalSeekerBomb.GetSeekerType))]
    internal static class OpticalSeekerBombGetSeekerTypePatch
    {
        private static bool Prefix(OpticalSeekerBomb __instance, ref string __result)
        {
            Missile? m = __instance.GetComponentInParent<Missile>();
            if (m == null || !TorpedoBootstrap.IsOurMissile(m))
                return true;
            __result = TorpedoConstants.SeekerTypeName;
            return false;
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), nameof(AircraftSelectionMenu.DisplayInfo))]
    internal static class AircraftSelectionMk54DisplayPatch
    {
        private static void Postfix(AircraftSelectionMenu __instance, WeaponInfo weaponInfo)
        {
            if (!IsOurInfo(weaponInfo))
                return;

            // Force SO fields — loadout UI may still hold a stale copy.
            weaponInfo.costPerRound = TorpedoConstants.Cost;
            weaponInfo.blastDamage = TorpedoConstants.BlastYieldKg;
            weaponInfo.massPerRound = TorpedoConstants.LaunchMassKg;

            SetTmp(__instance, "weaponSeeker", TorpedoConstants.SeekerTypeName);
            SetTmp(__instance, "weaponHE", "HE: " + UnitConverter.YieldReading(TorpedoConstants.BlastYieldKg));
            SetTmp(__instance, "weaponCost", "C: " + UnitConverter.ValueReading(TorpedoConstants.Cost));
            SetTmp(__instance, "weaponRCS", string.Format("RCS: {0}", TorpedoConstants.RadarSize));
        }

        private static bool IsOurInfo(WeaponInfo? info)
        {
            return info != null &&
                   (info.weaponName == TorpedoConstants.WeaponInfoName ||
                    info.shortName == TorpedoConstants.ShortName ||
                    info.shortName == TorpedoConstants.ShortNameLegacy);
        }

        internal static void SetTmp(object host, string field, string value)
        {
            FieldInfo? f = host.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            object? tmp = f?.GetValue(host);
            if (tmp == null)
                return;
            PropertyInfo? p = tmp.GetType().GetProperty("text");
            p?.SetValue(tmp, value);
        }
    }

    [HarmonyPatch(typeof(EncyclopediaBrowser), "DisplayUnitInfo")]
    internal static class EncyclopediaMk54DisplayPatch
    {
        private static void Postfix(EncyclopediaBrowser __instance, UnitDefinition definition)
        {
            if (definition == null ||
                !string.Equals(definition.jsonKey, TorpedoConstants.MissileJsonKey, System.StringComparison.Ordinal))
                return;

            // DisplayUnitInfo reads cost from shared bomb_glide1 WeaponInfo on unitPrefab — override UI.
            definition.value = TorpedoConstants.Cost;
            definition.length = TorpedoConstants.LengthM;
            definition.width = TorpedoConstants.WidthM;
            definition.height = TorpedoConstants.HeightM;
            definition.radarSize = TorpedoConstants.RadarSize;

            AircraftSelectionMk54DisplayPatch.SetTmp(__instance, "guidance", TorpedoConstants.SeekerTypeName);
            AircraftSelectionMk54DisplayPatch.SetTmp(
                __instance, "yield", UnitConverter.YieldReading(TorpedoConstants.BlastYieldKg) + " TNT");
            AircraftSelectionMk54DisplayPatch.SetTmp(
                __instance, "mass", UnitConverter.WeightReading(TorpedoConstants.LaunchMassKg));
            AircraftSelectionMk54DisplayPatch.SetTmp(
                __instance, "cost", UnitConverter.ValueReading(TorpedoConstants.Cost));
            AircraftSelectionMk54DisplayPatch.SetTmp(
                __instance, "length", UnitConverter.DimensionReading(TorpedoConstants.LengthM));
            AircraftSelectionMk54DisplayPatch.SetTmp(
                __instance, "width", UnitConverter.DimensionReading(TorpedoConstants.WidthM));
            AircraftSelectionMk54DisplayPatch.SetTmp(
                __instance, "height", UnitConverter.DimensionReading(TorpedoConstants.HeightM));
            AircraftSelectionMk54DisplayPatch.SetTmp(
                __instance, "rcs", string.Format("{0}", TorpedoConstants.RadarSize));
        }
    }

    [HarmonyPatch(typeof(MissileDefinition), nameof(MissileDefinition.GetMass))]
    internal static class MissileDefinitionGetMassPatch
    {
        private static void Postfix(MissileDefinition __instance, ref float __result)
        {
            if (__instance == null ||
                !string.Equals(__instance.jsonKey, TorpedoConstants.MissileJsonKey, System.StringComparison.Ordinal))
                return;
            __result = TorpedoConstants.LaunchMassKg;
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.GetMass))]
    internal static class MissileGetMassPatch
    {
        private static void Postfix(Missile __instance, ref float __result)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return;
            __result = TorpedoConstants.LaunchMassKg;
        }
    }

    [HarmonyPatch(typeof(WeaponMount), nameof(WeaponMount.Initialize))]
    internal static class WeaponMountInitializeMk54Patch
    {
        private static void Postfix(WeaponMount __instance)
        {
            if (__instance == null)
                return;
            if (!string.Equals(__instance.jsonKey, TorpedoConstants.MountJsonKey, System.StringComparison.Ordinal) &&
                !string.Equals(__instance.jsonKey, TorpedoConstants.MountJsonKeyDouble, System.StringComparison.Ordinal))
                return;

            WeaponInfo? info = TorpedoBootstrap.TorpedoInfo ?? __instance.info;
            if (info == null)
                return;

            // Force shared SO so multi-pylon Hydras stack into one WeaponStation.
            __instance.info = info;
            __instance.sortWeapons = true;

            int ammo = __instance.ammo > 0 ? __instance.ammo : 1;
            info.weaponName = TorpedoConstants.WeaponInfoName;
            info.shortName = TorpedoConstants.ShortName;
            Sprite? preview = Hydra.Runtime.HydraWeaponIcon.Get();
            if (preview != null)
                info.weaponIcon = preview;
            info.massPerRound = TorpedoConstants.LaunchMassKg;
            info.blastDamage = TorpedoConstants.BlastYieldKg;
            info.costPerRound = TorpedoConstants.Cost;
            info.bomb = false;
            info.glideBomb = true;
            info.missile = false;
            Mk54AiEmployment.ApplyProfile(info);
            if (TorpedoBootstrap.TorpedoDefinition?.unitPrefab != null)
                info.weaponPrefab = TorpedoBootstrap.TorpedoDefinition.unitPrefab;

            __instance.mountName = ammo >= 2
                ? TorpedoConstants.MountDisplayName + " x2"
                : TorpedoConstants.MountDisplayName;
            __instance.mass = __instance.emptyMass + TorpedoConstants.LaunchMassKg * ammo;
            __instance.RCS = TorpedoConstants.RadarSize;
            __instance.emptyCost = 0f;

            if (__instance.prefab == null)
                return;
            foreach (MountedMissile mm in __instance.prefab.GetComponentsInChildren<MountedMissile>(true))
            {
                if (mm != null)
                    mm.info = info;
            }
        }
    }

    /// <summary>PersistentUnit snapshots unitName at register — keep kill feed on Hydra identity.</summary>
    [HarmonyPatch(typeof(UnitRegistry), nameof(UnitRegistry.RegisterUnit))]
    internal static class Mk54PersistentIdentityPatch
    {
        private static void Postfix(Unit unit)
        {
            if (unit is not Missile missile || !TorpedoBootstrap.IsOurMissile(missile))
                return;
            Mk54SpawnGate.ApplyDisplayIdentity(missile);
        }
    }
}
