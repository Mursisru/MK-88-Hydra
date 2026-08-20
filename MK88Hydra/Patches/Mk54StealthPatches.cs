using HarmonyLib;
using Hydra.Bootstrap;
using Hydra.Runtime;
using UnityEngine;

namespace Hydra.Patches
{
    /// <summary>Submerged torpedo: no radar return, no turret acquisition.</summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.GetRadarReturn))]
    internal static class MissileGetRadarReturnMk54StealthPatch
    {
        private static void Postfix(Missile __instance, ref float __result)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return;
            if (Mk54Stealth.IsSubmerged(__instance))
                __result = 0f;
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.UpdateRadarAlt))]
    internal static class MissileUpdateRadarAltMk54StealthPatch
    {
        private static void Postfix(Missile __instance)
        {
            Mk54Stealth.Tick(__instance);
        }
    }

    [HarmonyPatch(typeof(Turret), "Turret_OnDetectTarget")]
    internal static class TurretDetectTargetMk54StealthPatch
    {
        private static bool Prefix(Unit unit)
        {
            if (unit is Missile missile &&
                TorpedoBootstrap.IsOurMissile(missile) &&
                Mk54Stealth.IsSubmerged(missile))
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.InitializeUnit))]
    internal static class UnitInitializeHydraDecoyPatch
    {
        private static void Postfix(Unit __instance)
        {
            if (__instance is Ship ship)
                HydraAcousticDecoyField.AttachIfNeeded(ship);
        }
    }
}
