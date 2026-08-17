using System;
using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Glide-bomb shell is Armed by default and OpticalSeekerBomb can fuse.
    /// More critically: mutating networked hierarchy (SetParent/Instantiate NI)
    /// after spawn unloads GameWorld → MainMenu. Keep spawn path hierarchy-safe.
    /// </summary>
    internal static class SafeSeparation
    {
        private static readonly FieldInfo? WarheadField =
            typeof(Missile).GetField("warhead", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? ArmedField =
            typeof(Missile.Warhead).GetField("Armed", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo? ImpactFuseField =
            typeof(Missile).GetField("impactFuse", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Prepare(Missile missile)
        {
            if (missile == null)
                return;

            try
            {
                missile.SetTangible(false);

                object? wh = WarheadField?.GetValue(missile);
                if (wh != null && ArmedField != null)
                    ArmedField.SetValue(wh, false);

                ImpactFuseField?.SetValue(missile, false);

                // Kill bomb seeker logic — RC/FPV also patch Seek; we steer via phase controller later.
                MissileSeeker[] seekers = missile.GetComponentsInChildren<MissileSeeker>(true);
                for (int i = 0; i < seekers.Length; i++)
                {
                    if (seekers[i] != null)
                        seekers[i].enabled = false;
                }

                StretchBombSeeker(missile);
                HydraPlugin.ModLog?.LogInfo(
                    $"SafeSeparation OK seekersOff={seekers.Length} armed={missile.IsArmed()} tangible={missile.IsTangible()}");
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogError($"SafeSeparation FAILED: {ex}");
            }
        }

        private static void StretchBombSeeker(Missile missile)
        {
            OpticalSeekerBomb? bomb = missile.GetComponent<OpticalSeekerBomb>() ??
                                      missile.GetComponentInChildren<OpticalSeekerBomb>(true);
            if (bomb == null)
                return;

            SetPrivateFloat(bomb, "tangibleDelay", 5f);
            SetPrivateFloat(bomb, "armDelay", 9999f);
            SetPrivateFloat(bomb, "guidanceDelay", 5f);
            SetPrivateFloat(bomb, "altitudeFuseHeight", 0f);
        }

        private static void SetPrivateFloat(object obj, string name, float value)
        {
            FieldInfo? f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
                f.SetValue(obj, value);
        }
    }
}
