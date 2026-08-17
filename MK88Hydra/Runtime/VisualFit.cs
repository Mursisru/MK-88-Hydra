using UnityEngine;

namespace Hydra.Runtime
{
    internal enum VisualMountSnap
    {
        /// <summary>External pylon: PlaceOfRocketLock + MountClearance.</summary>
        PylonAttach,
        /// <summary>Internal bay: full-model AABB center, then lift world-up so belly protrusion shrinks.</summary>
        FullModelCenter
    }

    /// <summary>
    /// Uniform scale to LengthM, then snap mount.
    /// Bay uses ALL TorpedoVisual meshes; after center snap, lift so the hull sits up in the bay.
    /// </summary>
    internal static class VisualFit
    {
        internal static void Apply(Transform vis) =>
            Apply(vis, VisualMountSnap.PylonAttach);

        internal static void Apply(Transform vis, VisualMountSnap snap)
        {
            if (vis == null)
                return;

            vis.localPosition = Vector3.zero;
            vis.localRotation = Quaternion.Euler(TorpedoConstants.VisualEulerDeg);
            vis.localScale = Vector3.one;

            EnsureVisualRenderersOn(vis);

            if (!TryEncapsulateLocal(vis, out Bounds localBounds, includeDisabled: true))
                return;

            Vector3 size = localBounds.size;
            int longAxis = 0;
            if (size.y >= size.x && size.y >= size.z)
                longAxis = 1;
            else if (size.z >= size.x && size.z >= size.y)
                longAxis = 2;

            vis.localRotation = Quaternion.Euler(TorpedoConstants.VisualEulerDeg) * AxisToForward(longAxis);

            if (!TryEncapsulateLocal(vis, out localBounds, includeDisabled: true))
                return;

            size = localBounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest < 1e-4f)
                return;

            float s = (TorpedoConstants.LengthM * TorpedoConstants.VisualScaleMult) / longest;
            vis.localScale = new Vector3(s, s, s);

            if (snap == VisualMountSnap.FullModelCenter)
            {
                SnapFullModelCenterToParentOrigin(vis);
                LiftBayIntoFuselage(vis);
            }
            else
            {
                SnapAttachToParentOrigin(vis, TorpedoConstants.AttachPylonAliases);
                vis.localPosition += Vector3.down * TorpedoConstants.MountClearanceM;
            }

            if (TryEncapsulateWorld(vis, out Bounds world, includeDisabled: true))
            {
                HydraPlugin.ModLog?.LogInfo(
                    $"VisualFit '{vis.name}' snap={snap} scale={s:F3} localPos={vis.localPosition} worldBounds={world.size}");
            }
        }

        /// <summary>X-77: FBX size × scaleMult, longest→+Z, aft mesh behind nose, snap attach.</summary>
        internal static void ApplyKeepFbxSize(
            Transform vis,
            VisualMountSnap snap,
            string[] attachAliases,
            float clearanceM,
            float scaleMult = 1f,
            string[]? aftAliases = null,
            string[]? noseAliases = null)
        {
            if (vis == null)
                return;

            vis.localPosition = Vector3.zero;
            vis.localRotation = Quaternion.identity;
            vis.localScale = Vector3.one;
            EnsureVisualRenderersOn(vis);

            if (!TryEncapsulateLocal(vis, out Bounds localBounds, includeDisabled: true))
                return;

            Vector3 size = localBounds.size;
            int longAxis = 0;
            if (size.y >= size.x && size.y >= size.z)
                longAxis = 1;
            else if (size.z >= size.x && size.z >= size.y)
                longAxis = 2;
            vis.localRotation = AxisToForward(longAxis);
            EnsureAftIsNegativeZ(vis, aftAliases, noseAliases);

            float s = Mathf.Max(0.05f, scaleMult);
            vis.localScale = new Vector3(s, s, s);

            if (snap == VisualMountSnap.FullModelCenter)
            {
                SnapFullModelCenterToParentOrigin(vis);
                LiftBayIntoFuselage(vis);
            }
            else
            {
                SnapAttachToParentOrigin(vis, attachAliases ?? TorpedoConstants.AttachPylonAliases);
                vis.localPosition += Vector3.down * Mathf.Max(0f, clearanceM);
            }

            if (TryEncapsulateWorld(vis, out Bounds world, includeDisabled: true))
            {
                HydraPlugin.ModLog?.LogInfo(
                    $"VisualFit FBX-size '{vis.name}' snap={snap} scale={s:F2} localPos={vis.localPosition} worldBounds={world.size}");
            }
        }

        /// <summary>Booster/nozzle (aft) must sit at −Z after longest→+Z; flip 180° yaw if reversed.</summary>
        private static void EnsureAftIsNegativeZ(Transform vis, string[]? aftAliases, string[]? noseAliases)
        {
            if (vis == null || aftAliases == null || noseAliases == null)
                return;
            Transform? aft = TransformBinder.FindByAliases(vis, aftAliases);
            Transform? nose = TransformBinder.FindByAliases(vis, noseAliases);
            if (aft == null || nose == null)
                return;

            Vector3 a = vis.InverseTransformPoint(RendererHub(aft));
            Vector3 n = vis.InverseTransformPoint(RendererHub(nose));
            if (a.z <= n.z)
                return;

            vis.localRotation = Quaternion.Euler(0f, 180f, 0f) * vis.localRotation;
            HydraPlugin.ModLog?.LogInfo($"VisualFit yaw180 (aft was forward) '{vis.name}'");
        }

        private static Vector3 RendererHub(Transform t)
        {
            Renderer? r = t.GetComponent<Renderer>();
            if (r == null)
                r = t.GetComponentInChildren<Renderer>(true);
            return r != null ? r.bounds.center : t.position;
        }

        private static void EnsureVisualRenderersOn(Transform vis)
        {
            Renderer[] rs = vis.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] != null)
                    rs[i].enabled = true;
            }
        }

        private static void SnapAttachToParentOrigin(Transform vis, string[] aliases)
        {
            if (vis.parent == null)
                return;

            Transform? attach = TransformBinder.FindByAliases(vis, aliases);
            if (attach == null)
                return;

            Vector3 attachInParent = vis.parent.InverseTransformPoint(attach.position);
            vis.localPosition -= attachInParent;
        }

        private static void SnapFullModelCenterToParentOrigin(Transform vis)
        {
            if (vis.parent == null)
                return;

            if (!TryEncapsulateLocal(vis, out Bounds localBounds, includeDisabled: true))
                return;

            Vector3 centerWorld = vis.TransformPoint(localBounds.center);
            Vector3 centerInParent = vis.parent.InverseTransformPoint(centerWorld);
            vis.localPosition -= centerInParent;
        }

        /// <summary>
        /// Belly bays: after COM@hardpoint, bottom still hangs into air. Lift world-up until
        /// bottom is only BayBottomSlackM below the hardpoint (pulls hull into the bay).
        /// </summary>
        private static void LiftBayIntoFuselage(Transform vis)
        {
            if (vis.parent == null)
                return;
            if (!TryEncapsulateWorld(vis, out Bounds world, includeDisabled: true))
                return;

            float hardpointY = vis.parent.position.y;
            float bottomY = world.min.y;
            float wantBottom = hardpointY - TorpedoConstants.BayBottomSlackM;
            float lift = wantBottom - bottomY;
            if (lift <= 0.01f)
                return;

            // Cap so we don't shove the top through the bay roof.
            float maxLift = world.size.y * 0.55f + TorpedoConstants.BayCenterLiftExtraM;
            lift = Mathf.Min(lift, maxLift);

            Vector3 liftParent = vis.parent.InverseTransformDirection(Vector3.up) * lift;
            vis.localPosition += liftParent;
            HydraPlugin.ModLog?.LogInfo(
                $"VisualFit bay lift={lift:F2}m hardpointY={hardpointY:F1} bottomWas={bottomY:F1}");
        }

        private static Quaternion AxisToForward(int axis)
        {
            switch (axis)
            {
                case 0:
                    return Quaternion.Euler(0f, 90f, 0f);
                case 1:
                    return Quaternion.Euler(90f, 0f, 0f);
                default:
                    return Quaternion.identity;
            }
        }

        private static bool TryEncapsulateLocal(Transform root, out Bounds bounds, bool includeDisabled)
        {
            bounds = default;
            bool any = false;
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null)
                    continue;
                if (!includeDisabled && !r.enabled)
                    continue;

                Bounds rb = r.localBounds;
                Vector3[] corners =
                {
                    new Vector3(rb.min.x, rb.min.y, rb.min.z),
                    new Vector3(rb.min.x, rb.min.y, rb.max.z),
                    new Vector3(rb.min.x, rb.max.y, rb.min.z),
                    new Vector3(rb.min.x, rb.max.y, rb.max.z),
                    new Vector3(rb.max.x, rb.min.y, rb.min.z),
                    new Vector3(rb.max.x, rb.min.y, rb.max.z),
                    new Vector3(rb.max.x, rb.max.y, rb.min.z),
                    new Vector3(rb.max.x, rb.max.y, rb.max.z)
                };

                Matrix4x4 toRoot = root.worldToLocalMatrix * r.localToWorldMatrix;
                for (int c = 0; c < corners.Length; c++)
                {
                    Vector3 p = toRoot.MultiplyPoint3x4(corners[c]);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                        bounds.Encapsulate(p);
                }
            }
            return any;
        }

        private static bool TryEncapsulateWorld(Transform root, out Bounds bounds, bool includeDisabled)
        {
            bounds = default;
            bool any = false;
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null)
                    continue;
                if (!includeDisabled && !rs[i].enabled)
                    continue;
                if (!any)
                {
                    bounds = rs[i].bounds;
                    any = true;
                }
                else
                {
                    Bounds b = bounds;
                    b.Encapsulate(rs[i].bounds);
                    bounds = b;
                }
            }
            return any;
        }
    }
}
