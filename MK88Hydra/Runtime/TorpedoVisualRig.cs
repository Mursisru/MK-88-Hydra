using System;
using System.Collections.Generic;
using Hydra.Blueprinter;
using Hydra.Bootstrap;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Caches role transforms. Never Instantiates networked parachutes here.</summary>
    internal sealed class TorpedoVisualRig : MonoBehaviour
    {
        internal Transform? AttachParachute;
        internal Transform? ParachuteCover;
        internal Transform? PropCw;
        internal Transform? PropCcw;
        internal Transform? Kozuch;
        internal readonly List<Transform> CoverWings = new List<Transform>(4);
        internal readonly List<Quaternion> WingRestLocal = new List<Quaternion>(4);
        internal GameObject? VisualRoot;

        internal bool GlideKitVisible { get; private set; } = true;
        internal bool ChuteBoxVisible { get; private set; } = true;

        private Quaternion _propCwRestRot;
        private float _propCwAngle;
        private bool _propCwOk;

        private Quaternion _propCcwRestRot;
        private float _propCcwAngle;
        private bool _propCcwOk;

        internal static TorpedoVisualRig Ensure(Missile missile)
        {
            TorpedoVisualRig? rig = missile.GetComponent<TorpedoVisualRig>();
            if (rig != null)
                return rig;
            rig = missile.gameObject.AddComponent<TorpedoVisualRig>();
            rig.Build(missile);
            return rig;
        }

        private void Build(Missile missile)
        {
            Transform? existing = PrefabFactory.FindTorpedoVisual(missile.transform);
            if (existing != null)
            {
                VisualRoot = existing.gameObject;
                Bind(existing);
                return;
            }

            GameObject? prefab = NobpContent.VisualPrefab;
            if (prefab != null)
            {
                VisualRoot = Instantiate(prefab, missile.transform, false);
                VisualRoot.name = "TorpedoVisual";
                VisualMaterials.MatchHostDrawState(VisualRoot, missile.gameObject);
                HideStockExceptVisual(missile.gameObject);
                VisualFit.Apply(VisualRoot.transform);
                VisualMaterials.NormalizeAndPaint(VisualRoot);
                Bind(VisualRoot.transform);
            }
            else
            {
                Bind(missile.transform);
            }
        }

        private static void HideStockExceptVisual(GameObject root)
        {
            Renderer[] stock = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < stock.Length; i++)
            {
                if (stock[i] == null)
                    continue;
                Transform t = stock[i].transform;
                bool underVis = false;
                while (t != null)
                {
                    if (t.name == "TorpedoVisual")
                    {
                        underVis = true;
                        break;
                    }
                    t = t.parent;
                }
                if (!underVis)
                    stock[i].enabled = false;
            }
        }

        private void Bind(Transform root)
        {
            AttachParachute = TransformBinder.FindByAliases(root, TorpedoConstants.AttachParachuteAliases);
            ParachuteCover = TransformBinder.FindByAliases(root, TorpedoConstants.ParachuteCoverAliases);
            PropCw = TransformBinder.FindByAliases(root, TorpedoConstants.PropCwAliases);
            PropCcw = TransformBinder.FindByAliases(root, TorpedoConstants.PropCcwAliases);
            Kozuch = TransformBinder.FindByAliases(root, TorpedoConstants.KozuchAliases);
            CoverWings.Clear();
            WingRestLocal.Clear();
            TransformBinder.CollectByAliases(root, TorpedoConstants.FinAliases, CoverWings);
            for (int i = 0; i < CoverWings.Count; i++)
            {
                if (CoverWings[i] != null)
                    WingRestLocal.Add(CoverWings[i].localRotation);
            }

            _propCwAngle = 0f;
            _propCcwAngle = 0f;
            _propCwOk = TorpedoPropSpin.Capture(PropCw, out _propCwRestRot);
            _propCcwOk = TorpedoPropSpin.Capture(PropCcw, out _propCcwRestRot);

            Transform axis = VisualRoot != null ? VisualRoot.transform : root;
            TorpedoChuteSocket.LogDummy(AttachParachute, axis);

            HydraPlugin.ModLog?.LogInfo(
                $"Rig bind chute={(AttachParachute != null)} chuteBox={(ParachuteCover != null)} propCw={(PropCw != null)}/{_propCwOk} propCcw={(PropCcw != null)}/{_propCcwOk} kozuch={(Kozuch != null)} coverWings={CoverWings.Count}");
        }

        internal void TickWingDeploy(float t01)
        {
            float t = Mathf.Clamp01(t01);
            float ang = TorpedoConstants.FinDeployAngleDeg * t;
            int n = Mathf.Min(CoverWings.Count, WingRestLocal.Count);
            for (int i = 0; i < n; i++)
            {
                Transform w = CoverWings[i];
                if (w == null)
                    continue;
                float sign = WingSign(w.name);
                w.localRotation = WingRestLocal[i] * Quaternion.Euler(0f, 0f, sign * ang);
            }
        }

        internal void SpinProps(float dt)
        {
            float deg = TorpedoConstants.PropRpm * 6f * dt;
            _propCwAngle -= deg;
            _propCcwAngle += deg;
            TorpedoPropSpin.Tick(PropCw, _propCwOk, _propCwRestRot, _propCwAngle);
            TorpedoPropSpin.Tick(PropCcw, _propCcwOk, _propCcwRestRot, _propCcwAngle);
        }

        private static float WingSign(string name)
        {
            if (name.IndexOf("WingR", StringComparison.OrdinalIgnoreCase) >= 0)
                return -1f;
            if (name.IndexOf("КрылоП", StringComparison.OrdinalIgnoreCase) >= 0)
                return -1f;
            return 1f;
        }

        internal void HideGlideKit()
        {
            if (!GlideKitVisible)
                return;
            GlideKitVisible = false;
            HideRenderers(Kozuch);
            for (int i = 0; i < CoverWings.Count; i++)
                HideRenderers(CoverWings[i]);
        }

        internal void HideChuteBox()
        {
            if (!ChuteBoxVisible)
                return;
            ChuteBoxVisible = false;
            HideRenderers(ParachuteCover);
        }

        internal void HideCoverAssembly()
        {
            HideGlideKit();
            HideChuteBox();
        }

        private static void HideRenderers(Transform? root)
        {
            if (root == null)
                return;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null)
                    r.enabled = false;
            }
        }
    }
}
