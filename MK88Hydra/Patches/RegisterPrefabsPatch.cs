using Hydra.Bootstrap;
using NuclearOption.Networking;

namespace Hydra.Patches
{
    /// <summary>
    /// Shared vanilla unitPrefab is already registered by the game.
    /// Overwriting its PrefabHash (old MissilePack path) breaks glide bombs and still fails spawn.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(NetworkManagerNuclearOption), "RegisterPrefabs")]
    internal static class RegisterPrefabsPatch
    {
        private static void Postfix()
        {
            MissileDefinition? def = TorpedoBootstrap.TorpedoDefinition;
            if (def?.unitPrefab == null)
                return;
            HydraPlugin.ModLog?.LogInfo(
                $"MK54 uses stock unitPrefab '{def.unitPrefab.name}' jsonKey={def.jsonKey} (no custom PrefabHash).");
        }
    }
}
