using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// 25km ring → dive. Shed/chute only over clear deep-water swim corridor to ship.
    /// Over land / blocked water: keep gliding until unobstructed water.
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

        /// <summary>Deep water under current XZ only.</summary>
        internal static bool OverDropWater(Vector3 pos) =>
            TorpedoRouteLand.IsDeepWater(pos.x, pos.z);

        /// <summary>
        /// Current XZ is deep water AND swim run to ship has no land/shallow barriers.
        /// </summary>
        internal static bool OverClearSwimWater(Vector3 pos, Vector3 shipPos) =>
            OverDropWater(pos) && TorpedoRouteLand.HasClearDeepWaterCorridor(pos, shipPos);

        internal static bool MinGlideElapsed(float lifeS) =>
            lifeS >= TorpedoConstants.MinGlideSecondsBeforeShed;

        /// <summary>Dist to ship ≤ 25km ring (after min glide time).</summary>
        internal static bool ShouldStartRingEntry(float distShipM, float lifeS) =>
            MinGlideElapsed(lifeS) &&
            distShipM <= TorpedoConstants.RouteEntryStandoffM + TorpedoConstants.RouteEntryRingSlackM;

        /// <summary>
        /// Inside the delivery ring over clear swim water while still gliding — must shed.
        /// If land sits on the swim path, keep flying until corridor opens.
        /// </summary>
        internal static bool ShouldForceShedAtRing(
            bool ringEntry,
            float distShipM,
            float ringDiveTimeS,
            float altM,
            float ringAltAtStartM,
            float lifeS,
            Vector3 pos,
            Vector3 shipPos)
        {
            if (!MinGlideElapsed(lifeS) || !ringEntry)
                return false;
            if (!OverClearSwimWater(pos, shipPos))
                return false;
            // Need real dive time after ring arm — avoid Glide→Ballistic→Chute same frame at life=5s.
            if (ringDiveTimeS < TorpedoConstants.RouteEntryDiveStallS)
                return false;

            // Past the nominal 25 km ring while still airborne with kit → shed now.
            if (distShipM <= TorpedoConstants.RouteEntryStandoffM - TorpedoConstants.RouteEntryForceShedInsideM)
                return true;

            // Dive stalled: still high after brief ring dive.
            if (altM > ringAltAtStartM - 40f &&
                altM > TorpedoConstants.ShedKozuchAltitudeM + 50f)
                return true;

            return false;
        }

        /// <summary>
        /// Remaining air budget cannot cover distance to the 25km ring → dive early.
        /// </summary>
        internal static bool ShouldEarlyRingForAirBudget(float distShipM, float horizFlownM, float lifeS)
        {
            if (!MinGlideElapsed(lifeS))
                return false;

            float remainToRing = distShipM - TorpedoConstants.RouteEntryStandoffM;
            if (remainToRing <= 0f)
                return false;

            float airLeft = TorpedoWeaponRange.AirGlideRangeM - horizFlownM;
            return remainToRing > airLeft + TorpedoConstants.RouteAirGlideReachMarginM;
        }

        internal static bool ShouldShedKozuch(
            bool ringEntry,
            float seaGapM,
            float radarAltM,
            float lifeS,
            float ringDiveTimeS,
            Vector3 pos,
            Vector3 shipPos)
        {
            if (!MinGlideElapsed(lifeS) || !ringEntry || !OverClearSwimWater(pos, shipPos))
                return false;
            // Give ring dive a beat so Glide→Ballistic→Chute is not same FixedUpdate as ring arm.
            if (ringDiveTimeS < 0.25f)
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
