using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// PlaceOfSpawnParachute: position = socket, Blender Single Arrow = local +Z = Unity forward.
    /// NEVER use velocity / motion for deploy direction.
    /// </summary>
    internal static class TorpedoChuteSocket
    {
        /// <summary>Deploy direction = dummy local +Z (Single Arrow). No velocity.</summary>
        internal static Vector3 DeployAxis(Transform dummy)
        {
            if (dummy == null)
                return Vector3.up;
            // Blender Empty "Single Arrow" → local +Z → Transform.forward after FBX TRS.
            Vector3 d = dummy.forward;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up;
        }

        internal static void LogDummy(Transform? dummy, Transform? visualRoot)
        {
            if (dummy == null)
                return;
            Vector3 f = dummy.forward;
            Vector3 u = dummy.up;
            Vector3 r = dummy.right;
            float along = 0f;
            if (visualRoot != null)
                along = Vector3.Dot(dummy.position - visualRoot.position, -visualRoot.forward);
            HydraPlugin.ModLog?.LogInfo(
                $"MK54 chute dummy '{dummy.name}' pos={dummy.position} arrow(+Z/fwd)={f} up={u} right={r} alongAft={along:F2}m");
        }
    }
}
