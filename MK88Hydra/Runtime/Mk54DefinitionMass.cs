using System.Reflection;

namespace Hydra.Runtime
{
    /// <summary>MissileDefinition hides UnitDefinition.mass with private float? — GetMass ignores base field.</summary>
    internal static class Mk54DefinitionMass
    {
        private static readonly FieldInfo? NullableMass =
            typeof(MissileDefinition).GetField("mass",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        internal static void Apply(MissileDefinition? def, float kg)
        {
            if (def == null || kg <= 0f)
                return;
            NullableMass?.SetValue(def, kg);
        }
    }
}
