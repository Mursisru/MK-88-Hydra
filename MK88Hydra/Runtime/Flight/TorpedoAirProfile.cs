using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// HARD RULE (do not regress):
    /// - Air aim XZ = locked fire-target ship ONLY (or launch heading if no lock).
    /// - NEVER aim at a water/entry/sample waypoint — that yaws to "nearest water".
    /// - NEVER retarget to nearest ship mid-flight.
    /// - Over land: hold clearance altitude (plan to coast) — no ring dive into hills.
    /// - Over water + 25km ring = lower aim Y only (pitch). Bearing stays on lock.
    /// </summary>
    internal static class TorpedoAirProfile
    {
        private const float PreRingSlopeHorizonM = 20000f;
        private const float HoldHeadingRangeM = 12000f;

        internal static void TickAirAim(Missile missile, TorpedoGuidance guidance, Unit? ship, bool ringEntry)
        {
            if (missile == null || guidance == null)
                return;

            guidance.SyncLocked();

            Vector3 pos = missile.transform.position;
            Unit? locked = ship != null && !ship.disabled ? ship : guidance.TryGetLockedShip();
            Vector3 shipPos = locked != null ? locked.transform.position : guidance.ShipPos;

            float distShip = TorpedoGuidance.HorizDist(pos, shipPos);
            float alt = pos.y - Datum.LocalSeaY;
            float t = missile.timeSinceSpawn;
            float ringM = TorpedoConstants.RouteEntryStandoffM + TorpedoConstants.RouteEntryRingSlackM;
            float shedY = Datum.LocalSeaY + TorpedoConstants.ShedKozuchAltitudeM;
            bool overWater = TorpedoPhaseRules.OverDropWater(pos);
            bool clearSwim = locked != null && TorpedoPhaseRules.OverClearSwimWater(pos, shipPos);

            Vector3 aim;
            float stabilizeUntil = TorpedoConstants.GlideStabilizeSeconds + 1.5f;

            if (t < stabilizeUntil)
            {
                // Hold aircraft release heading — no side yaw on first seconds (down-rail + wrong nearest-ship).
                aim = pos + guidance.LaunchHeading * HoldHeadingRangeM;
                aim.y = pos.y;
            }
            else if (!overWater || !clearSwim)
            {
                // Land OR water with land on swim path: hold clearance, glide until clear corridor.
                aim = shipPos;
                aim.y = LandHoldAimY(pos);
            }
            else
            {
                // Water: ship-line XZ. Y = pitch profile.
                // Aggressive ring dive ONLY after ringEntry (min-glide elapsed).
                // dist≤25.8km alone must NOT dive — AI drops inside that band and was
                // crashing to ~150m before life=5s, then shed+chute same frame.
                aim = shipPos;
                if (!ringEntry)
                {
                    if (distShip > ringM)
                    {
                        float remainToRing = distShip - TorpedoConstants.RouteEntryStandoffM;
                        float blend = Mathf.Clamp01(remainToRing / PreRingSlopeHorizonM);
                        aim.y = Mathf.Lerp(shedY, pos.y, blend);
                        aim.y = Mathf.Clamp(aim.y, shedY, Mathf.Max(pos.y, shedY));
                    }
                    else
                    {
                        // Inside geometric ring, waiting min-glide: hold cruise alt.
                        aim.y = Mathf.Max(pos.y, shedY);
                    }
                }
                else if (alt > TorpedoConstants.ShedKozuchAltitudeM + 80f)
                    aim.y = Datum.LocalSeaY + TorpedoConstants.ParachuteDeployAltitudeM;
                else if (alt > TorpedoConstants.ParachuteDeployAltitudeM)
                    aim.y = Datum.LocalSeaY + TorpedoConstants.ParachuteDeployAltitudeM * 0.5f;
                else
                    aim.y = Datum.LocalSeaY;

                float keepY = aim.y;
                aim.x = shipPos.x;
                aim.z = shipPos.z;
                aim.y = keepY;
                WarnIfBearingWasStolen(pos, aim, shipPos, locked);
            }

            missile.SetAimpoint(aim.ToGlobalPosition(), Vector3.zero);
        }

        /// <summary>Keep above map + clearance so land overflight plans to water instead of diving in.</summary>
        private static float LandHoldAimY(Vector3 pos)
        {
            float minY = Datum.LocalSeaY + TorpedoConstants.LandGlideHoldAltM;
            if (TorpedoRouteLand.TryMapY(pos.x, pos.z, out float mapY))
                minY = Mathf.Max(minY, mapY + TorpedoConstants.LandGlideClearanceM);
            return Mathf.Max(pos.y, minY);
        }

        internal static void TickCruise(Missile missile, TorpedoGuidance guidance, Unit? ship) =>
            TickAirAim(missile, guidance, ship, ringEntry: false);

        internal static void TickRingDive(Missile missile, TorpedoGuidance guidance, Unit? ship) =>
            TickAirAim(missile, guidance, ship, ringEntry: true);

        private static void WarnIfBearingWasStolen(Vector3 pos, Vector3 aim, Vector3 shipPos, Unit? locked)
        {
            if (locked == null)
                return;

            Vector3 toAim = aim - pos;
            toAim.y = 0f;
            Vector3 toShip = shipPos - pos;
            toShip.y = 0f;
            if (toAim.sqrMagnitude < 1f || toShip.sqrMagnitude < 1f)
                return;

            float ang = Vector3.Angle(toAim, toShip);
            if (ang > 2.5f)
            {
                HydraPlugin.ModLog?.LogError(
                    $"MK54 AIR AIM REGRESSION yawOffLock={ang:F1}° after force — investigate SetAimpoint callers");
            }
        }
    }
}
