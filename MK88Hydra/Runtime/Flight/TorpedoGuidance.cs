using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Air lock = fire target only. Never retarget to nearest ship.
    /// Dead lock: keep lastKnown ship pos for swim boom (multi-torpedo salvo).
    /// </summary>
    internal sealed class TorpedoGuidance
    {
        private PersistentID _lockId = PersistentID.None;
        private Vector3 _shipPos;
        private Vector3 _lastKnownShipPos;
        private bool _lockDied;
        private Vector3 _launchHeading = Vector3.forward;
        private Vector3 _launchPos;
        private float _swimFuelUsedM;

        internal void Build(Missile missile, Unit? fireTarget)
        {
            _swimFuelUsedM = 0f;
            _lockDied = false;
            if (missile == null)
            {
                _lockId = PersistentID.None;
                _shipPos = Vector3.zero;
                _lastKnownShipPos = Vector3.zero;
                _launchPos = Vector3.zero;
                return;
            }

            Vector3 launch = missile.transform.position;
            _launchPos = launch;
            _launchHeading = Horiz(missile.startingVelocity);
            if (_launchHeading.sqrMagnitude < 0.01f && missile.rb != null)
                _launchHeading = Horiz(missile.rb.velocity);
            if (_launchHeading.sqrMagnitude < 0.01f)
                _launchHeading = Horiz(missile.transform.forward);
            if (_launchHeading.sqrMagnitude < 0.01f)
                _launchHeading = Vector3.forward;

            Unit? locked = ResolveFireLock(missile, fireTarget);
            if (locked != null)
            {
                _lockId = locked.persistentID;
                _shipPos = locked.transform.position;
                _lastKnownShipPos = _shipPos;
            }
            else
            {
                Mk54FireLock? slot = missile.GetComponent<Mk54FireLock>();
                if (slot != null && slot.Id.IsValid)
                {
                    _lockId = slot.Id;
                    Unit? fromSlot = slot.Resolve();
                    _shipPos = fromSlot != null
                        ? fromSlot.transform.position
                        : launch + _launchHeading * TorpedoConstants.RouteFallbackRunM;
                    _lastKnownShipPos = _shipPos;
                    locked = fromSlot;
                }
                else
                {
                    _lockId = PersistentID.None;
                    _shipPos = launch + _launchHeading * TorpedoConstants.RouteFallbackRunM;
                    _lastKnownShipPos = _shipPos;
                }
            }

            float dist = HorizDist(launch, _shipPos);
            HydraPlugin.ModLog?.LogInfo(
                $"TorpedoGuidance LOCK target={(locked != null ? locked.name : "none")} distShip={dist / 1000f:F1}km");
        }

        /// <summary>Live locked ship only. Null if dead/missing.</summary>
        internal Unit? TryGetLockedShip()
        {
            if (!_lockId.IsValid)
                return null;
            if (!UnitRegistry.TryGetUnit(new PersistentID?(_lockId), out Unit u) || u == null || u.disabled)
            {
                if (!_lockDied)
                {
                    _lockDied = true;
                    HydraPlugin.ModLog?.LogWarning(
                        $"TorpedoGuidance lock dead — swim to lastKnown {_lastKnownShipPos}");
                }
                return null;
            }
            if (u is not Ship)
                return null;
            _shipPos = u.transform.position;
            _lastKnownShipPos = _shipPos;
            return u;
        }

        internal void SyncLocked()
        {
            Unit? ship = TryGetLockedShip();
            if (ship != null)
            {
                _shipPos = ship.transform.position;
                _lastKnownShipPos = _shipPos;
            }
        }

        internal float DistToShip(Vector3 from) => HorizDist(from, _shipPos);

        internal float DistToLastKnown(Vector3 from) => HorizDist(from, _lastKnownShipPos);

        internal Vector3 ShipPos => _shipPos;

        internal Vector3 LastKnownShipPos => _lastKnownShipPos;

        internal bool LockDied => _lockDied;

        internal Vector3 LaunchHeading => _launchHeading;

        internal Vector3 LaunchPos => _launchPos;

        /// <summary>Horizontal distance flown since launch.</summary>
        internal float HorizFlownM(Vector3 now) => HorizDist(_launchPos, now);

        internal Vector3 DirToShip(Vector3 from)
        {
            Vector3 d = _shipPos - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? d.normalized : _launchHeading;
        }

        internal Vector3 SwimAim(Vector3 from, Unit? ship, out bool terminal)
        {
            terminal = false;
            float swimY = Datum.LocalSeaY - TorpedoConstants.SwimDepthM;

            // Dead / missing lock: drive to last known position (salvo mates boom there).
            if (ship == null || ship.disabled)
            {
                Vector3 aimDead = _lastKnownShipPos;
                aimDead.y = swimY;
                float distDead = Vector3.Distance(from, aimDead);
                terminal = distDead <= TorpedoConstants.TerminalRangeM;
                return aimDead;
            }

            Vector3 shipPos = ship.transform.position;
            _lastKnownShipPos = shipPos;
            float dist = Vector3.Distance(from, shipPos);
            terminal = dist <= TorpedoConstants.TerminalRangeM;

            if (terminal)
            {
                Vector3 aim = shipPos;
                aim.y = Mathf.Min(shipPos.y, Datum.LocalSeaY - 1f);
                return aim;
            }

            Vector3 vel = ship.rb != null ? ship.rb.velocity : Vector3.zero;
            float speed = Mathf.Max(TorpedoConstants.SwimSpeedMps, 1f);
            float leadS = Mathf.Clamp(dist / speed, 0f, TorpedoConstants.InterceptLeadMaxS);
            Vector3 lead = shipPos + vel * leadS;
            lead.y = swimY;
            return lead;
        }

        internal bool ConsumeSwimFuel(float deltaM)
        {
            if (deltaM > 0f)
                _swimFuelUsedM += deltaM;
            return _swimFuelUsedM <= TorpedoConstants.SwimFuelRangeM;
        }

        internal static float HorizDist(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Unit? ResolveFireLock(Missile missile, Unit? fireTarget)
        {
            if (fireTarget != null && !fireTarget.disabled && fireTarget is Ship)
                return fireTarget;

            if (missile.targetID.IsValid &&
                UnitRegistry.TryGetUnit(new PersistentID?(missile.targetID), out Unit t) &&
                t != null && !t.disabled && t is Ship)
                return t;

            Mk54FireLock? slot = missile.GetComponent<Mk54FireLock>();
            return slot != null ? slot.Resolve() : null;
        }

        private static Vector3 Horiz(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.01f ? v.normalized : Vector3.zero;
        }
    }
}
