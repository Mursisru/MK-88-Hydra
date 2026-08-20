using System.Collections.Generic;
using HarmonyLib;
using Hydra.Runtime;

namespace Hydra.Patches
{
    /// <summary>AI Hydra: vanilla GlideBombing approach ≤1 km; Fire only at ship / ring / clear water.</summary>
    [HarmonyPatch(typeof(Pilot), nameof(Pilot.Fire))]
    internal static class PilotFireMk54AiPatch
    {
        private static bool Prefix(Pilot __instance)
        {
            if (__instance == null || __instance.aircraft == null)
                return true;

            Aircraft ac = __instance.aircraft;
            if (ac.Player != null)
                return true;

            WeaponManager? wm = ac.weaponManager;
            if (wm == null || wm.currentWeaponStation == null)
                return true;
            if (!Mk54AiEmployment.IsHydraInfo(wm.currentWeaponStation.WeaponInfo))
                return true;

            Unit? target = null;
            List<Unit> list = wm.GetTargetList();
            if (list != null && list.Count > 0)
                target = list[0];

            return Mk54AiEmployment.MayRelease(ac, target);
        }
    }

    [HarmonyPatch(typeof(AIPilotCombatModes), "UseGlideBombs")]
    internal static class AiPilotUseGlideBombsMk54Patch
    {
        private static void Postfix(
            bool checkMode,
            Aircraft ___aircraft,
            WeaponInfo ___currentWeaponInfo,
            Unit ___currentTarget,
            float ___targetAngle,
            ref float ___targetHeight,
            ref float ___bombLastDropped)
        {
            if (!Mk54AiEmployment.IsHydraInfo(___currentWeaponInfo))
                return;

            Mk54AiEmployment.ClampApproachAlt(ref ___targetHeight);
            if (!checkMode)
                return;

            Mk54AiEmployment.TryGlideRelease(
                ___aircraft,
                ___currentTarget,
                ___targetAngle,
                ref ___bombLastDropped);
        }
    }
}
