using System;
using Mirage;
using Hydra.Runtime;
using UnityEngine;

namespace Hydra.Bootstrap
{
    /// <summary>Clone game prefabs for our own munition without mutating encyclopedia assets.</summary>
    internal static class PrefabFactory
    {
        /// <summary>
        /// Live GO used as Instantiate source (same contract as a Unity prefab asset):
        /// local TRS identity, inactive, no HideAndDontSave.
        /// ParkPos is forbidden — Hardpoint.SpawnMount does Instantiate(prefab, pylon)
        /// which copies localPosition onto the pylon child.
        /// </summary>
        internal static GameObject CloneAsPrefab(GameObject source, string name)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            GameObject clone = UnityEngine.Object.Instantiate(source);
            clone.name = name;
            NetworkPrefabPrep.PrepareTemplate(clone);
            UnityEngine.Object.DontDestroyOnLoad(clone);
            ResetPrefabTransform(clone);
            FreezeTemplatePhysics(clone);
            clone.SetActive(false);
            NetworkPrefabPrep.PrepareTemplate(clone);
            NetworkPrefabPrep.LogState("template", clone);
            return clone;
        }

        internal static void ResetPrefabTransform(GameObject go)
        {
            if (go == null)
                return;
            go.hideFlags = HideFlags.None;
            go.transform.SetParent(null, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }

        /// <summary>Template must not simulate while briefly activated for Instantiate.</summary>
        internal static void FreezeTemplatePhysics(GameObject root)
        {
            if (root == null)
                return;

            foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null)
                    continue;
                rb.detectCollisions = false;
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (cam != null)
                    cam.enabled = false;
            }

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light != null)
                    light.enabled = false;
            }
        }

        /// <summary>Wake a pylon-mounted instance. Keep kinematic so it stays on the rail.</summary>
        internal static void ActivateMountedInstance(GameObject instance, bool internalBay = false)
        {
            if (instance == null)
                return;

            instance.hideFlags = HideFlags.None;
            instance.SetActive(true);
            // Mount root at pylon origin — do not touch MountedMissile child TRS (rail pose)
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null)
                    continue;
                rb.isKinematic = true;
                rb.detectCollisions = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            EnableGameplayBehaviours(instance, enableMissile: false, stripJunk: true);

            if (internalBay)
            {
                // Internal bay: never show mesh (fins poke through Darkreach/Alkyon skin).
                HideMountedBayVisuals(instance);
            }
            else
            {
                EnsureVisualRenderers(instance);
            }

            LogRenderers(internalBay ? "mount-bay" : "mount-pylon", instance);
        }

        /// <summary>Hide TorpedoVisual + stock meshes inside closed weapon bays.</summary>
        internal static void HideMountedBayVisuals(GameObject host)
        {
            if (host == null)
                return;

            HideStockRenderers(host);

            Transform[] visuals = FindAllTorpedoVisuals(host.transform);
            for (int i = 0; i < visuals.Length; i++)
            {
                Transform vis = visuals[i];
                if (vis == null)
                    continue;
                foreach (Renderer r in vis.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null)
                        r.enabled = false;
                }
                vis.gameObject.SetActive(false);
            }

            HydraPlugin.ModLog?.LogInfo(
                $"HideMountedBayVisuals '{host.name}' visuals={visuals.Length}");
        }

        /// <summary>
        /// Legacy bay refit — kept for callers; bay mounts now hide instead of re-centering.
        /// </summary>
        internal static void RefitVisualForInternalBay(GameObject host) =>
            HideMountedBayVisuals(host);

        /// <summary>Wake a dropped/flying instance at the spawn pose already set by Spawner.</summary>
        internal static void ActivateFlyingInstance(GameObject instance)
        {
            if (instance == null)
                return;

            instance.hideFlags = HideFlags.None;
            instance.SetActive(true);

            Missile? missile = instance.GetComponent<Missile>() ?? instance.GetComponentInChildren<Missile>(true);
            Rigidbody? rootRb = instance.GetComponent<Rigidbody>();
            if (rootRb == null && missile != null)
                rootRb = missile.rb;

            if (rootRb == null)
            {
                HydraPlugin.ModLog?.LogError($"Flying '{instance.name}' has no Rigidbody.");
            }
            else
            {
                // Spawner sets velocity while template is still kinematic; flipping kinematic
                // after StartMissile wipes velocity → OpticalSeekerBomb slow-fuse detonates.
                Vector3 keepV = rootRb.velocity;
                if (keepV.sqrMagnitude < 0.01f && missile != null)
                    keepV = missile.startingVelocity;

                rootRb.isKinematic = false;
                rootRb.detectCollisions = true;
                rootRb.useGravity = false; // vanilla Missile ApplyAero / gravMult
                rootRb.velocity = keepV;
                rootRb.angularVelocity = Vector3.zero;
            }

            foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null || rb == rootRb)
                    continue;
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            if (missile != null)
            {
                SafeSeparation.Prepare(missile);
                if (rootRb != null)
                {
                    HydraPlugin.ModLog?.LogInfo(
                        $"Fly physics: v={rootRb.velocity} startV={missile.startingVelocity} kin={rootRb.isKinematic} g={rootRb.useGravity}");
                }
            }

            EnableGameplayBehaviours(instance, enableMissile: true, stripJunk: false);
            EnsureVisualRenderers(instance);
            LogRenderers("fly", instance);
        }

        private static void EnableGameplayBehaviours(GameObject root, bool enableMissile, bool stripJunk)
        {
            foreach (Behaviour b in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (b == null)
                    continue;
                if (b is NetworkIdentity || b is NetworkBehaviour)
                    continue;
                string tn = b.GetType().Name;
                if (tn == "Camera" || tn == "AudioListener" || tn == "Flare" || tn == "Light" ||
                    tn == "ReflectionProbe" || tn == "Skybox")
                {
                    b.enabled = false;
                    continue;
                }
                if (!enableMissile && (tn == "Missile" || tn.EndsWith("Seeker", StringComparison.Ordinal)))
                {
                    b.enabled = false;
                    continue;
                }
                b.enabled = true;
            }

            if (stripJunk)
                VisualMaterials.StripSceneJunk(root);
        }

        internal static Transform? FindTorpedoVisual(Transform root)
        {
            Transform[] all = FindAllTorpedoVisuals(root);
            return all.Length > 0 ? all[0] : null;
        }

        internal static Transform[] FindAllTorpedoVisuals(Transform root)
        {
            if (root == null)
                return System.Array.Empty<Transform>();

            var list = new System.Collections.Generic.List<Transform>(4);
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "TorpedoVisual")
                    list.Add(all[i]);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Visual must live under MountedMissile (mount) or Missile (unit).
        /// Sibling visual survives Fire/SetActive(false) on MountedMissile → "ammo drops, mesh stays".
        /// </summary>
        internal static Transform ResolveVisualParent(GameObject host)
        {
            MountedMissile? mm = host.GetComponentInChildren<MountedMissile>(true);
            if (mm != null)
                return mm.transform;
            Missile? mis = host.GetComponentInChildren<Missile>(true);
            if (mis != null)
                return mis.transform;
            return host.transform;
        }

        private static void EnsureVisualRenderers(GameObject root)
        {
            Transform[] visuals = FindAllTorpedoVisuals(root.transform);
            for (int i = 0; i < visuals.Length; i++)
            {
                Transform vis = visuals[i];
                if (vis == null)
                    continue;
                vis.gameObject.SetActive(true);
                foreach (Renderer r in vis.GetComponentsInChildren<Renderer>(true))
                {
                    if (r != null)
                        r.enabled = true;
                }
                if (ApproximatelyOne(vis.localScale))
                    VisualFit.Apply(vis);
                VisualMaterials.NormalizeAndPaint(vis.gameObject);
            }
        }

        private static Transform? FindNamedVisual(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            Transform direct = root.Find(name);
            if (direct != null)
                return direct;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        private static bool ApproximatelyOne(Vector3 s)
        {
            return Mathf.Abs(s.x - 1f) < 0.001f &&
                   Mathf.Abs(s.y - 1f) < 0.001f &&
                   Mathf.Abs(s.z - 1f) < 0.001f;
        }

        private static void DestroyExistingVisuals(GameObject host)
        {
            Transform[] all = host.GetComponentsInChildren<Transform>(true);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                if (all[i] != null && all[i].name == "TorpedoVisual")
                    UnityEngine.Object.DestroyImmediate(all[i].gameObject);
            }
        }

        internal static void StampVisual(GameObject host, GameObject? visualPrefab)
        {
            if (host == null)
                return;

            DestroyExistingVisuals(host);

            if (visualPrefab == null)
            {
                HydraPlugin.ModLog?.LogWarning($"StampVisual: no visual on '{host.name}', keeping shell mesh.");
                return;
            }

            int stamped = StampVisualOnAllWeapons(host, visualPrefab);
            if (stamped > 0)
                HideStockRenderers(host);
            else
                HydraPlugin.ModLog?.LogWarning("StampVisual: visual has 0 parents, keeping shell mesh.");

            ResetPrefabTransform(host);
            host.SetActive(false);
            NetworkPrefabPrep.PrepareTemplate(host);
        }

        /// <summary>Live spawn / encyclopedia — no template deactivate or NI prep.</summary>
        internal static void StampVisualLive(GameObject host, GameObject? visualPrefab)
        {
            if (host == null || visualPrefab == null)
                return;

            DestroyExistingVisuals(host);
            int stamped = StampVisualOnAllWeapons(host, visualPrefab);
            if (stamped > 0)
                HideStockRenderers(host);
        }

        /// <summary>
        /// Dual mounts: stamp TorpedoVisual under EVERY MountedMissile.
        /// Old path stamped only the first → HideStockRenderers hid the second rail mesh.
        /// </summary>
        private static int StampVisualOnAllWeapons(GameObject host, GameObject visualPrefab)
        {
            MountedMissile[] mms = host.GetComponentsInChildren<MountedMissile>(true);
            int stamped = 0;
            if (mms.Length > 0)
            {
                for (int i = 0; i < mms.Length; i++)
                {
                    if (mms[i] == null)
                        continue;
                    if (StampOneVisual(mms[i].transform, host, visualPrefab))
                        stamped++;
                }
                return stamped;
            }

            return StampOneVisual(ResolveVisualParent(host), host, visualPrefab) ? 1 : 0;
        }

        private static bool StampOneVisual(Transform parent, GameObject host, GameObject visualPrefab)
        {
            if (parent == null || visualPrefab == null)
                return false;

            GameObject vis = UnityEngine.Object.Instantiate(visualPrefab, parent, false);
            vis.name = "TorpedoVisual";
            vis.hideFlags = HideFlags.None;
            vis.SetActive(true);

            VisualMaterials.StripSceneJunk(vis);
            VisualMaterials.MatchHostDrawState(vis, host);

            int visOn = 0;
            foreach (Renderer r in vis.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                r.enabled = true;
                visOn++;
            }

            if (visOn == 0)
            {
                HydraPlugin.ModLog?.LogWarning($"StampOneVisual: 0 renderers under '{parent.name}'");
                return false;
            }

            VisualFit.Apply(vis.transform);
            VisualMaterials.NormalizeAndPaint(vis);
            HydraPlugin.ModLog?.LogInfo(
                $"StampVisual parent='{parent.name}' visLocal={vis.transform.localPosition}");
            return true;
        }

        private static void LogRenderers(string tag, GameObject root)
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            int on = 0;
            Bounds? b = null;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null || !rs[i].enabled)
                    continue;
                on++;
                if (b == null)
                    b = rs[i].bounds;
                else
                {
                    Bounds nb = b.Value;
                    nb.Encapsulate(rs[i].bounds);
                    b = nb;
                }
            }
            HydraPlugin.ModLog?.LogInfo(
                $"[{tag}] '{root.name}' active={root.activeInHierarchy} local={root.transform.localPosition} world={root.transform.position} renderersOn={on}/{rs.Length} bounds={(b.HasValue ? b.Value.size.ToString() : "none")}");
        }

        internal static void AssignUniquePrefabHash(GameObject root, string stableKey)
        {
            if (root == null)
                return;
            NetworkIdentity[] ids = root.GetComponentsInChildren<NetworkIdentity>(true);
            int hash = StableHash(stableKey);
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == null)
                    continue;
                ids[i].PrefabHash = hash + i;
            }
        }

        internal static void HideStockRenderers(GameObject root)
        {
            if (root == null)
                return;
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null)
                    continue;
                if (IsVisualRoot(rs[i].transform))
                    continue;
                rs[i].enabled = false;
            }
        }

        internal static bool IsVisualRoot(Transform t)
        {
            while (t != null)
            {
                if (t.name == "TorpedoVisual")
                    return true;
                t = t.parent;
            }
            return false;
        }

        internal static void NormalizeShaders(GameObject root) => VisualMaterials.NormalizeAndPaint(root);

        internal static WeaponMount? FindMountByExactKey(Encyclopedia enc, string jsonKey)
        {
            if (string.IsNullOrEmpty(jsonKey))
                return null;
            if (Encyclopedia.WeaponLookup != null &&
                Encyclopedia.WeaponLookup.TryGetValue(jsonKey, out WeaponMount m) &&
                m != null)
                return m;

            if (enc?.weaponMounts == null)
                return null;
            foreach (WeaponMount w in enc.weaponMounts)
            {
                if (w != null && string.Equals(w.jsonKey, jsonKey, StringComparison.Ordinal))
                    return w;
            }
            return null;
        }

        internal static MissileDefinition? FindMissileByExactKey(Encyclopedia enc, string jsonKey)
        {
            if (string.IsNullOrEmpty(jsonKey))
                return null;
            if (Encyclopedia.Lookup != null &&
                Encyclopedia.Lookup.TryGetValue(jsonKey, out UnitDefinition u) &&
                u is MissileDefinition md)
                return md;

            if (enc?.missiles == null)
                return null;
            foreach (MissileDefinition m in enc.missiles)
            {
                if (m != null && string.Equals(m.jsonKey, jsonKey, StringComparison.Ordinal))
                    return m;
            }
            return null;
        }

        internal static void AssertPiledriverIntact(Encyclopedia enc)
        {
            string[] keys =
            {
                "BallisticMissile1_single",
                "BallisticMissile1_internalx2",
                "BallisticMissile1_tacNuke_single",
                "BallisticMissile1_tacNuke_internalx2"
            };
            foreach (string key in keys)
            {
                WeaponMount? m = FindMountByExactKey(enc, key);
                if (m == null)
                    continue;
                if (!string.Equals(m.jsonKey, key, StringComparison.Ordinal))
                    HydraPlugin.ModLog?.LogError($"Piledriver corrupted: expected jsonKey '{key}' got '{m.jsonKey}'");
                else if (m.mountName != null &&
                         (m.mountName.IndexOf("MK-88", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          m.mountName.IndexOf("MK54", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          m.mountName.IndexOf("Hydra", StringComparison.OrdinalIgnoreCase) >= 0))
                    HydraPlugin.ModLog?.LogError($"Piledriver corrupted: mountName became '{m.mountName}' for '{key}'");
            }
        }

        internal static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                for (int i = 0; i < s.Length; i++)
                    h = h * 31 + s[i];
                if (h == 0)
                    h = 0x4D504B31;
                if (h < 0)
                    h = -h;
                return h;
            }
        }

        internal static void CopyMountScalars(WeaponMount src, WeaponMount dst)
        {
            dst.ammo = src.ammo;
            dst.turret = src.turret;
            dst.missileBay = src.missileBay;
            dst.radar = false;
            dst.tailHook = false;
            dst.slingloadHook = false;
            dst.countermeasure = false;
            dst.colorable = src.colorable;
            dst.Cargo = false;
            dst.Troops = false;
            dst.sortWeapons = src.sortWeapons;
            dst.GearSafety = src.GearSafety;
            dst.GroundSafety = src.GroundSafety;
            dst.GunAmmo = false;
            dst.emptyCost = src.emptyCost;
            dst.emptyMass = src.emptyMass;
            dst.mass = src.mass;
            dst.drag = src.drag;
            dst.emptyDrag = src.emptyDrag;
            dst.RCS = src.RCS;
            dst.emptyRCS = src.emptyRCS;
            dst.dontAutomaticallyAddToEncyclopedia = false;
        }

        internal static void CopyWeaponInfoScalars(WeaponInfo src, WeaponInfo dst)
        {
            dst.effectiveness = src.effectiveness;
            dst.targetRequirements = src.targetRequirements;
            dst.pK = src.pK;
            dst.fireInterval = src.fireInterval;
            dst.muzzleVelocity = src.muzzleVelocity;
            dst.maxSpeed = src.maxSpeed;
            dst.dragCoef = src.dragCoef;
            dst.gravMult = src.gravMult;
            dst.pierceDamage = src.pierceDamage;
            dst.blastDamage = src.blastDamage;
            dst.weaponIcon = src.weaponIcon;
            dst.armorTierEffectiveness = src.armorTierEffectiveness;
            dst.airburstHeight = src.airburstHeight;
            dst.visibilityWhenFired = src.visibilityWhenFired;
            dst.useWeaponDoors = src.useWeaponDoors;
            dst.boresight = src.boresight;
            dst.laserGuided = false;
            dst.missile = false;
            // Glide weapon HUD (MissileUI), not free-fall CCIP.
            dst.bomb = false;
            dst.glideBomb = true;
            dst.gun = false;
            dst.overHorizon = false;
            dst.nuclear = false;
            dst.strategic = false;
            dst.energy = false;
            dst.jammer = false;
            dst.troops = false;
            dst.hideInDisplay = false;
            dst.cargo = false;
            dst.rearmGround = src.rearmGround;
            dst.rearmShip = src.rearmShip;
            dst.sling = false;
        }

        internal static void CopyUnitDefScalars(UnitDefinition src, UnitDefinition dst)
        {
            dst.visibleRange = src.visibleRange;
            dst.iconRange = src.iconRange;
            dst.iconSize = src.iconSize;
            dst.mapIconSize = src.mapIconSize;
            dst.captureStrength = 0f;
            dst.captureDefense = 0f;
            dst.manpower = 0f;
            dst.armorTier = src.armorTier;
            dst.damageTolerance = src.damageTolerance;
            dst.minEditorHeight = src.minEditorHeight;
            dst.maxEditorHeight = src.maxEditorHeight;
            dst.code = src.code;
        }

        internal static void CopyMapIdentity(UnitDefinition src, UnitDefinition dst)
        {
            dst.mapIcon = src.mapIcon;
            dst.friendlyIcon = src.friendlyIcon;
            dst.hostileIcon = src.hostileIcon;
            dst.mapOrient = src.mapOrient;
            dst.mapIconSize = src.mapIconSize;
            dst.typeIdentity = src.typeIdentity;
            dst.roleIdentity = src.roleIdentity;
            dst.IsObstacle = false;
        }
    }
}
