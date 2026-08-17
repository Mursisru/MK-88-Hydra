using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Neutralize bomb_glide1 fuses/seeker side-effects on live MK54 rounds.</summary>
    internal static class Mk54ShellPrep
    {
        private static readonly FieldInfo? ImpactFuseField =
            typeof(Missile).GetField("impactFuse", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? WarheadField =
            typeof(Missile).GetField("warhead", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? BlastYieldField =
            typeof(Missile).GetField("blastYield", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Prepare(Missile missile)
        {
            if (missile == null)
                return;

            ImpactFuseField?.SetValue(missile, false);
            EnsureBlastYield(missile);
            Disarm(missile);
        }

        internal static void EnsureBlastYield(Missile missile)
        {
            if (missile == null)
                return;
            BlastYieldField?.SetValue(missile, TorpedoConstants.BlastYieldKg);
        }

        internal static void Disarm(Missile missile)
        {
            if (missile == null)
                return;
            if (WarheadField?.GetValue(missile) is Missile.Warhead wh)
                wh.Armed = false;
        }

        internal static void ArmForStrike(Missile missile)
        {
            if (missile == null)
                return;
            if (WarheadField?.GetValue(missile) is Missile.Warhead wh)
                wh.Armed = true;
        }
    }
}
