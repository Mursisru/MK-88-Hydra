using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// 25km ring → dive. Shed/chute only over deep water. Over land: keep gliding to water (no land boom drop).
    /// </summary>
    internal static class TorpedoPhaseRules
    {
        internal static float SeaGapM(Vector3 pos) =>
            pos.y - Datum.LocalSeaY;

        internal static float AltM(float seaGapM, float radarAltM)
        {
            if (radarAltM > 2f && radarAltM < seaGapM + 50f)
                return Mathf.Min(seaGapM, radarAltM);
            return seaGapM;
        }

        /// <summary>Drop corridor: deep water under XZ (map below swim+keel).</summary>
        internal static bool OverDropWater(Vector3 pos) =>
            TorpedoRouteLand.IsDeepWater(pos.x, pos.z);

        /// <summary>Dist to ship ≤ 25km ring.</summary>
        internal static bool ShouldStartRingEntry(float distShipM) =>
            distShipM <= TorpedoConstants.RouteEntryStandoffM + TorpedoConstants.RouteEntryRingSlackM;

        /// <summary>
        /// Remaining air budget cannot cover distance to the 25km ring → dive early.
        /// </summary>
        internal static bool ShouldEarlyRingForAirBudget(float distShipM, float horizFlownM, float lifeS)
        {
            if (lifeS < TorpedoConstants.MinGlideSecondsBeforeShed)
                return false;

            float remainToRing = distShipM - TorpedoConstants.RouteEntryStandoffM;
            if (remainToRing <= 0f)
                return false;

            float airLeft = TorpedoWeaponRange.AirGlideRangeM - horizFlownM;
            return remainToRing > airLeft + TorpedoConstants.RouteAirGlideReachMarginM;
        }

        internal static bool ShouldShedKozuch(bool ringEntry, float seaGapM, float radarAltM, Vector3 pos)
        {
            if (!ringEntry || !OverDropWater(pos))
                return false;
            float alt = AltM(seaGapM, radarAltM);
            return alt <= TorpedoConstants.ShedKozuchAltitudeM + TorpedoConstants.AltitudeGateSlackM;
        }

        internal static bool ShouldDeployParachute(bool glideKitShed, float seaGapM, float radarAltM, Vector3 pos)
        {
            if (!glideKitShed || !OverDropWater(pos))
                return false;
            float alt = AltM(seaGapM, radarAltM);
            return alt <= TorpedoConstants.ParachuteDeployAltitudeM + TorpedoConstants.AltitudeGateSlackM;
        }

        internal static bool ShouldJettisonChuteBox(float seaGapM, float radarAltM, Vector3 pos)
        {
            if (!OverDropWater(pos))
                return false;
            float alt = AltM(seaGapM, radarAltM);
            return alt <= TorpedoConstants.ChuteBoxJettisonAltitudeM + TorpedoConstants.AltitudeGateSlackM;
        }

        internal static bool ShouldSwim(Vector3 pos)
        {
            if (SeaGapM(pos) > -TorpedoConstants.WaterEntrySubmergeM)
                return false;
            return TorpedoRouteLand.IsDeepWater(pos.x, pos.z);
        }

        internal static bool ShouldWaitForWater(Vector3 pos)
        {
            if (SeaGapM(pos) > 40f)
                return false;
            return !TorpedoRouteLand.IsDeepWater(pos.x, pos.z);
        }
    }
}
