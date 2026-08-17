using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Air helpers. Glide flight itself is vanilla Steering/ApplyAero.</summary>
    internal static class TorpedoGlidePhysics
    {
        internal static void KillAirCollisions(Missile missile)
        {
            if (missile?.rb == null)
                return;
            missile.rb.useGravity = false;
            missile.rb.detectCollisions = false;
        }
    }
}
