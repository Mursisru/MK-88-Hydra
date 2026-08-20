using Hydra.Bootstrap;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Submerged MK-54: no enemy radar, sonar-only; friendly HQ keeps full track.</summary>
    internal static class Mk54Stealth
    {
        internal static bool IsSubmerged(Missile? missile)
        {
            if (missile == null)
                return false;

            TorpedoPhaseController? ctrl = missile.GetComponent<TorpedoPhaseController>();
            if (ctrl != null && ctrl.Phase >= TorpedoPhase.Swim)
                return true;

            return TorpedoImpact.IsUnderwater(missile.transform.position);
        }

        internal static void OnSubmerged(Missile missile)
        {
            if (missile == null)
                return;
            missile.RCS = 0f;
            missile.radarAlt = -TorpedoConstants.SwimDepthM;
        }

        /// <summary>Ensure air RCS matches encyclopedia size (definition swap can leave shell RCS).</summary>
        internal static void EnsureAirRadarSignature(Missile missile)
        {
            if (missile == null || IsSubmerged(missile))
                return;
            missile.RCS = TorpedoConstants.RadarSize;
        }

        internal static void Tick(Missile missile)
        {
            if (!TorpedoBootstrap.IsOurMissile(missile))
                return;

            if (IsSubmerged(missile))
            {
                missile.RCS = 0f;
                missile.radarAlt = Mathf.Min(missile.radarAlt, -1f);
                TickFriendlyTrack(missile);
                return;
            }

            missile.RCS = TorpedoConstants.RadarSize;
        }

        internal static void TickFriendlyTrack(Missile missile)
        {
            if (missile == null || !IsSubmerged(missile))
                return;

            FactionHQ? hq = missile.NetworkHQ;
            if (hq == null)
                return;

            hq.RpcUpdateTrackingInfo(missile.persistentID);
        }

        /// <summary>0..1 acoustic strength for ship sonar at distance.</summary>
        internal static float GetAcousticSignal(Missile missile, float distM, float maxRangeM)
        {
            if (!IsSubmerged(missile) || maxRangeM <= 1f)
                return 0f;

            float norm = Mathf.Clamp01(1f - distM / maxRangeM);
            float body = TorpedoConstants.RadarSize / 0.45f;
            return norm * norm * body * TorpedoConstants.SonarTargetStrength;
        }
    }
}
