using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Underwater: vanilla underwaterEffect has no Shockwave when yield&gt;200.
    /// BlastFrag OverlapSphere hits EVERY ship collider in ~yield^(1/3)*20m — sinks a carrier from one round.
    /// One moderated punch on the nearest/locked ship only.
    /// </summary>
    internal static class Mk54UnderwaterBlast
    {
        internal static void Apply(Missile missile, Vector3 position)
        {
            if (missile == null)
                return;

            PersistentID dealer = missile.ownerID;
            Ship? ship = ResolveShip(missile, position);
            if (ship == null || ship.disabled)
            {
                HydraPlugin.ModLog?.LogWarning("MK54 UW blast: no ship in range");
                return;
            }

            IDamageable? dmg = NearestDamageable(ship, position);
            if (dmg == null)
            {
                HydraPlugin.ModLog?.LogWarning($"MK54 UW blast: ship '{ship.name}' has no IDamageable");
                return;
            }

            float dist = Vector3.Distance(position, ship.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / TorpedoConstants.UnderwaterPunchRadiusM);
            float blast = TorpedoConstants.UnderwaterPunchBlast * falloff;
            if (blast < 1f)
                return;

            // affected~0.35: local keel hit, not whole-ship vaporize
            float affected = Mathf.Lerp(0.15f, TorpedoConstants.UnderwaterPunchAffected, falloff);
            dmg.TakeDamage(0f, blast, affected, 0f, 0f, dealer);
            HydraPlugin.ModLog?.LogInfo(
                $"MK54 UW punch ship='{ship.name}' blast={blast:F0} aff={affected:F2} dist={dist:F1}m yieldHint={TorpedoConstants.BlastYieldKg}");
        }

        private static Ship? ResolveShip(Missile missile, Vector3 position)
        {
            if (missile.targetID.IsValid &&
                UnitRegistry.TryGetUnit(new PersistentID?(missile.targetID), out Unit t) &&
                t is Ship s0 && !s0.disabled)
                return s0;

            Mk54FireLock? slot = missile.GetComponent<Mk54FireLock>();
            Unit? locked = slot != null ? slot.Resolve() : null;
            if (locked is Ship s1 && !s1.disabled)
                return s1;

            Ship? best = null;
            float bestSq = TorpedoConstants.UnderwaterPunchRadiusM * TorpedoConstants.UnderwaterPunchRadiusM;
            foreach (Unit u in UnitRegistry.allUnits)
            {
                if (u == null || u.disabled || u is not Ship ship)
                    continue;
                float sq = (ship.transform.position - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = ship;
                }
            }
            return best;
        }

        private static IDamageable? NearestDamageable(Ship ship, Vector3 position)
        {
            IDamageable? best = null;
            float bestSq = float.MaxValue;
            IDamageable[] all = ship.GetComponentsInChildren<IDamageable>(true);
            for (int i = 0; i < all.Length; i++)
            {
                IDamageable d = all[i];
                if (d is not Component c || c == null)
                    continue;
                float sq = (c.transform.position - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = d;
                }
            }
            return best ?? ship.GetComponent<IDamageable>();
        }
    }
}
