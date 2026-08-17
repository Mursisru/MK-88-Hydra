using HarmonyLib;
using Hydra.Bootstrap;
using Hydra.Runtime;

namespace Hydra.Patches
{
    /// <summary>Yashma: stamp extras after the round exists, never during ServerObjectManager.Spawn.</summary>
    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class MissileOnStartClientMk54Patch
    {
        private static void Postfix(Missile __instance)
        {
            if (__instance == null || !TorpedoBootstrap.IsOurMissile(__instance))
                return;
            Mk54SpawnGate.EnsureController(__instance);
        }
    }
}
