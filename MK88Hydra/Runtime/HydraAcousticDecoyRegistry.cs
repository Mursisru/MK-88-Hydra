using System;

namespace Hydra.Runtime
{
    /// <summary>Carrier hydroacoustic trap counts (matched on unitName/jsonKey).</summary>
    internal static class HydraAcousticDecoyRegistry
    {
        internal static int ResolveTrapCount(UnitDefinition? def)
        {
            if (def == null)
                return 0;

            string key = (def.unitName + " " + def.jsonKey).ToLowerInvariant();
            if (key.Contains("annex"))
                return TorpedoConstants.DecoyCountAnnex;
            if (key.Contains("hyperion") || key.Contains("hypryon"))
                return TorpedoConstants.DecoyCountHyperion;
            if (key.Contains("dynamo"))
                return TorpedoConstants.DecoyCountDynamo;
            if (key.Contains("argus"))
                return TorpedoConstants.DecoyCountArgus;
            if (key.Contains("shard"))
                return TorpedoConstants.DecoyCountShard;
            if (key.Contains("cursor") && !key.Contains("override"))
                return TorpedoConstants.DecoyCountCursor;
            return 0;
        }
    }
}
