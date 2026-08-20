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
            HydraPlugin.ModLog?.LogInfo(
                $"MK54 NoteFire pending={Pending} ttl={PendingTtlS:F0}s ownerId={_fireOwnerId}");
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

        internal static void Claim(Missile missile, Unit? fireTarget = null)
        {
            if (missile == null)
                return;

            if (TorpedoBootstrap.TorpedoDefinition != null)
                missile.definition = TorpedoBootstrap.TorpedoDefinition;
            if (TorpedoBootstrap.TorpedoInfo != null)
                InfoField?.SetValue(missile, TorpedoBootstrap.TorpedoInfo);

            if (missile.GetComponent<Mk54Tag>() == null)
                missile.gameObject.AddComponent<Mk54Tag>();

            missile.NetworkunitName = TorpedoConstants.UnitName;
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
    }
}
