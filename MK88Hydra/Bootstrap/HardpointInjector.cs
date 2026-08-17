using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hydra.Bootstrap
{
    /// <summary>
    /// ADD-ONLY inject into Darkreach / Alkyon hardpoints that already list HE Piledriver.
    /// Never removes/replaces BallisticMissile1* options. Ignores tacNuke-only sets unless HE is also present.
    /// </summary>
    internal static class HardpointInjector
    {
        internal static void InjectPiledriverSlots(Encyclopedia enc, WeaponMount torpedoMount)
        {
            if (enc == null || torpedoMount == null)
                return;

            // Guard: never inject a mount that still carries a vanilla TBM key
            if (torpedoMount.jsonKey != null &&
                torpedoMount.jsonKey.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
            {
                HydraPlugin.ModLog?.LogError("Refusing inject: torpedo mount still has BallisticMissile1 jsonKey.");
                return;
            }

            int injected = 0;
            injected += InjectOnAircraft(enc, TorpedoConstants.CarrierDarkreach, torpedoMount);
            injected += InjectOnAircraft(enc, TorpedoConstants.CarrierAlkyon, torpedoMount);

            HydraPlugin.ModLog?.LogInfo(
                $"HardpointInjector: added '{torpedoMount.mountName}' to {injected} hardpoint set(s) (add-only).");
        }

        private static int InjectOnAircraft(Encyclopedia enc, string jsonKey, WeaponMount mount)
        {
            AircraftDefinition? ad = FindAircraft(enc, jsonKey);
            if (ad?.unitPrefab == null)
            {
                HydraPlugin.ModLog?.LogWarning($"Carrier '{jsonKey}' not found.");
                return 0;
            }
            return InjectWhereHePiledriverPresent(ad.unitPrefab, mount);
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

        private static int InjectWhereHePiledriverPresent(GameObject aircraftPrefab, WeaponMount mount)
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
                    if (ContainsRef(set.weaponOptions, mount))
                        continue;
                    set.weaponOptions.Add(mount);
                    count++;
                }
            }
            return count;
        }

        private static bool HasHePiledriverOption(List<WeaponMount> options)
        {
            foreach (WeaponMount o in options)
            {
                if (o == null || string.IsNullOrEmpty(o.jsonKey))
                    continue;
                // HE only — do not treat tacNuke-only as the slot marker for mutation risk reporting
                if (!o.jsonKey.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (o.jsonKey.IndexOf(TorpedoConstants.PiledriverNukeToken, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                return true;
            }
            return false;
        }

        private static bool ContainsRef(List<WeaponMount> options, WeaponMount mount)
        {
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
