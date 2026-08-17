using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Phase mass: launch 2350kg → after shed 2000kg core.</summary>
    internal static class Mk54Mass
    {
        private static readonly FieldInfo? MassField =
            typeof(Missile).GetField("mass", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void ApplyLaunch(Missile missile) =>
            Set(missile, TorpedoConstants.LaunchMassKg);

        internal static void ApplyCore(Missile missile) =>
            Set(missile, TorpedoConstants.TorpedoCoreMassKg);

        internal static void Set(Missile missile, float kg)
        {
            if (missile == null || kg <= 0f)
                return;

            MassField?.SetValue(missile, kg);
            Rigidbody? rb = missile.rb != null ? missile.rb : missile.GetComponent<Rigidbody>();
            if (rb != null)
                rb.mass = kg;
        }
    }
}
