using Hydra.Bootstrap;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Underwater sonar — detects submerged targets only (radar-like, max 25 km).</summary>
    internal sealed class HydraShipSonar : MonoBehaviour
    {
        private Ship? _ship;
        private float _rangeM;
        private float _nextScan;

        internal static void AttachIfNeeded(Ship ship)
        {
            if (ship == null || ship.GetComponent<HydraShipSonar>() != null)
                return;
            if (HydraSonarRegistry.IsExcluded(ship.definition))
                return;

            var sonar = ship.gameObject.AddComponent<HydraShipSonar>();
            sonar.Init(ship);
        }

        private void Init(Ship ship)
        {
            _ship = ship;
            _rangeM = HydraSonarRegistry.ComputeRangeM(ship.definition);
            HydraPlugin.ModLog?.LogInfo(
                $"Hydra sonar on '{ship.definition?.unitName ?? ship.name}' range={_rangeM / 1000f:F1}km");
        }

        private void Update()
        {
            if (_ship == null || _ship.disabled || !_ship.IsServer)
                return;
            if (Time.time < _nextScan)
                return;

            _nextScan = Time.time + TorpedoConstants.SonarScanIntervalS;
            Scan();
        }

        private void Scan()
        {
            if (_ship == null)
                return;

            FactionHQ? hq = _ship.NetworkHQ;
            if (hq == null)
                return;

            Vector3 scanPos = _ship.transform.position;
            foreach (Unit u in UnitRegistry.allUnits)
            {
                if (u == null || u.disabled || u.NetworkHQ == hq)
                    continue;
                if (u is not Missile missile || !TorpedoBootstrap.IsOurMissile(missile))
                    continue;
                if (!Mk54Stealth.IsSubmerged(missile))
                    continue;

                float dist = Vector3.Distance(scanPos, missile.transform.position);
                if (dist > _rangeM)
                    continue;

                float signal = Mk54Stealth.GetAcousticSignal(missile, dist, _rangeM);
                if (signal < TorpedoConstants.SonarMinSignal)
                    continue;

                hq.RpcUpdateTrackingInfo(missile.persistentID);
            }
        }
    }
}
