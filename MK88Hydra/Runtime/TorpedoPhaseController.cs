using System;
using UnityEngine;

namespace Hydra.Runtime
{
    internal enum TorpedoPhase
    {
        Dropped = 0,
        Stabilize = 1,
        Glide = 2,
        Ballistic = 3,
        Chute = 4,
        WaterEntry = 5,
        Swim = 6,
        Done = 7
    }

    /// <summary>
    /// Vanilla glide (aimpoint only) → 500m shed kozuch → ballistic → 400m chute →
    /// 30m jettison chute box → water → swim intercept → terminal boom.
    /// </summary>
    internal sealed class TorpedoPhaseController : MonoBehaviour
    {
        private Missile? _missile;
        private TorpedoVisualRig? _rig;
        private TorpedoPhase _phase = TorpedoPhase.Dropped;
        private float _phaseTime;
        private float _life;
        private bool _glideKitShed;
        private bool _chuteBoxJettisoned;
        private Parachute? _chute;
        private TorpedoGuidance? _guidance;
        private bool _ringEntry;
        private float _nextRangeLog;
        private float _ringAltAtStart;
        private float _ringDiveTime;
        private bool _loggedApproach30km;

        internal TorpedoPhase Phase => _phase;
        internal bool RingEntry => _ringEntry;
        internal TorpedoGuidance? Guidance => _guidance;

        /// <summary>
        /// Vanilla Steering/ApplyAero ONLY while glide kit is on (Stabilize/Glide).
        /// After shed → gravity fall / chute — not a glide bomb.
        /// </summary>
        internal static bool UsesVanillaAir(Missile? missile)
        {
            if (missile == null)
                return false;
            TorpedoPhaseController? c = missile.GetComponent<TorpedoPhaseController>();
            if (c == null)
                return true;
            return c._phase == TorpedoPhase.Stabilize || c._phase == TorpedoPhase.Glide;
        }

        /// <summary>Same-frame aimpoint before vanilla Steering (glide kit only).</summary>
        internal static void ApplyCruiseAimBeforeSteering(Missile missile)
        {
            if (missile == null)
                return;
            TorpedoPhaseController? c = missile.GetComponent<TorpedoPhaseController>();
            if (c == null || c._guidance == null)
                return;
            if (c._phase != TorpedoPhase.Stabilize && c._phase != TorpedoPhase.Glide)
                return;

            Unit? ship = c.ResolveLockedShip(allowNearestFallback: false);
            TorpedoAirProfile.TickAirAim(missile, c._guidance, ship, c._ringEntry);
        }

        internal static void Attach(Missile missile)
        {
            if (missile == null || missile.GetComponent<TorpedoPhaseController>() != null)
                return;

            try
            {
                TorpedoPhaseController c = missile.gameObject.AddComponent<TorpedoPhaseController>();
                c.Init(missile);
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogError($"TorpedoPhaseController.Attach: {ex}");
            }
        }

        private void Init(Missile missile)
        {
            _missile = missile;
            _rig = TorpedoVisualRig.Ensure(missile);
            _phase = TorpedoPhase.Dropped;
            _phaseTime = 0f;
            _life = 0f;
            _glideKitShed = false;
            _chuteBoxJettisoned = false;
            _ringEntry = false;
            _nextRangeLog = 1f;
            _ringDiveTime = 0f;
            _loggedApproach30km = false;
            missile.SetThrottle(0f);
            Mk54ShellPrep.Disarm(missile);
            TorpedoGlidePhysics.KillAirCollisions(missile);

            _guidance = new TorpedoGuidance();
            // Prefer SyncVar from SpawnMissile(Fire target). Do NOT nearest-scan at Init.
            Unit? ship = ResolveLockedShip(allowNearestFallback: false);
            _guidance.Build(missile, ship);
            ship = _guidance.TryGetLockedShip();
            TorpedoAirProfile.TickAirAim(missile, _guidance, ship, ringEntry: false);

            float dist0 = ship != null
                ? TorpedoGuidance.HorizDist(missile.transform.position, ship.transform.position)
                : -1f;
            HydraPlugin.ModLog?.LogInfo(
                $"TorpedoPhase Init ok id={missile.GetInstanceID()} distShip={dist0 / 1000f:F1}km ship={(ship != null ? ship.name : "none")}");
        }

        private void OnDestroy()
        {
            HydraPlugin.ModLog?.LogWarning(
                $"TorpedoPhase OnDestroy phase={_phase} life={_life:F2}s pos={(_missile != null ? _missile.transform.position.ToString() : "?")}");
        }

        private void FixedUpdate()
        {
            if (_missile == null || _missile.disabled || _phase == TorpedoPhase.Done)
                return;

            float dt = Time.fixedDeltaTime;
            _life += dt;
            _phaseTime += dt;

            if (_life > TorpedoConstants.SoftKillTimeoutS)
            {
                SoftKill();
                return;
            }

            try
            {
                switch (_phase)
                {
                    case TorpedoPhase.Dropped:
                        Enter(TorpedoPhase.Stabilize);
                        break;
                    case TorpedoPhase.Stabilize:
                        TickStabilize();
                        break;
                    case TorpedoPhase.Glide:
                        TickGlide();
                        break;
                    case TorpedoPhase.Ballistic:
                        TickBallistic();
                        break;
                    case TorpedoPhase.Chute:
                        TickChute();
                        break;
                    case TorpedoPhase.WaterEntry:
                        TickWaterEntry();
                        break;
                    case TorpedoPhase.Swim:
                        TickSwim(dt);
                        break;
                }
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogError($"TorpedoPhase tick: {ex}");
            }
        }

        private void Enter(TorpedoPhase next)
        {
            HydraPlugin.ModLog?.LogInfo($"TorpedoPhase {_phase} → {next} life={_life:F1}s");
            _phase = next;
            _phaseTime = 0f;

            if (next == TorpedoPhase.Stabilize || next == TorpedoPhase.Glide)
            {
                Mk54ShellPrep.Disarm(_missile!);
                TorpedoGlidePhysics.KillAirCollisions(_missile!);
            }

            if (next == TorpedoPhase.Ballistic)
            {
                ShedGlideKit();
                TorpedoBallisticFall.KillSpin(_missile);
            }

            if (next == TorpedoPhase.Chute)
            {
                // Stay armed after shed — hull Probe only (sea + chute ignored).
                TorpedoChuteSetup.PrepareMissileBody(_missile!);
                OpenChute();
            }

            if (next == TorpedoPhase.WaterEntry)
            {
                JettisonChuteBoxAndCanopy();
                PrepareWaterEntry();
            }

            if (next == TorpedoPhase.Swim)
            {
                JettisonChuteBoxAndCanopy();
                if (_missile != null)
                {
                    TorpedoChuteSetup.RestoreAfterChute(_missile);
                    TorpedoGlidePhysics.KillAirCollisions(_missile);
                    _missile.SetTangible(true);
                    Mk54ShellPrep.ArmForStrike(_missile);
                    CapSwimEntrySpeed(_missile);
                    Mk54Stealth.OnSubmerged(_missile);
                    Unit? ship = ResolveLockedShip(allowNearestFallback: false);
                    if (ship != null)
                        _missile.SetTarget(ship);
                }
            }

            if (next == TorpedoPhase.Done)
                JettisonChuteBoxAndCanopy();
        }

        private void TickStabilize()
        {
            Unit? ship = ResolveLockedShip(allowNearestFallback: false);
            if (_guidance != null && _missile != null)
                TorpedoAirProfile.TickAirAim(_missile, _guidance, ship, ringEntry: false);
            if (TryImpact("stabilize"))
                return;
            if (_phaseTime >= TorpedoConstants.GlideStabilizeSeconds)
                Enter(TorpedoPhase.Glide);
        }

        private void TickGlide()
        {
            if (_missile == null || _rig == null)
                return;

            if (!_rig.GlideKitVisible)
            {
                Enter(TorpedoPhase.Ballistic);
                return;
            }

            Unit? ship = ResolveLockedShip(allowNearestFallback: false);
            if (_guidance != null)
                _guidance.SyncLocked();

            Vector3 pos = _missile.transform.position;
            float distShip = ship != null
                ? TorpedoGuidance.HorizDist(pos, ship.transform.position)
                : (_guidance != null ? _guidance.DistToShip(pos) : float.MaxValue);
            float alt = SeaGap();

            if (!_loggedApproach30km && distShip < 30000f)
            {
                _loggedApproach30km = true;
                HydraPlugin.ModLog?.LogWarning(
                    $"MK54 APPROACH 30km distShip={distShip / 1000f:F1}km alt={alt:F0}m — ring at 25.8km");
            }

            if (_life >= _nextRangeLog)
            {
                _nextRangeLog = _life + 5f;
                HydraPlugin.ModLog?.LogInfo(
                    $"MK54 air distShip={distShip / 1000f:F1}km alt={alt:F0}m ring={_ringEntry} phase={_phase}");
            }

            float flown = _guidance != null ? _guidance.HorizFlownM(pos) : 0f;
            if (!_ringEntry && TorpedoPhaseRules.ShouldStartRingEntry(distShip))
            {
                _ringEntry = true;
                _ringAltAtStart = alt;
                _ringDiveTime = 0f;
                HydraPlugin.ModLog?.LogWarning(
                    $"MK54 RING ENTRY 25km distShip={distShip / 1000f:F1}km alt={alt:F0}m — ship-line dive (pitch only)");
            }
            else if (!_ringEntry &&
                     TorpedoPhaseRules.ShouldEarlyRingForAirBudget(distShip, flown, _life))
            {
                _ringEntry = true;
                _ringAltAtStart = alt;
                _ringDiveTime = 0f;
                float remain = distShip - TorpedoConstants.RouteEntryStandoffM;
                float airLeft = TorpedoWeaponRange.AirGlideRangeM - flown;
                HydraPlugin.ModLog?.LogWarning(
                    $"MK54 RING ENTRY early air-budget remainToRing={remain / 1000f:F1}km airLeft={airLeft / 1000f:F1}km distShip={distShip / 1000f:F1}km alt={alt:F0}m");
            }

            if (_ringEntry)
                _ringDiveTime += Time.fixedDeltaTime;

            if (_guidance != null)
                TorpedoAirProfile.TickAirAim(_missile, _guidance, ship, _ringEntry);

            if (_ringEntry &&
                TorpedoPhaseRules.OverDropWater(pos) &&
                _ringDiveTime >= 5f &&
                alt > _ringAltAtStart - 40f &&
                alt > TorpedoConstants.ShedKozuchAltitudeM + 50f)
            {
                HydraPlugin.ModLog?.LogWarning(
                    $"MK54 RING dive stalled alt={alt:F0}m start={_ringAltAtStart:F0}m — force shed (water)");
                Enter(TorpedoPhase.Ballistic);
                return;
            }

            _rig.TickWingDeploy(_phaseTime / TorpedoConstants.FinFoldSeconds);
            if (TryImpact("glide"))
                return;

            if (ship != null &&
                Vector3.Distance(_missile.transform.position, ship.transform.position) < 80f)
            {
                Boom(_missile.transform.forward, "air-ship-safety");
                return;
            }

            // Shed/chute only over deep water. Over land: keep Glide until coast.
            if (TorpedoPhaseRules.ShouldShedKozuch(_ringEntry, alt, RadarAlt(), pos))
            {
                HydraPlugin.ModLog?.LogWarning(
                    $"MK54 shed kozuch WATER alt={alt:F0}m distShip={distShip / 1000f:F1}km");
                Enter(TorpedoPhase.Ballistic);
            }
        }

        private void TickBallistic()
        {
            if (_missile == null)
                return;

            if (!_glideKitShed)
                ShedGlideKit();

            TorpedoBallisticFall.Apply(_missile, Time.fixedDeltaTime);
            if (_guidance != null)
                _guidance.SyncLocked();
            if (TryImpact("ballistic"))
                return;

            Vector3 pos = _missile.transform.position;
            if (TorpedoPhaseRules.ShouldDeployParachute(_glideKitShed, SeaGap(), RadarAlt(), pos))
                Enter(TorpedoPhase.Chute);
        }

        private void TickChute()
        {
            if (_missile == null)
                return;

            if (_guidance != null)
                _guidance.SyncLocked();

            TorpedoBallisticFall.ApplyUnderChute(_missile, Time.fixedDeltaTime);

            Vector3 pos = _missile.transform.position;

            if (_chute == null && _phaseTime > 0.5f)
            {
                HydraPlugin.ModLog?.LogWarning("MK54 chute missing — fall to jettison/water.");
                if (TorpedoPhaseRules.ShouldJettisonChuteBox(SeaGap(), RadarAlt(), pos))
                    Enter(TorpedoPhase.WaterEntry);
                return;
            }

            if (!TorpedoChuteVisual.IsOpen(_chute) && _phaseTime > 0.15f)
                TorpedoChuteVisual.DeployCanopy(_chute, _missile);

            TorpedoChuteVisual.TickHoldAft(_chute, _missile);
            TorpedoChuteSetup.PrepareMissileBody(_missile);
            if (TryImpact("chute"))
                return;

            if (TorpedoPhaseRules.ShouldJettisonChuteBox(SeaGap(), RadarAlt(), pos))
            {
                HydraPlugin.ModLog?.LogInfo($"MK54 jettison chute box alt={SeaGap():F0}m");
                Enter(TorpedoPhase.WaterEntry);
            }
        }

        private void TickWaterEntry()
        {
            if (_missile == null || _missile.rb == null)
                return;

            Unit? ship = ResolveLockedShip(allowNearestFallback: false);
            if (_guidance != null)
                TorpedoAirProfile.TickAirAim(_missile, _guidance, ship, ringEntry: true);
            if (TryImpact("entry"))
                return;

            Vector3 pos = _missile.transform.position;
            if (TorpedoPhaseRules.ShouldWaitForWater(pos))
                return;

            if (TorpedoPhaseRules.ShouldSwim(pos))
                Enter(TorpedoPhase.Swim);
        }

        private void TickSwim(float dt)
        {
            if (_missile == null || _missile.rb == null)
                return;

            float stepM = _missile.rb.velocity.magnitude * dt;
            if (_guidance != null && !_guidance.ConsumeSwimFuel(stepM))
            {
                HydraPlugin.ModLog?.LogWarning("MK54 swim fuel exhausted.");
                SoftKill();
                return;
            }

            _rig?.SpinProps(dt);
            if (TryImpact("swim"))
                return;

            if (TorpedoImpact.IsBeached(_missile.transform.position))
            {
                Boom(Vector3.up, "swim-beach");
                return;
            }

            // Dead lock: stay on lastKnown — never nearest-ship retarget.
            Vector3 pos = _missile.transform.position;
            _guidance?.TryDecoySeduction(pos);

            Unit? ship = ResolveLockedShip(allowNearestFallback: false);
            bool terminal = false;
            Vector3 aim = _guidance != null
                ? _guidance.SwimAim(pos, ship, out terminal)
                : pos + _missile.transform.forward * 200f;

            TorpedoSwimPhysics.Apply(_missile, aim, dt, terminal, _phaseTime);

            if (_guidance != null && _guidance.DecoySeduced)
            {
                float toDecoy = Vector3.Distance(pos, aim);
                if (toDecoy <= TorpedoConstants.DetonateProximityM)
                    Boom(_missile.transform.forward, "decoy-trap");
                return;
            }

            if (ship != null)
            {
                float dist = Vector3.Distance(_missile.transform.position, ship.transform.position);
                if (dist <= TorpedoConstants.DetonateProximityM)
                    Boom(_missile.transform.forward, terminal ? "ship-terminal" : "ship");
                return;
            }

            if (_guidance == null)
                return;

            float toLast = Vector3.Distance(_missile.transform.position, aim);
            if (toLast <= TorpedoConstants.DetonateProximityM)
                Boom(_missile.transform.forward, "ship-dead-lastpos");
        }

        private static void CapSwimEntrySpeed(Missile missile)
        {
            if (missile?.rb == null)
                return;

            Vector3 v = missile.rb.velocity;
            Vector3 horiz = new Vector3(v.x, 0f, v.z);
            float cap = TorpedoConstants.SwimEntryHorizCapMps;
            if (horiz.sqrMagnitude > cap * cap)
                horiz = horiz.normalized * cap;
            missile.rb.velocity = new Vector3(horiz.x, v.y, horiz.z);
        }

        private void PrepareWaterEntry()
        {
            if (_missile?.rb == null)
                return;

            TorpedoChuteSetup.RestoreAfterChute(_missile);
            Mk54ShellPrep.Disarm(_missile);
            _missile.rb.detectCollisions = false;
            // Keep existing velocity — no artificial air-brake at 30m.
        }

        private float SeaGap()
        {
            if (_missile == null)
                return 999f;
            _missile.UpdateRadarAlt();
            return TorpedoPhaseRules.SeaGapM(_missile.transform.position);
        }

        private float RadarAlt()
        {
            if (_missile == null)
                return 999f;
            _missile.UpdateRadarAlt();
            return _missile.radarAlt;
        }

        /// <summary>
        /// Fire lock only. Nearest-ship fallback is opt-in but unused for MK swim (dead → lastKnown boom).
        /// </summary>
        private Unit? ResolveLockedShip(bool allowNearestFallback)
        {
            if (_missile == null)
                return null;

            if (_guidance != null)
            {
                Unit? locked = _guidance.TryGetLockedShip();
                if (locked != null)
                    return locked;
            }

            Mk54FireLock? slot = _missile.GetComponent<Mk54FireLock>();
            Unit? fromFire = slot != null ? slot.Resolve() : null;
            if (fromFire != null)
                return fromFire;

            if (!allowNearestFallback)
                return null;

            Unit? best = null;
            float bestSq = TorpedoConstants.ShipSearchRangeM * TorpedoConstants.ShipSearchRangeM;
            foreach (Unit u in UnitRegistry.allUnits)
            {
                if (u == null || u.disabled || u is not Ship)
                    continue;
                if (_missile.NetworkHQ != null && u.NetworkHQ == _missile.NetworkHQ)
                    continue;
                float sq = (_missile.transform.position - u.transform.position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = u;
                }
            }
            return best;
        }

        private bool TryImpact(string tag)
        {
            if (_missile == null)
                return false;

            // Hull-sized probe every phase. Sea + parachute + owner ignored inside ProbeHull.
            if (!TorpedoImpact.ProbeHull(_missile, out RaycastHit hit))
                return false;

            Boom(hit.normal.sqrMagnitude > 0.01f ? hit.normal : Vector3.up, tag);
            return true;
        }

        private void Boom(Vector3 normal, string reason)
        {
            if (_missile == null)
                return;
            TorpedoImpact.DetonateNow(_missile, normal, reason);
            Enter(TorpedoPhase.Done);
        }

        private void ShedGlideKit()
        {
            if (_glideKitShed)
                return;
            _glideKitShed = true;
            _rig?.HideGlideKit();
            if (_missile != null)
            {
                Mk54Mass.ApplyCore(_missile);
                // Combat-ready after shed. PhysX off — our hull Probe is the fuse.
                _missile.SetTangible(true);
                Mk54ShellPrep.ArmForStrike(_missile);
                if (_missile.rb != null)
                    _missile.rb.detectCollisions = false;
            }
            HydraPlugin.ModLog?.LogInfo("MK54 glide kit shed → armed, hull Probe fuse (PhysX off)");
        }

        private void OpenChute()
        {
            if (_chute != null)
                return;

            if (_rig == null || !_rig.ChuteBoxVisible)
            {
                HydraPlugin.ModLog?.LogWarning("MK54 chute blocked: parachute box not present.");
                return;
            }

            Transform? attach = _rig.AttachParachute;
            if (attach == null)
            {
                HydraPlugin.ModLog?.LogWarning(
                    "MK54 chute: PlaceOfSpawnParachute missing — cannot deploy.");
                return;
            }
            _chute = TorpedoChuteVisual.Create(attach, _missile!);
            if (_chute != null)
                TorpedoChuteVisual.DeployCanopy(_chute, _missile);
        }

        private void JettisonChuteBoxAndCanopy()
        {
            if (_chute != null)
            {
                TorpedoChuteVisual.CutAndDestroy(_chute);
                _chute = null;
            }

            if (_chuteBoxJettisoned)
                return;
            _chuteBoxJettisoned = true;
            _rig?.HideChuteBox();
            HydraPlugin.ModLog?.LogInfo("MK54 parachute box jettisoned.");
        }

        private void SoftKill()
        {
            HydraPlugin.ModLog?.LogWarning($"Torpedo SoftKill life={_life:F1}s");
            Enter(TorpedoPhase.Done);
            if (_missile == null || _missile.disabled)
                return;

            Mk54ShellPrep.ArmForStrike(_missile);
            Mk54DetonateGate.Allow = true;
            try { _missile.Detonate(Vector3.up, false, true); }
            finally { Mk54DetonateGate.Allow = false; }
        }
    }
}
