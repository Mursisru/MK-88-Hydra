using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Prop spin: fixed localPos, rotate only around local X.</summary>
    internal static class TorpedoPropSpin
    {
        internal static bool Capture(Transform? prop, out Quaternion restLocalRot)
        {
            restLocalRot = Quaternion.identity;
            if (prop == null)
                return false;
            restLocalRot = prop.localRotation;
            return true;
        }

        internal static void Tick(Transform? prop, bool captured, Quaternion restLocalRot, float angleDeg)
        {
            if (!captured || prop == null)
                return;
            // Blender props: spin shaft = local X only (no hub orbit / no YZ).
            prop.localRotation = restLocalRot * Quaternion.AngleAxis(angleDeg, Vector3.right);
        }
    }
}
