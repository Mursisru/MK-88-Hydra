using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Deploy seed along PlaceOfSpawnParachute arrow once. Then vanilla canopy fabric physics.
    /// Body must NOT spin with chute — hang torque is killed after Parachute.FixedUpdate.
    /// </summary>
    internal static class TorpedoChuteVisual
    {
        private static readonly FieldInfo? CanopyField =
            typeof(Parachute).GetField("canopy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? CanopyVelField =
            typeof(Parachute).GetField("canopyVel", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? OpenAmountField =
            typeof(Parachute).GetField("openAmount", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static Parachute? Create(Transform? attach, Missile missile)
        {
            if (missile == null || !ParachuteDonor.Ready)
                return null;

            if (attach == null)
            {
                HydraPlugin.ModLog?.LogWarning(
                    "MK54 chute: PlaceOfSpawnParachute missing — abort spawn.");
                return null;
            }

            TorpedoVisualRig? rig = missile.GetComponent<TorpedoVisualRig>();
            Transform? vis = rig != null && rig.VisualRoot != null ? rig.VisualRoot.transform : null;
            TorpedoChuteSocket.LogDummy(attach, vis);

            Parachute? chute = ParachuteDonor.SpawnAtSocket(missile, attach.position);
            if (chute == null)
            {
                HydraPlugin.ModLog?.LogWarning("Mk54 chute spawn failed.");
                return null;
            }

            float mass = missile.rb != null ? missile.rb.mass : TorpedoConstants.TorpedoCoreMassKg;
            TorpedoChuteSetup.TuneForMk54(chute, mass);
            PinRootToDummy(chute, missile, attach);
            HydraPlugin.ModLog?.LogInfo(
                $"MK54 chute socket={attach.position} arrow={TorpedoChuteSocket.DeployAxis(attach)} radius={TorpedoChuteSetup.TargetMaxRadiusM:F2}m");
            return chute;
        }

        internal static void DeployCanopy(Parachute? chute, Missile? missile)
        {
            if (chute == null || missile == null)
                return;

            float mass = missile.rb != null ? missile.rb.mass : TorpedoConstants.TorpedoCoreMassKg;
            TorpedoChuteSetup.TuneForMk54(chute, mass);
            Transform? dummy = ResolveDummy(missile);
            PinRootToDummy(chute, missile, dummy);
            if (!IsOpen(chute))
                chute.DeployChute();
            SeedCanopyAlongArrow(chute, missile, dummy);
            ForceOpenAmount(chute, 0.15f); // start inflate; TickInflate finishes
            ParachuteDonor.KillCollidersAfterDeploy(chute);
        }

        /// <summary>Line socket only — do not fight canopy (that locked body+chute as one rigid spin).</summary>
        internal static void TickHoldAft(Parachute? chute, Missile? missile)
        {
            if (chute == null || missile == null || !IsOpen(chute))
                return;
            PinRootToDummy(chute, missile, ResolveDummy(missile));
            TickInflate(chute, Time.fixedDeltaTime);
        }

        internal static bool IsOpen(Parachute? chute) =>
            chute != null && chute.IsOpen();

        internal static void CutAndDestroy(Parachute? chute)
        {
            if (chute == null)
                return;
            chute.CutCanopy();
            Object.Destroy(chute.gameObject);
        }

        private static Transform? ResolveDummy(Missile missile)
        {
            TorpedoVisualRig? rig = missile != null ? missile.GetComponent<TorpedoVisualRig>() : null;
            return rig != null ? rig.AttachParachute : null;
        }

        internal static void PinRootToDummy(Parachute chute, Missile missile, Transform? dummy)
        {
            if (chute == null || missile == null)
                return;

            Transform hull = missile.transform;
            if (chute.transform.parent != hull)
                chute.transform.SetParent(hull, true);

            chute.transform.localScale = Vector3.one;

            if (dummy != null)
            {
                chute.transform.position = dummy.position;
                // Keep root attitude with hull so line origin stays stable — not dummy.rot (coupled spin).
                chute.transform.rotation = hull.rotation;
                return;
            }

            chute.transform.localPosition = -Vector3.forward * (TorpedoConstants.LengthM * 0.5f);
            chute.transform.localRotation = Quaternion.identity;
        }

        /// <summary>One-shot: place canopy on arrow, give fabric opening velocity along arrow (not −fall).</summary>
        internal static void SeedCanopyAlongArrow(Parachute chute, Missile missile, Transform? dummy)
        {
            if (chute == null || missile == null || dummy == null)
                return;

            PinRootToDummy(chute, missile, dummy);

            Vector3 attach = dummy.position;
            Vector3 arrow = TorpedoChuteSocket.DeployAxis(dummy);
            Vector3 want = attach + arrow * TorpedoConstants.ChuteLineLengthM;

            if (CanopyField?.GetValue(chute) is GameObject canopy && canopy != null)
            {
                canopy.transform.position = want;
                Vector3 lookUp = dummy.up;
                if (Mathf.Abs(Vector3.Dot(arrow, lookUp)) > 0.95f)
                    lookUp = dummy.right;
                canopy.transform.LookAt(attach, lookUp);
            }

            // Opening impulse along arrow — then vanilla drag/gravity run free.
            CanopyVelField?.SetValue(chute, arrow * TorpedoConstants.ChuteDeployImpulse);
        }

        internal static void ForceOpenAmount(Parachute chute, float target)
        {
            if (chute == null || OpenAmountField == null)
                return;
            float cur = OpenAmountField.GetValue(chute) is float f ? f : 0f;
            OpenAmountField.SetValue(chute, Mathf.Max(cur, Mathf.Clamp01(target)));
        }

        internal static void TickInflate(Parachute chute, float dt)
        {
            if (chute == null || OpenAmountField == null || dt <= 0f)
                return;
            float cur = OpenAmountField.GetValue(chute) is float f ? f : 0f;
            if (cur >= 0.99f)
                return;
            OpenAmountField.SetValue(chute, Mathf.MoveTowards(cur, 1f, TorpedoConstants.ChuteInflatePerSec * dt));
        }

        /// <summary>After vanilla chute physics: kill body spin from line torque. Do not snap canopy.</summary>
        internal static void AfterChutePhysics(Parachute chute, Missile missile)
        {
            if (missile?.rb == null)
                return;
            PinRootToDummy(chute, missile, ResolveDummy(missile));
            TickInflate(chute, Time.fixedDeltaTime);
            // Line AddTorque spins hull with canopy — zero it every step.
            missile.rb.angularVelocity = Vector3.zero;
            missile.rb.angularDrag = TorpedoConstants.ChuteBodyAngularDrag;
        }

        internal static void HoldCanopyOnHullAxis(Parachute chute, Missile missile) =>
            SeedCanopyAlongArrow(chute, missile, ResolveDummy(missile));
    }
}
