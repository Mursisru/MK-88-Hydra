using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hydra.Blueprinter;
using Hydra.Patches;
using Hydra.Runtime;
using UnityEngine;

namespace Hydra.Bootstrap
{
    /// <summary>
    /// Builds a NEW MK54 munition (CreateInstance + GO clones).
    /// Never Instantiates or mutates Piledriver / BallisticMissile1* encyclopedia assets.
    /// Physics shell = guided bomb (Down-rail) only as Mirage-compatible host.
    /// </summary>
    internal static class TorpedoBootstrap
    {
        private static bool _done;
        internal static MissileDefinition? TorpedoDefinition { get; private set; }
        internal static WeaponMount? TorpedoMount { get; private set; }
        internal static WeaponMount? TorpedoMountDouble { get; private set; }
        internal static WeaponInfo? TorpedoInfo { get; private set; }

        private static readonly FieldInfo? UnitDisabled =
            typeof(UnitDefinition).GetField("disabled", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? MountDisabled =
            typeof(WeaponMount).GetField("disabled", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static IEnumerator Run(Encyclopedia enc)
        {
            if (_done || enc == null)
                yield break;

            yield return BlueprinterGate.WaitUntilReady();

            try
            {
                PrefabFactory.AssertPiledriverIntact(enc);
                NobpContent.TryLoad();
                Mk54WarheadFx.CaptureTbm(enc);

                if (Encyclopedia.Lookup != null &&
                    Encyclopedia.Lookup.TryGetValue(TorpedoConstants.MissileJsonKey, out UnitDefinition existing) &&
                    existing is MissileDefinition md && md.unitPrefab != null)
                {
                    TorpedoDefinition = md;
                }
                else
                {
                    TorpedoDefinition = CreateMissileDefinition(enc);
                }

                BindShellPrefab(enc, TorpedoDefinition);
                Mk54DefinitionMass.Apply(TorpedoDefinition, TorpedoConstants.LaunchMassKg);

                MissileDefinition? shellMissile = ResolveShellMissile(enc);
                if (shellMissile?.unitPrefab != null)
                    VisualMaterials.PrimeShaderFrom(shellMissile.unitPrefab);
                else if (TorpedoDefinition?.unitPrefab != null)
                    VisualMaterials.PrimeShaderFrom(TorpedoDefinition.unitPrefab);

                if (Encyclopedia.WeaponLookup != null &&
                    Encyclopedia.WeaponLookup.TryGetValue(TorpedoConstants.MountJsonKey, out WeaponMount existingMount) &&
                    existingMount.prefab != null &&
                    !IsVanillaKey(existingMount.jsonKey))
                {
                    TorpedoMount = existingMount;
                    RefreshMount(enc, TorpedoMount, TorpedoDefinition);
                }
                else
                {
                    TorpedoMount = CreateWeaponMount(enc, TorpedoDefinition);
                    TorpedoInfo = TorpedoMount?.info;
                }

                if (TorpedoMount != null)
                {
                    if (TorpedoDefinition?.unitPrefab != null && TorpedoMount.info != null)
                        TorpedoMount.info.weaponPrefab = TorpedoDefinition.unitPrefab;

                    TorpedoMountDouble = EnsureDoubleMount(enc, TorpedoDefinition, TorpedoMount);
                    ShareWeaponInfoAcrossMounts();
                    HardpointInjector.InjectPiledriverSlots(enc, TorpedoMount, TorpedoMountDouble);
                }

                PrefabFactory.AssertPiledriverIntact(enc);
                ParachuteDonor.Cache(enc);

                _done = TorpedoDefinition != null && TorpedoMount != null;
                if (TorpedoInfo != null)
                {
                    WeaponMount? sm = ResolveShellMount(enc);
                    if (sm?.info != null)
                    {
                        float r = TorpedoWeaponRange.CombinedMaxRange(sm.info);
                        HydraPlugin.ModLog?.LogInfo($"MK54 maxRange={r / 1000f:F0}km (air+swim)");
                    }
                }
                HydraPlugin.ModLog?.LogInfo(_done
                    ? $"Torpedo ready: def={TorpedoConstants.MissileJsonKey} mount={TorpedoConstants.MountJsonKey} visual={(NobpContent.VisualPrefab != null)}"
                    : "Torpedo bootstrap incomplete.");
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogError($"TorpedoBootstrap: {ex}");
            }
        }

        private static void BindShellPrefab(Encyclopedia enc, MissileDefinition? def)
        {
            if (def == null)
                return;
            MissileDefinition? shell = ResolveShellMissile(enc);
            if (shell?.unitPrefab == null)
                return;
            def.unitPrefab = shell.unitPrefab;
        }

        private static bool IsVanillaKey(string? key)
        {
            if (key == null || key.Length == 0)
                return false;
            string k = key;
            return k.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase) ||
                   k.StartsWith("ballisticMissile1", StringComparison.OrdinalIgnoreCase);
        }

        private static MissileDefinition? CreateMissileDefinition(Encyclopedia enc)
        {
            MissileDefinition? shell = ResolveShellMissile(enc);
            if (shell?.unitPrefab == null)
            {
                HydraPlugin.ModLog?.LogError("No glide/bomb shell MissileDefinition.");
                return null;
            }

            // NEW SO — never Object.Instantiate(encyclopedia asset)
            MissileDefinition def = ScriptableObject.CreateInstance<MissileDefinition>();
            def.name = "MissilePack_MK54_Definition";
            def.jsonKey = TorpedoConstants.MissileJsonKey;
            PrefabFactory.CopyUnitDefScalars(shell, def);
            PrefabFactory.CopyMapIdentity(shell, def);
            def.unitName = TorpedoConstants.UnitName;
            def.bogeyName = TorpedoConstants.BogeyName;
            def.description = "Air-dropped anti-ship torpedo. Glide, shed cover, parachute, sonar run. 450kg warhead.";
            def.value = TorpedoConstants.Cost;
            def.mass = TorpedoConstants.MassKg;
            def.length = TorpedoConstants.LengthM;
            def.width = TorpedoConstants.WidthM;
            def.height = TorpedoConstants.HeightM;
            def.radarSize = TorpedoConstants.RadarSize;
            def.code = "MSL";
            def.IsObstacle = false;
            UnitDisabled?.SetValue(def, false);

            // Yashma/FPV: reuse the already-registered vanilla prefab. Runtime
            // Instantiate+DontDestroyOnLoad of a NetworkIdentity is a scene object
            // and Mirage destroys the clone on spawn (NetID 0 already spawned).
            def.unitPrefab = shell.unitPrefab;

            enc.missiles ??= new List<MissileDefinition>();
            if (!enc.missiles.Contains(def))
                enc.missiles.Add(def);

            Encyclopedia.Lookup ??= new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            Encyclopedia.Lookup[def.jsonKey] = def;

            if (enc.IndexLookup != null && !ContainsNet(enc.IndexLookup, def))
            {
                enc.IndexLookup.Add(def);
                ((INetworkDefinition)def).LookupIndex = enc.IndexLookup.Count - 1;
            }

            Mk54DefinitionMass.Apply(def, TorpedoConstants.LaunchMassKg);

            HydraPlugin.ModLog?.LogInfo(
                $"Created MissileDefinition from shell '{shell.jsonKey}' (unitPrefab shared, read-only).");
            return def;
        }

        private static void RefreshMount(
            Encyclopedia enc,
            WeaponMount? mount,
            MissileDefinition? def,
            int ammo = 1,
            bool keepDualWeapons = false)
        {
            if (mount == null)
                return;

            NobpContent.TryLoad();
            if (mount.prefab != null && NobpContent.VisualPrefab != null)
                PrefabFactory.StampVisual(mount.prefab, NobpContent.VisualPrefab);

            WeaponInfo? info = TorpedoInfo;
            if (info == null)
            {
                info = mount.info;
                if (info == null)
                {
                    info = ScriptableObject.CreateInstance<WeaponInfo>();
                    info.name = "MissilePack_MK54_Info";
                }
            }

            WeaponMount? shellMount = keepDualWeapons ? ResolveDoubleShellMount(enc) : ResolveShellMount(enc);
            shellMount ??= ResolveShellMount(enc);
            if (shellMount?.info != null)
            {
                PrefabFactory.CopyWeaponInfoScalars(shellMount.info, info);
                TorpedoWeaponRange.Apply(info, shellMount.info);
                Mk54AiEmployment.ApplyProfile(info);
            }

            Sprite? preview = Hydra.Runtime.HydraWeaponIcon.Get();
            if (preview != null)
                info.weaponIcon = preview;

            info.weaponName = TorpedoConstants.WeaponInfoName;
            info.shortName = TorpedoConstants.ShortName;
            info.description = "Air-dropped anti-ship torpedo. Sonar-guided, 450kg warhead.";
            info.massPerRound = TorpedoConstants.LaunchMassKg;
            info.costPerRound = TorpedoConstants.Cost;
            info.blastDamage = TorpedoConstants.BlastYieldKg;
            info.nuclear = false;
            // bomb=false → MissileUI (not free-fall CCIP). glideBomb=true → planning weapon.
            // Free-fall only after kozuch shed (phase Ballistic), not HUD type.
            info.bomb = false;
            info.glideBomb = true;
            info.missile = false;
            Mk54AiEmployment.ApplyProfile(info);
            if (def?.unitPrefab != null)
                info.weaponPrefab = def.unitPrefab;

            mount.info = info;
            mount.ammo = ammo;
            mount.mountName = ammo >= 2
                ? TorpedoConstants.MountDisplayName + " x2"
                : TorpedoConstants.MountDisplayName;
            mount.mass = mount.emptyMass + TorpedoConstants.LaunchMassKg * ammo;
            mount.RCS = TorpedoConstants.RadarSize;
            mount.emptyRCS = 0f;
            mount.emptyCost = 0f;
            mount.GearSafety = true;
            mount.GroundSafety = true;
            MountDisabled?.SetValue(mount, false);

            if (mount.prefab != null)
            {
                if (keepDualWeapons)
                    EnsureDualMountedWeapons(mount.prefab);
                else
                    KeepSingleMountedWeapon(mount.prefab);

                foreach (MountedMissile mm in mount.prefab.GetComponentsInChildren<MountedMissile>(true))
                {
                    if (mm != null)
                        mm.info = info;
                }
            }

            TorpedoInfo = info;
            HydraPlugin.ModLog?.LogInfo(
                $"Refreshed MK54 mount x{ammo} visual={(NobpContent.VisualPrefab != null)} mass={mount.mass:F0}kg");
        }

        /// <summary>
        /// WeaponManager stacks stations by WeaponInfo reference equality.
        /// Single + double mounts must share one SO so pylons cycle as one station.
        /// </summary>
        private static void ShareWeaponInfoAcrossMounts()
        {
            WeaponInfo? shared = TorpedoMount?.info ?? TorpedoMountDouble?.info ?? TorpedoInfo;
            if (shared == null)
                return;

            TorpedoInfo = shared;
            ApplySharedInfo(TorpedoMount, shared);
            ApplySharedInfo(TorpedoMountDouble, shared);
        }

        private static void ApplySharedInfo(WeaponMount? mount, WeaponInfo shared)
        {
            if (mount == null)
                return;

            mount.info = shared;
            mount.sortWeapons = true;
            if (mount.prefab == null)
                return;

            foreach (MountedMissile mm in mount.prefab.GetComponentsInChildren<MountedMissile>(true))
            {
                if (mm != null)
                    mm.info = shared;
            }
        }

        private static WeaponMount? EnsureDoubleMount(
            Encyclopedia enc,
            MissileDefinition? missileDef,
            WeaponMount singleMount)
        {
            if (missileDef?.unitPrefab == null || singleMount?.info == null)
                return null;

            if (Encyclopedia.WeaponLookup != null &&
                Encyclopedia.WeaponLookup.TryGetValue(TorpedoConstants.MountJsonKeyDouble, out WeaponMount existing) &&
                existing.prefab != null &&
                !IsVanillaKey(existing.jsonKey))
            {
                existing.info = singleMount.info;
                RefreshMount(enc, existing, missileDef, ammo: 2, keepDualWeapons: true);
                existing.info = singleMount.info;
                ApplySharedInfo(existing, singleMount.info);
                return existing;
            }

            WeaponMount? shellDouble = ResolveDoubleShellMount(enc);
            if (shellDouble?.prefab == null || shellDouble.info == null)
            {
                HydraPlugin.ModLog?.LogWarning("No HE Piledriver x2 shell mount — double torpedo mount skipped.");
                return null;
            }

            WeaponMount? created = CreateWeaponMount(
                enc, missileDef, shellDouble, TorpedoConstants.MountJsonKeyDouble, ammo: 2, keepDualWeapons: true);
            if (created != null && singleMount.info != null)
            {
                created.info = singleMount.info;
                ApplySharedInfo(created, singleMount.info);
                TorpedoInfo = singleMount.info;
            }
            return created;
        }

        private static WeaponMount? CreateWeaponMount(Encyclopedia enc, MissileDefinition? missileDef)
        {
            WeaponMount? shellMount = ResolveShellMount(enc);
            if (shellMount?.prefab == null || shellMount.info == null)
            {
                HydraPlugin.ModLog?.LogError("No glide/bomb shell WeaponMount.");
                return null;
            }

            return CreateWeaponMount(
                enc,
                missileDef,
                shellMount,
                TorpedoConstants.MountJsonKey,
                ammo: 1,
                keepDualWeapons: false);
        }

        private static WeaponMount? CreateWeaponMount(
            Encyclopedia enc,
            MissileDefinition? missileDef,
            WeaponMount shellMount,
            string mountJsonKey,
            int ammo,
            bool keepDualWeapons)
        {
            if (missileDef?.unitPrefab == null)
                return null;

            // Snapshot shell fields BEFORE creating anything — never write to shellMount
            string shellKey = shellMount.jsonKey;
            GameObject shellPrefab = shellMount.prefab;
            WeaponInfo shellInfo = shellMount.info;

            WeaponMount mount = ScriptableObject.CreateInstance<WeaponMount>();
            mount.name = mountJsonKey.IndexOf("double", StringComparison.OrdinalIgnoreCase) >= 0
                ? "MissilePack_MK54_Mount_Double"
                : "MissilePack_MK54_Mount";
            mount.jsonKey = mountJsonKey;
            mount.mountName = ammo >= 2
                ? TorpedoConstants.MountDisplayName + " x2"
                : TorpedoConstants.MountDisplayName;
            PrefabFactory.CopyMountScalars(shellMount, mount);
            mount.ammo = ammo;
            mount.missileBay = shellMount.missileBay;
            mount.emptyMass = 25f;
            mount.mass = mount.emptyMass + TorpedoConstants.LaunchMassKg * ammo;
            mount.RCS = TorpedoConstants.RadarSize;
            mount.emptyRCS = 0f;
            mount.emptyCost = 0f;
            mount.GearSafety = true;
            mount.GroundSafety = true;
            MountDisabled?.SetValue(mount, false);

            WeaponInfo info = TorpedoInfo ?? ScriptableObject.CreateInstance<WeaponInfo>();
            if (TorpedoInfo == null)
                info.name = "MissilePack_MK54_Info";
            PrefabFactory.CopyWeaponInfoScalars(shellInfo, info);
            Sprite? preview = Hydra.Runtime.HydraWeaponIcon.Get();
            if (preview != null)
                info.weaponIcon = preview;
            info.weaponName = TorpedoConstants.WeaponInfoName;
            info.shortName = TorpedoConstants.ShortName;
            info.description = "Air-dropped anti-ship torpedo. Sonar-guided, 450kg warhead.";
            info.weaponPrefab = missileDef.unitPrefab;
            info.massPerRound = TorpedoConstants.MassKg;
            info.costPerRound = TorpedoConstants.Cost;
            info.pK = 0.65f;
            info.blastDamage = TorpedoConstants.BlastYieldKg;
            info.nuclear = false;
            info.strategic = false;
            info.hideInDisplay = false;
            info.sling = false;
            info.cargo = false;
            info.troops = false;
            // bomb=false → MissileUI (glide/lock), not free-fall CCIP BombingUI.
            info.bomb = false;
            info.glideBomb = true;
            info.missile = false;
            TorpedoWeaponRange.Apply(info, shellInfo);
            Mk54AiEmployment.ApplyProfile(info);
            mount.info = info;

            GameObject mountGo = PrefabFactory.CloneAsPrefab(
                shellPrefab,
                keepDualWeapons ? "MissilePack_MK54_MountPrefab_Double" : "MissilePack_MK54_MountPrefab");
            if (keepDualWeapons)
                EnsureDualMountedWeapons(mountGo);
            else
                KeepSingleMountedWeapon(mountGo);
            ForceDownRail(mountGo);
            PrefabFactory.StampVisual(mountGo, NobpContent.VisualPrefab);
            mount.prefab = mountGo;

            MountedMissile[] mounted = mountGo.GetComponentsInChildren<MountedMissile>(true);
            foreach (MountedMissile mm in mounted)
            {
                if (mm != null)
                    mm.info = info;
            }

            // Safety: shell mount must still be itself
            if (!string.Equals(shellMount.jsonKey, shellKey, StringComparison.Ordinal))
                HydraPlugin.ModLog?.LogError($"Shell mount jsonKey mutated! was '{shellKey}' now '{shellMount.jsonKey}'");

            enc.weaponMounts ??= new List<WeaponMount>();
            if (!enc.weaponMounts.Contains(mount))
                enc.weaponMounts.Add(mount);

            Encyclopedia.WeaponLookup ??= new Dictionary<string, WeaponMount>(StringComparer.Ordinal);
            Encyclopedia.WeaponLookup[mount.jsonKey] = mount;

            if (enc.IndexLookup != null && !ContainsNet(enc.IndexLookup, mount))
            {
                enc.IndexLookup.Add(mount);
                ((INetworkDefinition)mount).LookupIndex = enc.IndexLookup.Count - 1;
            }

            try
            {
                mount.Initialize();
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogWarning($"WeaponMount.Initialize: {ex.Message}");
            }

            // Initialize may rewrite info from children — force ours back
            mount.info = info;
            mount.mountName = ammo >= 2
                ? TorpedoConstants.MountDisplayName + " x2"
                : TorpedoConstants.MountDisplayName;
            mount.jsonKey = mountJsonKey;
            mount.ammo = ammo;
            mount.mass = mount.emptyMass + TorpedoConstants.LaunchMassKg * ammo;
            mount.GearSafety = true;
            mount.GroundSafety = true;
            info.weaponPrefab = missileDef.unitPrefab;
            info.blastDamage = TorpedoConstants.BlastYieldKg;

            if (string.Equals(mountJsonKey, TorpedoConstants.MountJsonKey, StringComparison.Ordinal))
                TorpedoMount = mount;
            else if (string.Equals(mountJsonKey, TorpedoConstants.MountJsonKeyDouble, StringComparison.Ordinal))
                TorpedoMountDouble = mount;

            TorpedoInfo = info;
            HydraPlugin.ModLog?.LogInfo($"Created WeaponMount '{mountJsonKey}' x{ammo} from shell '{shellKey}'.");
            return mount;
        }

        private static void EnsureDualMountedWeapons(GameObject mountGo)
        {
            MountedMissile[] mounted = mountGo.GetComponentsInChildren<MountedMissile>(true);
            if (mounted.Length >= 2)
            {
                // Preserve shell dual-rail layout; only separate if they share the same pose.
                if (mounted[0] != null && mounted[1] != null &&
                    (mounted[0].transform.localPosition - mounted[1].transform.localPosition).sqrMagnitude < 0.01f)
                {
                    // Lateral offset (bay / twin-rail), not aft — aft hides the second in the bay.
                    mounted[1].transform.localPosition =
                        mounted[0].transform.localPosition + new Vector3(0.85f, 0f, 0f);
                }
                return;
            }
            if (mounted.Length == 0)
                return;

            MountedMissile first = mounted[0];
            if (first == null)
                return;

            GameObject clone = UnityEngine.Object.Instantiate(first.gameObject, first.transform.parent);
            clone.name = first.name + "_2";
            clone.transform.localPosition = first.transform.localPosition + new Vector3(0.85f, 0f, 0f);
            clone.transform.localRotation = first.transform.localRotation;
        }

        private static void KeepSingleMountedWeapon(GameObject mountGo)
        {
            MountedMissile[] mounted = mountGo.GetComponentsInChildren<MountedMissile>(true);
            for (int i = 1; i < mounted.Length; i++)
            {
                if (mounted[i] != null)
                    UnityEngine.Object.DestroyImmediate(mounted[i].gameObject);
            }
        }

        private static void ForceDownRail(GameObject mountGo)
        {
            // MountedMissile.RailDirection.Down = 1
            const int down = 1;
            MountedMissile[] mounted = mountGo.GetComponentsInChildren<MountedMissile>(true);
            foreach (MountedMissile mm in mounted)
            {
                if (mm == null)
                    continue;
                FieldInfo? railDir = typeof(MountedMissile).GetField("railDirection", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? railLen = typeof(MountedMissile).GetField("railLength", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? railSpd = typeof(MountedMissile).GetField("railSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? railDelay = typeof(MountedMissile).GetField("railDelay", BindingFlags.Instance | BindingFlags.NonPublic);
                railDir?.SetValue(mm, (MountedMissile.RailDirection)down);
                if (railLen != null && railLen.GetValue(mm) is float len && len < 0.05f)
                    railLen.SetValue(mm, 0.6f);
                if (railSpd != null && railSpd.GetValue(mm) is float spd && spd < 0.05f)
                    railSpd.SetValue(mm, 4f);
                if (railDelay != null && railDelay.GetValue(mm) is float d && d > 2f)
                    railDelay.SetValue(mm, 0.15f);
            }
        }

        private static WeaponMount? ResolveDoubleShellMount(Encyclopedia enc)
        {
            string[] keys =
            {
                "BallisticMissile1_internalx2",
                "BallisticMissile1_HE_internalx2"
            };
            foreach (string key in keys)
            {
                WeaponMount? m = PrefabFactory.FindMountByExactKey(enc, key);
                if (m?.prefab != null && m.info != null &&
                    m.jsonKey.IndexOf(TorpedoConstants.PiledriverNukeToken, StringComparison.OrdinalIgnoreCase) < 0)
                    return m;
            }

            if (enc.weaponMounts == null)
                return null;
            foreach (WeaponMount cand in enc.weaponMounts)
            {
                if (cand?.prefab == null || cand.info == null || string.IsNullOrEmpty(cand.jsonKey))
                    continue;
                if (!cand.jsonKey.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (cand.jsonKey.IndexOf(TorpedoConstants.PiledriverNukeToken, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (cand.jsonKey.IndexOf("internalx2", StringComparison.OrdinalIgnoreCase) < 0 &&
                    cand.jsonKey.IndexOf("double", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                return cand;
            }
            return null;
        }

        private static WeaponMount? ResolveShellMount(Encyclopedia enc)
        {
            WeaponMount? m = PrefabFactory.FindMountByExactKey(enc, TorpedoConstants.ShellMountKey);
            if (m?.prefab != null && m.info != null)
                return m;
            m = PrefabFactory.FindMountByExactKey(enc, TorpedoConstants.ShellMountKeyAlt);
            if (m?.prefab != null && m.info != null)
                return m;

            // Extra known glide / internal mounts (never TBM)
            string[] extras = { "bomb_glide1_triple", "bomb_glide1_single", "bomb_125_internal", "bomb_250_internalx2", "bomb_500_internal" };
            foreach (string key in extras)
            {
                m = PrefabFactory.FindMountByExactKey(enc, key);
                if (m?.prefab != null && m.info != null && !m.info.nuclear)
                    return m;
            }

            if (enc.weaponMounts == null)
                return null;
            foreach (WeaponMount cand in enc.weaponMounts)
            {
                if (cand?.prefab == null || cand.info == null || string.IsNullOrEmpty(cand.jsonKey))
                    continue;
                if (cand.jsonKey.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (cand.jsonKey.IndexOf("tacNuke", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (cand.info.nuclear || cand.info.strategic)
                    continue;
                if (cand.info.glideBomb && cand.info.weaponPrefab != null)
                    return cand;
                if (cand.info.bomb && cand.ammo <= 4 && cand.info.weaponPrefab != null)
                    return cand;
            }
            return null;
        }

        private static MissileDefinition? ResolveShellMissile(Encyclopedia enc)
        {
            MissileDefinition? m = PrefabFactory.FindMissileByExactKey(enc, TorpedoConstants.ShellMissileKey);
            if (m?.unitPrefab != null)
                return m;
            m = PrefabFactory.FindMissileByExactKey(enc, TorpedoConstants.ShellMissileKeyAlt);
            if (m?.unitPrefab != null)
                return m;

            WeaponMount? mount = ResolveShellMount(enc);
            if (mount?.info?.weaponPrefab != null)
            {
                Missile? mis = mount.info.weaponPrefab.GetComponent<Missile>() ??
                               mount.info.weaponPrefab.GetComponentInChildren<Missile>(true);
                if (mis?.definition is MissileDefinition md && md.unitPrefab != null)
                    return md;
            }

            if (enc.missiles == null)
                return null;
            MissileDefinition? glide = null;
            foreach (MissileDefinition cand in enc.missiles)
            {
                if (cand?.unitPrefab == null || string.IsNullOrEmpty(cand.jsonKey))
                    continue;
                if (cand.jsonKey.IndexOf("nuclear", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (cand.jsonKey.StartsWith("BallisticMissile", StringComparison.OrdinalIgnoreCase) ||
                    cand.jsonKey.StartsWith("ballisticMissile", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (cand.jsonKey.IndexOf("glide", StringComparison.OrdinalIgnoreCase) >= 0)
                    return cand;
                if (glide == null && cand.jsonKey.IndexOf("bomb", StringComparison.OrdinalIgnoreCase) >= 0)
                    glide = cand;
            }
            return glide;
        }

        private static bool ContainsNet(List<INetworkDefinition> list, INetworkDefinition item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                    return true;
            }
            return false;
        }

        internal static bool IsOurMissile(Missile? missile)
        {
            if (missile == null)
                return false;
            if (missile.GetComponent<Mk54Tag>() != null)
                return true;
            if (Mk54SpawnGate.InFlight)
                return true;
            WeaponInfo? wi = missile.GetWeaponInfo();
            if (wi != null &&
                (wi.weaponName == TorpedoConstants.WeaponInfoName ||
                 wi.shortName == TorpedoConstants.ShortName ||
                 wi.shortName == TorpedoConstants.ShortNameLegacy))
                return true;
            return missile.definition != null &&
                   string.Equals(missile.definition.jsonKey, TorpedoConstants.MissileJsonKey, StringComparison.Ordinal);
        }

        internal static bool IsOurMount(WeaponMount? mount)
        {
            if (mount == null)
                return false;
            return string.Equals(mount.jsonKey, TorpedoConstants.MountJsonKey, StringComparison.Ordinal) ||
                   string.Equals(mount.jsonKey, TorpedoConstants.MountJsonKeyDouble, StringComparison.Ordinal);
        }
    }
}
