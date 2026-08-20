using System.Collections.Generic;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Vanilla GlideBombing employment: ships only, ≤1 km AGL, drop near ~25 km water-entry ring.
    /// Player Fire is never gated.
    /// </summary>
    internal static class Mk54AiEmployment
    {
        internal static bool IsHydraInfo(WeaponInfo? info)
        {
            if (info == null)
                return false;
            return info.weaponName == TorpedoConstants.WeaponInfoName ||
                   info.shortName == TorpedoConstants.ShortName ||
                   info.shortName == TorpedoConstants.ShortNameLegacy;
        }

        /// <summary>Anti-ship profile after TorpedoWeaponRange.Apply (keeps maxRange).</summary>
        internal static void ApplyProfile(WeaponInfo? info)
        {
            if (info == null)
                return;

            info.effectiveness = new RoleIdentity
            {
                antiSurface = 1f,
                antiAir = 0f,
                antiMissile = 0f,
                antiRadar = 0f
            };

            TargetRequirements tr = info.targetRequirements;
            tr.lineOfSight = false;
            tr.minAltitude = 0f;
            tr.maxAltitude = TorpedoConstants.AiTargetMaxRadarAltM;
            tr.minRange = TorpedoConstants.AiReleaseMinDistShipM;
            if (tr.maxRange < TorpedoConstants.AiReleaseMaxDistShipM)
                tr.maxRange = TorpedoConstants.AiReleaseMaxDistShipM;
            tr.minAlignment = TorpedoConstants.AiMinAlignmentDeg;
            tr.minOwnerSpeed = 0f;
            tr.maxSpeed = TorpedoConstants.AiTargetMaxSpeedMps;
            tr.minIR = 0f;
            tr.minRadar = 0f;
            info.targetRequirements = tr;

            info.bomb = false;
            info.glideBomb = true;
            info.missile = false;
        }

        /// <summary>
        /// false = block this Fire. true = allow.
        /// Player / non-Hydra always allowed.
        /// </summary>
        internal static bool MayRelease(Aircraft? aircraft, Unit? target)
        {
            if (aircraft == null)
                return true;
            if (aircraft.Player != null)
                return true;

            WeaponStation? station = aircraft.weaponManager != null
                ? aircraft.weaponManager.currentWeaponStation
                : null;
            if (station == null || !IsHydraInfo(station.WeaponInfo))
                return true;

            if (target == null || target.disabled || target is not Ship)
                return false;

            if (aircraft.radarAlt > TorpedoConstants.AiReleaseMaxOwnerAltM)
                return false;

            Vector3 from = aircraft.transform.position;
            Vector3 shipPos = target.transform.position;
            float dist = TorpedoGuidance.HorizDist(from, shipPos);
            if (dist < TorpedoConstants.AiReleaseMinDistShipM)
                return false;
            if (dist > TorpedoConstants.AiReleaseMaxDistShipM)
                return false;

            WeaponInfo? info = station.WeaponInfo;
            if (info != null && dist > info.targetRequirements.maxRange)
                return false;

            return TorpedoPhaseRules.OverClearSwimWater(from, shipPos);
        }

        internal static void ClampApproachAlt(ref float targetHeight)
        {
            if (targetHeight > TorpedoConstants.AiReleaseMaxOwnerAltM)
                targetHeight = TorpedoConstants.AiReleaseMaxOwnerAltM;
        }

        /// <summary>
        /// Vanilla glide Fire needs a steep height/dist ratio that never holds at 1 km / 25 km.
        /// When the envelope is met, fill target list and Fire.
        /// </summary>
        internal static void TryGlideRelease(
            Aircraft? aircraft,
            Unit? target,
            float targetAngle,
            ref float bombLastDropped)
        {
            if (aircraft == null || aircraft.weaponManager == null)
                return;
            WeaponStation? station = aircraft.weaponManager.currentWeaponStation;
            if (station == null || !IsHydraInfo(station.WeaponInfo))
                return;
            if (Time.timeSinceLevelLoad - bombLastDropped < TorpedoConstants.AiGlideFireCooldownS)
                return;
            if (targetAngle >= TorpedoConstants.AiMinAlignmentDeg)
                return;
            if (!MayRelease(aircraft, target) || target == null)
                return;

            List<Unit> list = aircraft.weaponManager.GetTargetList();
            if (list == null)
                return;
            list.Clear();
            int n = CombatAI.LookForMissileTargets(aircraft, target, station, list);
            aircraft.weaponManager.TargetListChanged();
            if (n <= 0)
                return;

            bombLastDropped = Time.timeSinceLevelLoad;
            aircraft.weaponManager.Fire();
        }
    }
}
