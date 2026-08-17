namespace Hydra.Runtime
{
    /// <summary>
    /// MK54 Detonate is allowed only:
    /// - Allow: our hull Probe / SoftKill
    /// - CombatDepth: Missile.TakeDamage from a real attacker (not SlowChecks / slam)
    /// </summary>
    internal static class Mk54DetonateGate
    {
        internal static bool Allow;
        internal static int CombatDepth;
    }
}
