using HarmonyLib;
using Hydra.Bootstrap;
using Hydra.Runtime;
using UnityEngine;

namespace Hydra.Patches
{
    /// <summary>
    /// Air (until Swim): vanilla Steering/ApplyAero ALWAYS on.
    /// Aimpoint set in Steering Prefix same frame.
    /// </summary>
    [HarmonyPatch(typeof(Missile), "Steering")]
    internal static class MissileSteeringSkipPatch
    {
        private static bool Prefix(Missile __instance)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return true;

            // Swim = our underwater physics; skip vanilla PID.
            if (!TorpedoPhaseController.UsesVanillaAir(__instance))
                return false;

            TorpedoPhaseController.ApplyCruiseAimBeforeSteering(__instance);
            return true;
        }
    }

    [HarmonyPatch(typeof(Missile), "ApplyAero")]
    internal static class MissileApplyAeroSkipPatch
    {
        private static bool Prefix(Missile __instance, ref float __state)
        {
            __state = -1f;
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return true;
            if (!TorpedoPhaseController.UsesVanillaAir(__instance))
                return false;

            Rigidbody? rb = __instance.rb;
            if (rb == null)
                return true;

            // Identity mass 2350; aero ForceMode.Force needs shell-scale mass for glide feel.
            __state = rb.mass;
            rb.mass = TorpedoConstants.ShellAeroMassKg;
            return true;
        }

        private static void Postfix(Missile __instance, float __state)
        {
            if (__state >= 0f && __instance?.rb != null)
                __instance.rb.mass = __state;
        }
    }

    [HarmonyPatch(typeof(Missile), "MotorThrust")]
    internal static class MissileMotorThrustSkipPatch
    {
        private static bool Prefix(Missile __instance) =>
            !TorpedoBootstrap.IsOurMissile(__instance) ||
            TorpedoPhaseController.UsesVanillaAir(__instance);
    }
}
