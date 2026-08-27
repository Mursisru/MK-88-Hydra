using System.Reflection;
using Hydra.Bootstrap;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Marks a live round as MK54. Vanilla unitPrefab is shared with glide bombs.</summary>
    internal sealed class Mk54Tag : MonoBehaviour
    {
    }

    /// <summary>
    /// Fire enqueues a timed token; SpawnMissile claims only while token is fresh.
    /// Prevents unrelated SpawnMissile(GO) from stealing Pending during RailLaunch.
    /// </summary>
    internal static class Mk54SpawnGate
    {
        private static readonly FieldInfo? InfoField =
            typeof(Missile).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic);

        private const float PendingTtlS = 8f;

        internal static int Pending;
        internal static bool InFlight;
        private static float _pendingUntilRealtime = -1f;
        private static int _fireOwnerId;

        internal static void NoteFire(MountedMissile? mount = null)
        {
            ExpirePendingIfNeeded();
            Pending++;
            _pendingUntilRealtime = Time.realtimeSinceStartup + PendingTtlS;
            _fireOwnerId = mount != null ? mount.GetInstanceID() : 0;
            SyncSharedInfo(mount);
            HydraPlugin.ModLog?.LogInfo(
                $"MK54 NoteFire pending={Pending} ttl={PendingTtlS:F0}s ownerId={_fireOwnerId}");
        }

        /// <summary>Recent Hydra Fire even if Pending token was stolen by unrelated SpawnMissile.</summary>
        internal static bool HasRecentFire() =>
            _pendingUntilRealtime > 0f && Time.realtimeSinceStartup <= _pendingUntilRealtime;

        /// <summary>Shared bomb_glide1 shell spawn that missed TryBegin — reclaim as Hydra.</summary>
        internal static bool ShouldRescueClaim(GameObject? prefab)
        {
            if (!HasRecentFire())
                return false;
            return IsOurFlyPrefab(prefab);
        }

        internal static void SyncSharedInfo(MountedMissile? mount)
        {
            WeaponInfo? shared = TorpedoBootstrap.TorpedoInfo;
            GameObject? fly = TorpedoBootstrap.TorpedoDefinition?.unitPrefab;
            if (shared == null)
                return;
            if (fly != null)
                shared.weaponPrefab = fly;
            if (mount != null)
                mount.info = shared;
        }

        internal static bool TryBegin()
        {
            ExpirePendingIfNeeded();
            if (Pending <= 0)
                return false;
            Pending--;
            InFlight = true;
            return true;
        }

        internal static void End() => InFlight = false;

        private static void ExpirePendingIfNeeded()
        {
            if (Pending <= 0)
                return;
            if (_pendingUntilRealtime < 0f)
                return;
            if (Time.realtimeSinceStartup <= _pendingUntilRealtime)
                return;

            HydraPlugin.ModLog?.LogWarning(
                $"MK54 Pending expired ({Pending}) after {PendingTtlS:F0}s — clearing stolen/stale tokens");
            Pending = 0;
            _pendingUntilRealtime = -1f;
            _fireOwnerId = 0;
        }

        // unitName SyncVar is initial-only — stamp definition on shared bomb shell BEFORE Instantiate.
        private static Missile? _stampMissile;
        private static UnitDefinition? _stampSavedDef;

        internal static bool BeginPrefabStamp(GameObject? prefab)
        {
            EndPrefabStamp();
            MissileDefinition? ours = TorpedoBootstrap.TorpedoDefinition;
            if (prefab == null || ours == null)
                return false;
            Missile? m = prefab.GetComponent<Missile>() ?? prefab.GetComponentInChildren<Missile>(true);
            if (m == null)
                return false;
            _stampMissile = m;
            _stampSavedDef = m.definition;
            m.definition = ours;
            return true;
        }

        internal static void EndPrefabStamp()
        {
            if (_stampMissile != null && _stampSavedDef != null)
                _stampMissile.definition = _stampSavedDef;
            _stampMissile = null;
            _stampSavedDef = null;
        }

        internal static bool IsOurFlyPrefab(GameObject? go)
        {
            if (go == null)
                return false;
            GameObject? fly = TorpedoBootstrap.TorpedoDefinition?.unitPrefab;
            return fly != null && ReferenceEquals(go, fly);
        }

        /// <summary>Kill feed / PersistentUnit snapshot — keep Hydra identity, not bomb_glide1.</summary>
        internal static void ApplyDisplayIdentity(Missile missile)
        {
            if (missile == null)
                return;
            MissileDefinition? def = TorpedoBootstrap.TorpedoDefinition;
            if (def != null)
                missile.definition = def;
            missile.NetworkunitName = TorpedoConstants.UnitName;
            missile.unitName = TorpedoConstants.UnitName;
            if (!UnitRegistry.TryGetPersistentUnit(missile.persistentID, out PersistentUnit pu) || pu == null)
                return;
            pu.unitName = TorpedoConstants.UnitName;
            if (def != null)
                pu.definition = def;
        }

        internal static void Claim(Missile missile, Unit? fireTarget = null)
        {
            if (missile == null)
                return;
            // Another mod already owns this round (e.g. MK-65 Crosswim on AShM shell).
            if (HasForeignOwnerTag(missile))
                return;

            ApplyDisplayIdentity(missile);
            if (TorpedoBootstrap.TorpedoInfo != null)
                InfoField?.SetValue(missile, TorpedoBootstrap.TorpedoInfo);

            if (missile.GetComponent<Mk54Tag>() == null)
                missile.gameObject.AddComponent<Mk54Tag>();

            missile.SetThrottle(0f);
            // Snapshot fire target then clear SyncVar (HUD/seeker off). Never lose lock to nearest-ship.
            Mk54FireLock.Capture(missile, fireTarget);

            Rigidbody? rb = missile.rb != null ? missile.rb : missile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.detectCollisions = false;
                Vector3 src = missile.startingVelocity.sqrMagnitude > 0.01f
                    ? missile.startingVelocity
                    : rb.velocity;
                if (src.sqrMagnitude > 0.01f)
                    rb.velocity = src;
                rb.angularVelocity = Vector3.zero;
            }

            Mk54ShellPrep.Prepare(missile);
            Mk54Mass.ApplyLaunch(missile);
            SafeSeparation.Prepare(missile);
            Mk54Stealth.EnsureAirRadarSignature(missile);
        }

        internal static void FinishVisual(Missile missile)
        {
            if (missile == null)
                return;
            TorpedoPhaseController.Attach(missile);
        }

        /// <summary>Claim + Attach if tagged but controller missing (LocalStart / OnStartClient backup).</summary>
        internal static void EnsureController(Missile missile)
        {
            if (missile == null || !TorpedoBootstrap.IsOurMissile(missile))
                return;
            Claim(missile);
            FinishVisual(missile);
        }

        /// <summary>Do not hijack rounds tagged by sibling mods (Crosswim etc.).</summary>
        private static bool HasForeignOwnerTag(Missile missile)
        {
            if (missile == null)
                return false;
            MonoBehaviour[] comps = missile.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                MonoBehaviour? c = comps[i];
                if (c == null)
                    continue;
                string n = c.GetType().Name;
                if (n.IndexOf("CrosswimTag", System.StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }
}
