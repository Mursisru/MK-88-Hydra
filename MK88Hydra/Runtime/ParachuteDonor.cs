using System;
using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Read-only vanilla Parachute GO — prefer GPO-N for ×3 radius baseline.</summary>
    internal static class ParachuteDonor
    {
        private static readonly FieldInfo? CanopyField =
            typeof(Parachute).GetField("canopy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? LinesField =
            typeof(Parachute).GetField("lines", BindingFlags.Instance | BindingFlags.NonPublic);

        private static GameObject? _template;
        private static string? _sourceKey;

        internal static bool Ready => _template != null;

        internal static void Cache(Encyclopedia enc)
        {
            _template = null;
            _sourceKey = null;
            if (enc?.missiles == null)
                return;

            GameObject? best = null;
            string? bestKey = null;
            int bestScore = -1;

            foreach (MissileDefinition md in enc.missiles)
            {
                if (md?.unitPrefab == null || string.IsNullOrEmpty(md.jsonKey))
                    continue;

                Parachute? p = md.unitPrefab.GetComponentInChildren<Parachute>(true);
                if (p == null)
                    continue;

                int score = ScoreKey(md.jsonKey);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = p.gameObject;
                bestKey = md.jsonKey;
            }

            _template = best;
            _sourceKey = bestKey;
            if (_template != null)
            {
                Parachute? donorChute = _template.GetComponent<Parachute>();
                TorpedoChuteSetup.CaptureDonorRadius(donorChute);
                HydraPlugin.ModLog?.LogInfo($"Parachute donor='{_sourceKey}' score={bestScore}");
            }
            else
                HydraPlugin.ModLog?.LogWarning("Parachute donor not found — chute will be skipped.");
        }

        private static int ScoreKey(string key)
        {
            string k = key.ToLowerInvariant();
            // GPO-N first — radius baseline for ×3. tacNuke is larger and was making MK chute giant.
            if (k.IndexOf("gpo", StringComparison.Ordinal) >= 0)
                return 100;
            if (k.IndexOf("tacnuke", StringComparison.Ordinal) >= 0)
                return 70;
            if (k.IndexOf("nuclear", StringComparison.Ordinal) >= 0)
                return 50;
            if (k.IndexOf("genie", StringComparison.Ordinal) >= 0)
                return 40;
            if (k.IndexOf("bomb", StringComparison.Ordinal) >= 0)
                return 20;
            return 1;
        }

        internal static Parachute? SpawnBehindHull(Missile missile, Vector3 aftWorld) =>
            SpawnAtSocket(missile, aftWorld);

        internal static Parachute? SpawnAtSocket(Missile missile, Vector3 socketWorld)
        {
            if (_template == null || missile == null)
                return null;

            Transform parent = missile.transform;
            GameObject go = UnityEngine.Object.Instantiate(_template, parent, false);
            go.name = "Mk54Parachute";
            StripNetwork(go);
            SanitizeChuteGo(go);

            go.transform.position = socketWorld;
            go.transform.rotation = parent.rotation;
            go.transform.localScale = Vector3.one;
            go.SetActive(true);

            Parachute chute = go.GetComponent<Parachute>();
            if (chute == null)
            {
                UnityEngine.Object.Destroy(go);
                return null;
            }

            chute.SetAttachedUnit(missile);
            return chute;
        }

        internal static void PlaceCanopyBehindHull(Parachute chute, Missile missile) =>
            TorpedoChuteVisual.HoldCanopyOnHullAxis(chute, missile);

        internal static void KillCollidersAfterDeploy(Parachute chute)
        {
            if (chute == null)
                return;

            SanitizeChuteGo(chute.gameObject);

            if (CanopyField?.GetValue(chute) is GameObject canopy && canopy != null)
                SanitizeChuteGo(canopy);

            if (LinesField?.GetValue(chute) is GameObject lines && lines != null)
                SanitizeChuteGo(lines);
        }

        internal static void SanitizeChuteGo(GameObject root)
        {
            if (root == null)
                return;

            if (root.GetComponent<Mk54ChuteNoHit>() == null)
                root.AddComponent<Mk54ChuteNoHit>();

            SetLayerRecurse(root.transform, PhysicsLayers.IgnoreRaycast);
            KillCollidersImmediate(root);
        }

        internal static void KillCollidersImmediate(GameObject root)
        {
            if (root == null)
                return;

            Collider[] cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null)
                    continue;
                UnityEngine.Object.DestroyImmediate(cols[i]);
            }
        }

        private static void SetLayerRecurse(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecurse(t.GetChild(i), layer);
        }

        private static void StripNetwork(GameObject root)
        {
            Mirage.NetworkIdentity[] ids = root.GetComponentsInChildren<Mirage.NetworkIdentity>(true);
            for (int i = ids.Length - 1; i >= 0; i--)
            {
                if (ids[i] != null)
                    UnityEngine.Object.DestroyImmediate(ids[i]);
            }
        }
    }
}
