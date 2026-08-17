namespace Hydra.Runtime
{
    /// <summary>Vanilla HUD/fire-control range = glide shell air range + swim fuel.</summary>
    internal static class TorpedoWeaponRange
    {
        internal static float AirGlideRangeM { get; private set; } = TorpedoConstants.FallbackAirGlideRangeM;

        internal static void Apply(WeaponInfo info, WeaponInfo shellInfo)
        {
            if (info == null || shellInfo == null)
                return;

            AirGlideRangeM = ReadAirRange(shellInfo);
            TargetRequirements tr = info.targetRequirements;
            tr.maxRange = AirGlideRangeM + TorpedoConstants.SwimFuelRangeM;
            info.targetRequirements = tr;
        }

        internal static float CombinedMaxRange(WeaponInfo shellInfo)
        {
            AirGlideRangeM = ReadAirRange(shellInfo);
            return AirGlideRangeM + TorpedoConstants.SwimFuelRangeM;
        }

        internal static float ReadAirRange(WeaponInfo shellInfo)
        {
            float airRange = shellInfo.targetRequirements.maxRange;
            if (airRange <= 0f)
                airRange = TorpedoConstants.FallbackAirGlideRangeM;
            return airRange;
        }
    }
}
