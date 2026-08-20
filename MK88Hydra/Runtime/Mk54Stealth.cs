using Hydra.Bootstrap;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Submerged MK-54 is acoustically/radar silent — drop HQ tracks.</summary>
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

            foreach (FactionHQ hq in FactionRegistry.GetAllHQs())
            {
                if (hq == null)
                    continue;
                if (hq.trackingDatabase.ContainsKey(missile.persistentID))
                    hq.DeregisterTrackedUnit(missile);
            }
        }

        internal static void Tick(Missile missile)
        {
            if (!TorpedoBootstrap.IsOurMissile(missile))
                return;
            if (!IsSubmerged(missile))
                return;

            missile.RCS = 0f;
            missile.radarAlt = Mathf.Min(missile.radarAlt, -1f);
        }
    }
}
