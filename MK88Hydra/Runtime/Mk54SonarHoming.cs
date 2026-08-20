using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Active sonar GSN — underwater track + lead intercept to hull.</summary>
    internal static class Mk54SonarHoming
    {
        internal static bool ComputeAim(
            TorpedoGuidance guidance,
            Vector3 from,
            out Vector3 aim,
            out bool terminal,
            out bool gsnActive)
        {
            aim = from;
            terminal = false;
            gsnActive = false;

            Unit? ship = guidance.TryGetLockedShip();
            if (ship == null)
            {
                aim = guidance.SwimAim(from, null, out terminal);
                return true;
            }

            Vector3 shipPos = ship.transform.position;
            float dist = Vector3.Distance(from, shipPos);
            gsnActive = dist <= TorpedoConstants.SonarHomingAcquireRangeM;

            float swimY = Datum.LocalSeaY - TorpedoConstants.SwimDepthM;
            Vector3 vel = ship.rb != null ? ship.rb.velocity : Vector3.zero;
            float speed = Mathf.Max(TorpedoConstants.SwimSpeedMps, 1f);
            float leadS = Mathf.Clamp(dist / speed, 0f, TorpedoConstants.InterceptLeadMaxS);

            Vector3 hull = shipPos;
            hull.y = swimY;
            Vector3 lead = hull + new Vector3(vel.x, 0f, vel.z) * leadS;

            terminal = dist <= TorpedoConstants.TerminalRangeM;
            if (terminal)
            {
                lead = shipPos;
                lead.y = Mathf.Min(shipPos.y, Datum.LocalSeaY - 1f);
            }

            aim = lead;
            guidance.SyncLocked();
            return true;
        }
    }
}
