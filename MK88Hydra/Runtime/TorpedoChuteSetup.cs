using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Canopy radius = GPO-N maxRadius × 3. Drag/spring from donor feel (fabric), not mass×16.
    /// </summary>
    internal static class TorpedoChuteSetup
    {
        private static readonly FieldInfo? MaxRadius =
            typeof(Parachute).GetField("maxRadius", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? MaxDrag =
            typeof(Parachute).GetField("maxDrag", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? LineSpring =
            typeof(Parachute).GetField("lineSpring", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? Damping =
            typeof(Parachute).GetField("damping", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? CanopyMass =
            typeof(Parachute).GetField("canopyMass", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? OpenAltMin =
            typeof(Parachute).GetField("openAltitudeMin", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? OpenAltMax =
            typeof(Parachute).GetField("openAltitudeMax", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? OpenDelayMin =
            typeof(Parachute).GetField("openDelayMin", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? OpenSpeedMin =
            typeof(Parachute).GetField("openSpeedMin", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? OpenSpeedMax =
            typeof(Parachute).GetField("openSpeedMax", BindingFlags.Instance | BindingFlags.NonPublic);

        private static float _donorMaxDrag = TorpedoConstants.ChuteMaxDrag;
        private static float _donorLineSpring = TorpedoConstants.ChuteLineSpring;
        private static float _donorDamping = TorpedoConstants.ChuteDamping;
        private static float _donorCanopyMass = TorpedoConstants.ChuteCanopyMassKg;

        internal static float DonorMaxRadiusM { get; private set; } = 1.2f;
        internal static bool DonorCaptured { get; private set; }

        internal static void CaptureDonorRadius(Parachute? donor)
        {
            if (donor == null)
                return;

            if (MaxRadius?.GetValue(donor) is float r && r > 0.05f)
            {
                DonorMaxRadiusM = r;
                DonorCaptured = true;
            }

            if (MaxDrag?.GetValue(donor) is float d && d > 0.1f)
                _donorMaxDrag = d;
            if (LineSpring?.GetValue(donor) is float s && s > 0.1f)
                _donorLineSpring = s;
            if (Damping?.GetValue(donor) is float damp && damp > 0.01f)
                _donorDamping = damp;
            if (CanopyMass?.GetValue(donor) is float cm && cm > 0.1f)
                _donorCanopyMass = cm;

            HydraPlugin.ModLog?.LogInfo(
                $"MK54 chute donor maxRadius={DonorMaxRadiusM:F2}m → use {TargetMaxRadiusM:F2}m (×{TorpedoConstants.ChuteRadiusScaleFromDonor:F0}) drag={_donorMaxDrag:F1}");
        }

        internal static float TargetMaxRadiusM =>
            DonorMaxRadiusM * TorpedoConstants.ChuteRadiusScaleFromDonor;

        internal static void TuneForMk54(Parachute chute, float bodyMassKg)
        {
            if (chute == null)
                return;

            // Exact GPO-N × 3 — never absolute 4m, never mass-scaled radius.
            float radius = TargetMaxRadiusM;
            MaxRadius?.SetValue(chute, radius);

            // Fabric feel: donor drag/spring (mild mass bump on drag only).
            float massScale = Mathf.Clamp(bodyMassKg / TorpedoConstants.ShellAeroMassKg, 1f, 8f);
            float drag = _donorMaxDrag * Mathf.Lerp(1f, massScale, 0.35f);
            MaxDrag?.SetValue(chute, drag);
            LineSpring?.SetValue(chute, _donorLineSpring);
            // damping scales hang AddTorque — 0 so canopy fabric moves without spinning the hull
            Damping?.SetValue(chute, 0f);
            CanopyMass?.SetValue(chute, _donorCanopyMass);

            OpenAltMin?.SetValue(chute, TorpedoConstants.ChuteOpenAltitudeMinM);
            OpenAltMax?.SetValue(chute, TorpedoConstants.ChuteOpenAltitudeMaxM);
            OpenDelayMin?.SetValue(chute, TorpedoConstants.ChuteOpenDelayMinS);
            OpenSpeedMin?.SetValue(chute, 4f);
            OpenSpeedMax?.SetValue(chute, 400f);

            HydraPlugin.ModLog?.LogInfo(
                $"MK54 chute tune radius={radius:F2}m donorCaptured={DonorCaptured} drag={drag:F1} spring={_donorLineSpring:F0}");
        }

        internal static void PrepareMissileBody(Missile missile)
        {
            if (missile?.rb == null)
                return;
            Rigidbody rb = missile.rb;
            rb.useGravity = true;
            rb.detectCollisions = false;
            rb.drag = TorpedoConstants.ChuteBodyDrag;
            // Soft — allow vanilla parachute hang torque (fabric), not concrete lock.
            rb.angularDrag = TorpedoConstants.ChuteBodyAngularDrag;
        }

        internal static void RestoreAfterChute(Missile missile)
        {
            if (missile?.rb == null)
                return;
            missile.rb.detectCollisions = false;
        }
    }
}
