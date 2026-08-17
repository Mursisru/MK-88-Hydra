using HarmonyLib;
using Hydra.Bootstrap;
using Hydra.Runtime;
using UnityEngine;

namespace Hydra.Patches
{
    [HarmonyPatch(typeof(Missile), "LocalStart")]
    internal static class MissileLocalStartPatch
    {
        private static void Postfix(Missile __instance)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return;
            Mk54SpawnGate.EnsureController(__instance);
        }
    }

    [HarmonyPatch(typeof(Missile), "StartMissile")]
    internal static class MissileStartMissilePatch
    {
        private static void Postfix(Missile __instance)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return;
            Mk54SpawnGate.EnsureController(__instance);
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.Arm))]
    internal static class MissileArmBlockPatch
    {
        private static bool Prefix(Missile __instance)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return true;

            // Safe-sep glide: block vanilla Arm. After shed: allow.
            TorpedoPhaseController? ctrl = __instance.GetComponent<TorpedoPhaseController>();
            return ctrl != null && ctrl.Phase >= TorpedoPhase.Ballistic;
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
    internal static class MissileDetonateBlockPatch
    {
        private static bool Prefix(Missile __instance)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return true;
            if (Mk54DetonateGate.Allow)
                return true;
            // Enemy TakeDamage is calling Detonate — allow. SlowChecks / slam fuse — block.
            if (Mk54DetonateGate.CombatDepth > 0)
                return true;

            TorpedoPhaseController? ctrl = __instance.GetComponent<TorpedoPhaseController>();
            HydraPlugin.ModLog?.LogWarning(
                $"MK54 Detonate blocked t={__instance.timeSinceSpawn:F2}s phase={ctrl?.Phase} armed={__instance.IsArmed()} spd={__instance.speed:F1} radar={__instance.radarAlt:F1}");
            return false;
        }
    }

    /// <summary>
    /// OpticalSeekerBomb.SlowChecks: Armed && speed&lt;30 && radarAlt&lt;30 → Detonate.
    /// Chute brakes below 30; radarAlt is often bogus. Zero physics-slam impactDamage.
    /// </summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.TakeDamage))]
    internal static class MissileTakeDamageMk54Patch
    {
        private static void Prefix(Missile __instance, ref float impactDamage)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return;
            impactDamage = 0f;
            Mk54DetonateGate.CombatDepth++;
        }

        private static void Postfix(Missile __instance)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return;
            if (Mk54DetonateGate.CombatDepth > 0)
                Mk54DetonateGate.CombatDepth--;
        }
    }

    [HarmonyPatch(typeof(Missile), "DetectCollisions")]
    internal static class MissileDetectCollisionsPatch
    {
        private static bool Prefix(Missile __instance)
        {
            if (!TorpedoBootstrap.IsOurMissile(__instance))
                return true;

            // Always skip vanilla fuse — TorpedoImpact.ProbeHull owns hits (model-sized, sea/chute safe).
            return false;
        }
    }

    [HarmonyPatch(typeof(MissileSeeker), nameof(MissileSeeker.Seek))]
    internal static class MissileSeekerSeekPatch
    {
        private static bool Prefix(MissileSeeker __instance)
        {
            Missile? m = __instance.GetComponentInParent<Missile>();
            return m == null || !TorpedoBootstrap.IsOurMissile(m);
        }
    }

    /// <summary>LocalStart calls Initialize before Seek — must skip or knownPos tracks ship.</summary>
    [HarmonyPatch(typeof(OpticalSeekerBomb), nameof(OpticalSeekerBomb.Initialize))]
    internal static class OpticalSeekerBombInitializePatch
    {
        private static bool Prefix(OpticalSeekerBomb __instance)
        {
            Missile? m = __instance.GetComponentInParent<Missile>();
            if (m == null || !TorpedoBootstrap.IsOurMissile(m))
                return true;
            HydraPlugin.ModLog?.LogInfo("MK54 OpticalSeekerBomb.Initialize skipped");
            return false;
        }
    }

    /// <summary>Missile.ServerFixedUpdate calls seeker.Seek() by reference — enabled=false does not stop it.</summary>
    [HarmonyPatch(typeof(OpticalSeekerBomb), nameof(OpticalSeekerBomb.Seek))]
    internal static class OpticalSeekerBombSeekPatch
    {
        private static bool Prefix(OpticalSeekerBomb __instance)
        {
            Missile? m = __instance.GetComponentInParent<Missile>();
            return m == null || !TorpedoBootstrap.IsOurMissile(m);
        }
    }

    [HarmonyPatch(typeof(OpticalSeekerBomb), "SlowChecks")]
    internal static class OpticalSeekerBombSlowChecksPatch
    {
        private static bool Prefix(OpticalSeekerBomb __instance)
        {
            Missile? m = __instance.GetComponentInParent<Missile>();
            return m == null || !TorpedoBootstrap.IsOurMissile(m);
        }
    }

    [HarmonyPatch(typeof(Parachute), nameof(Parachute.DeployChute))]
    internal static class ParachuteDeployChuteMk54Patch
    {
        private static void Postfix(Parachute __instance)
        {
            if (__instance == null || __instance.GetComponent<Mk54ChuteNoHit>() == null)
                return;
            Missile? m = __instance.GetComponentInParent<Missile>();
            if (m == null || !TorpedoBootstrap.IsOurMissile(m))
                return;
            // Seed along dummy arrow once — then fabric physics.
            TorpedoChuteVisual.HoldCanopyOnHullAxis(__instance, m);
            ParachuteDonor.KillCollidersAfterDeploy(__instance);
        }
    }

    /// <summary>
    /// After vanilla fabric step: keep line socket on dummy, inflate, kill hang-torque spin.
    /// Do NOT snap canopy (that made body+chute spin as one rigid).
    /// </summary>
    [HarmonyPatch(typeof(Parachute), "FixedUpdate")]
    internal static class ParachuteFixedUpdateMk54Patch
    {
        private static void Postfix(Parachute __instance)
        {
            if (__instance == null || __instance.GetComponent<Mk54ChuteNoHit>() == null)
                return;
            if (!__instance.IsOpen())
                return;
            Missile? m = __instance.GetComponentInParent<Missile>();
            if (m == null || !TorpedoBootstrap.IsOurMissile(m))
                return;
            TorpedoChuteVisual.AfterChutePhysics(__instance, m);
        }
    }
}
