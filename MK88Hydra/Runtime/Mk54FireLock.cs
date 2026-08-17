using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Fire-target snapshot. Survives SetTarget(null) (needed so seeker/HUD stay off during air).
    /// Air guidance MUST use this — never UnitRegistry nearest-ship scan.
    /// </summary>
    internal sealed class Mk54FireLock : MonoBehaviour
    {
        internal PersistentID Id = PersistentID.None;
        internal string DebugName = "none";

        internal static void Capture(Missile missile, Unit? fireTarget)
        {
            if (missile == null)
                return;

            Unit? locked = fireTarget;
            if (locked == null || locked.disabled || locked is not Ship)
            {
                locked = null;
                if (missile.targetID.IsValid &&
                    UnitRegistry.TryGetUnit(new PersistentID?(missile.targetID), out Unit t) &&
                    t != null && !t.disabled && t is Ship)
                    locked = t;
            }

            Mk54FireLock? slot = missile.GetComponent<Mk54FireLock>();
            if (slot == null)
                slot = missile.gameObject.AddComponent<Mk54FireLock>();

            if (locked != null)
            {
                slot.Id = locked.persistentID;
                slot.DebugName = locked.name;
            }
            else if (!slot.Id.IsValid)
            {
                slot.Id = PersistentID.None;
                slot.DebugName = "none";
            }

            // Clear SyncVar so OpticalSeeker/HUD do not treat this as a glide bomb —
            // guidance reads Mk54FireLock only.
            missile.SetTarget(null);

            HydraPlugin.ModLog?.LogInfo(
                $"MK54FireLock capture target={slot.DebugName} id={slot.Id}");
        }

        internal Unit? Resolve()
        {
            if (!Id.IsValid)
                return null;
            if (!UnitRegistry.TryGetUnit(new PersistentID?(Id), out Unit u) || u == null || u.disabled)
                return null;
            return u is Ship ? u : null;
        }
    }
}
