using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hydra.Bootstrap
{
    /// <summary>
    /// ADD-ONLY inject into Darkreach / Alkyon hardpoints that already list HE Piledriver.
    /// Single slots → single torpedo mount; internalx2/double slots → double mount.
    /// </summary>
    internal static class HardpointInjector
    {
        internal static void InjectPiledriverSlots(
            Encyclopedia enc,
            WeaponMount singleMount,
            WeaponMount? doubleMount)
        {
            if (enc == null || singleMount == null)
                return;

            if (singleMount.jsonKey != null &&
                singleMount.jsonKey.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
            {
                HydraPlugin.ModLog?.LogError("Refusing inject: torpedo mount still has BallisticMissile1 jsonKey.");
                return;
            }

            if (doubleMount != null &&
                doubleMount.jsonKey != null &&
                doubleMount.jsonKey.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
            {
                HydraPlugin.ModLog?.LogError("Refusing inject: double torpedo mount still has BallisticMissile1 jsonKey.");
                return;
            }

            int injected = 0;
            injected += InjectOnAircraft(enc, TorpedoConstants.CarrierDarkreach, singleMount, doubleMount);
            injected += InjectOnAircraft(enc, TorpedoConstants.CarrierAlkyon, singleMount, doubleMount);

            HydraPlugin.ModLog?.LogInfo(
                $"HardpointInjector: added torpedo mount(s) to {injected} hardpoint set(s) (add-only).");
        }

        private static int InjectOnAircraft(
            Encyclopedia enc,
            string jsonKey,
            WeaponMount singleMount,
            WeaponMount? doubleMount)
        {
            AircraftDefinition? ad = FindAircraft(enc, jsonKey);
            if (ad?.unitPrefab == null)
            {
                HydraPlugin.ModLog?.LogWarning($"Carrier '{jsonKey}' not found.");
                return 0;
            }
            return InjectWhereHePiledriverPresent(ad.unitPrefab, singleMount, doubleMount);
        }

        private static AircraftDefinition? FindAircraft(Encyclopedia enc, string jsonKey)
        {
            if (Encyclopedia.Lookup != null &&
                Encyclopedia.Lookup.TryGetValue(jsonKey, out UnitDefinition u) &&
                u is AircraftDefinition ad)
                return ad;

            if (enc.aircraft == null)
                return null;
            foreach (AircraftDefinition a in enc.aircraft)
            {
                if (a != null && string.Equals(a.jsonKey, jsonKey, StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return null;
        }

        private static int InjectWhereHePiledriverPresent(
            GameObject aircraftPrefab,
            WeaponMount singleMount,
            WeaponMount? doubleMount)
        {
            int count = 0;
            WeaponManager[] managers = aircraftPrefab.GetComponentsInChildren<WeaponManager>(true);
            foreach (WeaponManager wm in managers)
            {
                if (wm?.hardpointSets == null)
                    continue;
                foreach (HardpointSet set in wm.hardpointSets)
                {
                    if (set == null)
                        continue;
                    set.weaponOptions ??= new List<WeaponMount>();
                    if (!HasHePiledriverOption(set.weaponOptions))
                        continue;

                    if (HasHePiledriverDouble(set.weaponOptions) && doubleMount != null)
                    {
                        if (!ContainsRef(set.weaponOptions, doubleMount))
                        {
                            set.weaponOptions.Add(doubleMount);
                            count++;
                        }
                    }

                    if (HasHePiledriverSingle(set.weaponOptions))
                    {
                        if (!ContainsRef(set.weaponOptions, singleMount))
                        {
                            set.weaponOptions.Add(singleMount);
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private static bool HasHePiledriverOption(List<WeaponMount> options)
        {
            foreach (WeaponMount o in options)
            {
                if (IsHePiledriver(o))
                    return true;
            }
            return false;
        }

        private static bool HasHePiledriverSingle(List<WeaponMount> options)
        {
            foreach (WeaponMount o in options)
            {
                if (!IsHePiledriver(o))
                    continue;
                if (IsDoubleSlotKey(o.jsonKey))
                    continue;
                return true;
            }
            return false;
        }

        private static bool HasHePiledriverDouble(List<WeaponMount> options)
        {
            foreach (WeaponMount o in options)
            {
                if (!IsHePiledriver(o))
                    continue;
                if (IsDoubleSlotKey(o.jsonKey) || o.ammo >= 2)
                    return true;
            }
            return false;
        }

        private static bool IsHePiledriver(WeaponMount? o)
        {
            if (o == null || string.IsNullOrEmpty(o.jsonKey))
                return false;
            if (!o.jsonKey.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
                return false;
            return o.jsonKey.IndexOf(TorpedoConstants.PiledriverNukeToken, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsDoubleSlotKey(string? jsonKey)
        {
            if (jsonKey == null || jsonKey.Length == 0)
                return false;
            if (jsonKey.IndexOf("internalx2", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return jsonKey.IndexOf("double", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   jsonKey.IndexOf("single", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool ContainsRef(List<WeaponMount> options, WeaponMount? mount)
        {
            if (mount == null || string.IsNullOrEmpty(mount.jsonKey))
                return false;
            for (int i = 0; i < options.Count; i++)
            {
                if (ReferenceEquals(options[i], mount))
                    return true;
                if (options[i] != null && string.Equals(options[i].jsonKey, mount.jsonKey, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
