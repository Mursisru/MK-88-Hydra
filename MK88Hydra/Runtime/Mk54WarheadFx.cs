using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// bomb_glide1 underwaterEffect lacks Shockwave for yield&gt;200 — stamp TBM FX so vanilla Warhead.Detonate
    /// applies the same Shockwave damage underwater as above water.
    /// </summary>
    internal static class Mk54WarheadFx
    {
        private static readonly FieldInfo? WarheadField =
            typeof(Missile).GetField("warhead", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? AirEffectField =
            typeof(Missile.Warhead).GetField("airEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? ArmorEffectField =
            typeof(Missile.Warhead).GetField("armorEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? TerrainEffectField =
            typeof(Missile.Warhead).GetField("terrainEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? WaterSurfaceEffectField =
            typeof(Missile.Warhead).GetField("waterSurfaceEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? UnderwaterEffectField =
            typeof(Missile.Warhead).GetField("underwaterEffect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static GameObject? _air;
        private static GameObject? _armor;
        private static GameObject? _terrain;
        private static GameObject? _water;
        private static bool _captured;

        internal static void CaptureTbm(Encyclopedia enc)
        {
            if (_captured || enc?.missiles == null)
                return;

            Missile? donor = null;
            int best = -1;
            for (int i = 0; i < enc.missiles.Count; i++)
            {
                MissileDefinition? def = enc.missiles[i];
                if (def?.unitPrefab == null || string.IsNullOrEmpty(def.jsonKey))
                    continue;
                string k = def.jsonKey;
                int s = 0;
                if (k.Equals("BallisticMissile1", System.StringComparison.OrdinalIgnoreCase))
                    s = 100;
                else if (k.StartsWith("BallisticMissile1", System.StringComparison.OrdinalIgnoreCase))
                    s = 80;
                else if (k.IndexOf("BallisticMissile", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    s = 40;
                if (s <= best)
                    continue;
                Missile? m = def.unitPrefab.GetComponent<Missile>()
                             ?? def.unitPrefab.GetComponentInChildren<Missile>(true);
                if (m == null)
                    continue;
                best = s;
                donor = m;
            }

            if (donor == null || WarheadField?.GetValue(donor) is not Missile.Warhead wh)
            {
                HydraPlugin.ModLog?.LogWarning("MK54 warhead FX: no TBM donor.");
                return;
            }

            _air = AirEffectField?.GetValue(wh) as GameObject;
            _armor = ArmorEffectField?.GetValue(wh) as GameObject;
            _terrain = TerrainEffectField?.GetValue(wh) as GameObject;
            _water = WaterSurfaceEffectField?.GetValue(wh) as GameObject;
            _captured = _air != null && _air.GetComponentInChildren<Shockwave>(true) != null;

            HydraPlugin.ModLog?.LogInfo(
                $"MK54 warhead FX TBM air={(_air != null)} shockwave={_captured}");
        }

        internal static void Ensure(Missile missile)
        {
            if (missile == null || !_captured || WarheadField == null)
                return;
            if (WarheadField.GetValue(missile) is not Missile.Warhead wh)
                return;

            if (_air != null)
            {
                AirEffectField?.SetValue(wh, _air);
                // UW path uses underwaterEffect first — point at same Shockwave prefab as air.
                UnderwaterEffectField?.SetValue(wh, _air);
            }
            if (_armor != null)
                ArmorEffectField?.SetValue(wh, _armor);
            if (_terrain != null)
                TerrainEffectField?.SetValue(wh, _terrain);
            if (_water != null)
                WaterSurfaceEffectField?.SetValue(wh, _water);
        }
    }
}
