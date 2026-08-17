using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Ballistic: soft weathercock, no tumble. Under chute: stable nose, zero spin (chute brakes via line force only).
    /// </summary>
    internal static class TorpedoBallisticFall
    {
        internal static void Apply(Missile missile, float dt)
        {
            if (missile?.rb == null || dt <= 0f)
                return;

            Rigidbody rb = missile.rb;
            rb.useGravity = true;
            rb.detectCollisions = false;
            rb.drag = TorpedoConstants.BallisticDrag;
            rb.angularDrag = TorpedoConstants.BallisticAngularDrag;

            if (rb.angularVelocity.sqrMagnitude > TorpedoConstants.BallisticMaxAngVelRad * TorpedoConstants.BallisticMaxAngVelRad)
                rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, TorpedoConstants.BallisticMaxAngVelRad);
            rb.angularVelocity *= TorpedoConstants.BallisticAngVelDamp;

            AlignNoseToVelocity(missile, dt, TorpedoConstants.BallisticAlignDegS);
        }

        internal static void ApplyUnderChute(Missile missile, float dt)
        {
            if (missile?.rb == null || dt <= 0f)
                return;

            Rigidbody rb = missile.rb;
            rb.useGravity = true;
            rb.detectCollisions = false;
            rb.drag = TorpedoConstants.ChuteBodyDrag;
            rb.angularDrag = TorpedoConstants.ChuteBodyAngularDrag;
            // No rotate-with-canopy. Line force still slows fall; torque discarded in AfterChutePhysics.
            rb.angularVelocity = Vector3.zero;
        }

        internal static void AlignNoseToVelocity(Missile missile, float dt, float degPerSec)
        {
            if (missile?.rb == null || dt <= 0f)
                return;

            Rigidbody rb = missile.rb;
            Vector3 v = rb.velocity;
            if (v.sqrMagnitude < 4f)
                return;

            Vector3 dir = v.normalized;
            Vector3 up = missile.transform.up;
            if (Mathf.Abs(Vector3.Dot(dir, up)) > 0.92f)
                up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(dir, up)) > 0.92f)
                up = missile.transform.right;

            Quaternion want = Quaternion.LookRotation(dir, up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, want, degPerSec * dt));
        }

        internal static void KillSpin(Missile? missile)
        {
            if (missile?.rb == null)
                return;
            missile.rb.angularVelocity = Vector3.zero;
        }
    }
}
