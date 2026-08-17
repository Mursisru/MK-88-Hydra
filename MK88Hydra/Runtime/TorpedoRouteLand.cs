using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Water = volume under LocalSeaY above map. Land = mapY ≥ sea. Used for entry/beach only.
    /// </summary>
    internal static class TorpedoRouteLand
    {
        private const float ProbeTopY = 8000f;
        private const float ProbeDepth = 16000f;
        private static readonly RaycastHit[] Hits = new RaycastHit[48];
        private static readonly int MapMask = PhysicsLayers.StaticsMask.value;

        internal static bool TryMapY(float x, float z, out float mapY)
        {
            mapY = 0f;
            int n = Physics.RaycastNonAlloc(
                new Vector3(x, ProbeTopY, z),
                Vector3.down,
                Hits,
                ProbeDepth,
                MapMask,
                QueryTriggerInteraction.Ignore);
            if (n <= 0)
                return false;

            float best = float.MinValue;
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].collider == null)
                    continue;
                float y = Hits[i].point.y;
                if (y > best)
                {
                    best = y;
                    any = true;
                }
            }
            if (!any)
                return false;
            mapY = best;
            return true;
        }

        internal static bool IsDeepWater(float x, float z)
        {
            float sea = Datum.LocalSeaY;
            float slack = TorpedoConstants.RouteShoreSlackM;
            float need = TorpedoConstants.SwimDepthM + TorpedoConstants.RouteMinKeelClearM;

            if (!TryMapY(x, z, out float mapY))
                return true;
            if (mapY >= sea - slack)
                return false;
            return (sea - mapY) >= need;
        }

        internal static bool IsLandOrShallow(float x, float z) => !IsDeepWater(x, z);

        internal static bool IsMapLand(float x, float z)
        {
            float sea = Datum.LocalSeaY;
            if (!TryMapY(x, z, out float mapY))
                return false;
            return mapY >= sea - TorpedoConstants.RouteShoreSlackM;
        }
    }
}
