using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Hull-sized impact fuse. Sea ignored for mid-air hits.
    /// Underwater: bomb_glide1 underwaterEffect has no Shockwave в†’ Warhead deals 0 damage.
    /// We always apply DamageEffects.BlastFrag for underwater detonations.
    /// </summary>
    internal static class TorpedoImpact
    {
        private static readonly int LethalMask =
            PhysicsLayers.StaticsMask.value | PhysicsLayers.ShipsMask.value;

        internal static bool ProbeHull(Missile missile, out RaycastHit hit)
        {
            hit = default;
            if (missile?.rb == null)
                return false;

            Transform xform = missile.transform;
            Vector3 vel = missile.rb.velocity;
            float speed = vel.magnitude;
            Vector3 dir = speed > 0.2f ? vel / speed : xform.forward;
            if (dir.sqrMagnitude < 0.01f)
                return false;

            float radius = TorpedoConstants.WidthM * 0.45f;
            float halfLen = TorpedoConstants.LengthM * 0.5f;
            Vector3 nose = xform.position + xform.forward * halfLen;
            float look = Mathf.Max(radius * 0.5f, speed * Time.fixedDeltaTime * 1.35f);

            if (!Physics.SphereCast(
                    nose,
                    radius,
                    dir,
                    out hit,
                    look,
                    LethalMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            if (IsIgnoredHit(missile, hit))
                return false;

            HydraPlugin.ModLog?.LogInfo(
                $"MK54 ProbeHull hit '{hit.collider.name}' layer={hit.collider.gameObject.layer} dist={hit.distance:F2} pt={hit.point}");
            return true;
        }

        internal static bool Probe(Missile missile, bool shipsLethal, out RaycastHit hit) =>
            ProbeHull(missile, out hit);

        internal static bool ProbeShipsOnly(Missile missile, out RaycastHit hit)
        {
            if (!ProbeHull(missile, out hit))
                return false;
            return IsShip(hit);
        }

        internal static bool IsHarmlessWater(RaycastHit hit)
        {
            if (hit.collider != null && hit.collider.gameObject.layer == PhysicsLayers.Water)
                return true;

            float y = hit.point.y;
            float sea = Datum.LocalSeaY;
            if (y <= sea + TorpedoConstants.SeaHitSlackM)
                return true;

            return false;
        }

        internal static bool IsBeached(Vector3 pos)
        {
            if (pos.y > Datum.LocalSeaY + 12f)
                return false;
            return TorpedoRouteLand.IsLandOrShallow(pos.x, pos.z);
        }

        internal static bool IsUnderwater(Vector3 pos) =>
            pos.y < Datum.LocalSeaY + 0.1f;

        internal static void DetonateNow(Missile missile, Vector3 normal, string reason)
        {
            if (missile == null || missile.disabled)
                return;

            Vector3 pos = missile.transform.position;
            bool under = IsUnderwater(pos);
            bool shipHit = reason.IndexOf("ship", System.StringComparison.OrdinalIgnoreCase) >= 0;

            HydraPlugin.ModLog?.LogInfo(
                $"MK54 impact '{reason}' pos={pos} under={under} yield={TorpedoConstants.BlastYieldKg}");

            Mk54ShellPrep.ArmForStrike(missile);
            Mk54ShellPrep.EnsureBlastYield(missile);

            Mk54DetonateGate.Allow = true;
            try
            {
                // hitArmor for ship contacts so armor VFX path is considered above water.
                missile.Detonate(normal, shipHit && !under, false);
            }
            finally
            {
                Mk54DetonateGate.Allow = false;
            }

            // bomb_glide1 underwaterEffect has no Shockwave — Warhead.Detonate deals VFX only.
            // Do NOT BlastFrag OverlapSphere (wipes every carrier compartment). One moderated punch.
            if (under)
                Mk54UnderwaterBlast.Apply(missile, pos);
        }

        /// <summary>Legacy entry — routed to single-ship punch.</summary>
        internal static void ApplyUnderwaterBlast(Missile missile, Vector3 position) =>
            Mk54UnderwaterBlast.Apply(missile, position);

        private static bool IsIgnoredHit(Missile missile, RaycastHit hit)
        {
            if (hit.collider == null)
                return true;

            if (hit.distance < 0.04f)
                return true;

            if (IsHarmlessWater(hit))
                return true;

            if (hit.point.y > missile.transform.position.y + 0.15f && !IsShip(hit))
                return true;

            if (IsOurParachute(hit.collider))
                return true;

            if (hit.collider.transform.IsChildOf(missile.transform))
                return true;

            if (missile.ownerID.IsValid &&
                UnitRegistry.TryGetUnit(new PersistentID?(missile.ownerID), out Unit owner) &&
                owner != null &&
                hit.collider.transform.IsChildOf(owner.transform))
                return true;

            return false;
        }

        internal static bool IsOurParachute(Collider col)
        {
            if (col == null)
                return false;
            if (col.GetComponentInParent<Mk54ChuteNoHit>() != null)
                return true;
            if (col.GetComponentInParent<Parachute>() != null)
                return true;

            string n = col.transform.root != null ? col.transform.root.name : col.name;
            return !string.IsNullOrEmpty(n) &&
                   n.IndexOf("Mk54Parachute", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsShip(RaycastHit hit)
        {
            if (hit.collider == null)
                return false;
            if (hit.collider.gameObject.layer == PhysicsLayers.Ships)
                return true;
            return hit.collider.GetComponentInParent<Ship>() != null;
        }
    }

    /// <summary>Marker on chute clone — ProbeHull never treats it as a hit.</summary>
    internal sealed class Mk54ChuteNoHit : MonoBehaviour
    {
    }
}
