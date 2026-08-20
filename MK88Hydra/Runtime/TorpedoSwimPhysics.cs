using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Underwater: ramped prop thrust + quadratic + linear drag → gradual cruise ≈ SwimSpeedKmh.
    /// </summary>
    internal static class TorpedoSwimPhysics
    {
        internal static void Apply(Missile missile, Vector3 aim, float dt, bool terminal, float swimTimeS)
        {
            if (missile == null || missile.rb == null || dt <= 0f)
                return;

            Rigidbody rb = missile.rb;
            Transform xform = missile.transform;
            Vector3 pos = xform.position;
            float targetY = Datum.LocalSeaY - TorpedoConstants.SwimDepthM;

            rb.useGravity = false;
            rb.detectCollisions = false;
            rb.drag = 0f;
            rb.angularDrag = TorpedoConstants.SwimAngularDrag;

            Vector3 vel = rb.velocity;
            float speed = vel.magnitude;
            Vector3 forward = xform.forward;

            if (speed > 0.05f)
            {
                float q = 0.5f * TorpedoConstants.WaterDensity * speed * speed;
                rb.AddForce(-vel.normalized * (q * TorpedoConstants.SwimCdArea), ForceMode.Force);
                if (TorpedoConstants.SwimLinearDrag > 0f)
                    rb.AddForce(-vel.normalized * (TorpedoConstants.SwimLinearDrag * speed), ForceMode.Force);
            }

            Vector3 localVel = xform.InverseTransformDirection(vel);
            Vector3 dampLocal = new Vector3(
                -localVel.x * TorpedoConstants.SwimSideDamp,
                -localVel.y * TorpedoConstants.SwimHeaveDamp,
                0f);
            rb.AddForce(xform.TransformDirection(dampLocal), ForceMode.Acceleration);

            float depthErr = targetY - pos.y;
            rb.AddForce(Vector3.up * (depthErr * TorpedoConstants.SwimBuoyancyGain), ForceMode.Acceleration);

            Vector3 to = aim - pos;
            Vector3 wantDir;
            if (terminal)
            {
                wantDir = to.sqrMagnitude > 0.01f ? to.normalized : forward;
            }
            else
            {
                Vector3 horiz = to;
                horiz.y = 0f;
                wantDir = horiz.sqrMagnitude > 0.01f ? horiz.normalized : forward;
                wantDir.y = Mathf.Clamp(depthErr * 0.08f, -0.4f, 0.4f);
                if (wantDir.sqrMagnitude > 0.01f)
                    wantDir.Normalize();
            }

            float surge = Vector3.Dot(vel, forward);
            float thrustN = TorpedoConstants.SwimPropThrustN;
            if (terminal)
                thrustN *= TorpedoConstants.TerminalSpeedMult;

            float cruise = TorpedoConstants.SwimSpeedMps;
            float adv = cruise > 0.1f ? Mathf.Clamp01(surge / cruise) : 0f;
            thrustN *= Mathf.Lerp(
                TorpedoConstants.SwimPropStaticMult,
                TorpedoConstants.SwimPropCruiseMult,
                adv);

            float ramp = TorpedoConstants.SwimThrustRampS > 0.01f
                ? Mathf.SmoothStep(0f, 1f, swimTimeS / TorpedoConstants.SwimThrustRampS)
                : 1f;
            thrustN *= ramp;

            rb.AddForce(forward * thrustN, ForceMode.Force);

            float dynQ = 0.5f * TorpedoConstants.WaterDensity * Mathf.Max(speed, 2f) * Mathf.Max(speed, 2f);
            Vector3 axis = Vector3.Cross(forward, wantDir);
            float sinAng = Mathf.Clamp(axis.magnitude, 0f, 1f);
            if (sinAng > 0.001f)
            {
                axis /= sinAng;
                float fin = dynQ * TorpedoConstants.SwimFinAuthority * sinAng;
                if (terminal)
                    fin *= TorpedoConstants.TerminalFinMult;
                rb.AddTorque(axis * fin, ForceMode.Acceleration);
            }

            if (speed > 4f)
            {
                Vector3 flow = vel / speed;
                Quaternion flowRot = Quaternion.LookRotation(flow, Vector3.up);
                float align = TorpedoConstants.SwimAlignDegS * dt;
                if (terminal)
                    align *= 1.4f;
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, flowRot, align * 0.35f));
            }

            float maxW = TorpedoConstants.SwimMaxAngVelRad;
            if (rb.angularVelocity.sqrMagnitude > maxW * maxW)
                rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, maxW);
        }
    }
}
