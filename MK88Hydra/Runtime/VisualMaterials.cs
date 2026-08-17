using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Paint TorpedoVisual with URP Lit cloned from bomb_glide1 + disk/bundle albedos.</summary>
    internal static class VisualMaterials
    {
        private static Texture2D? _kozuch;
        private static bool _texTried;

        internal static void StripSceneJunk(GameObject root)
        {
            if (root == null)
                return;

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light != null)
                    UnityEngine.Object.DestroyImmediate(light.gameObject);
            }

            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (cam != null)
                    UnityEngine.Object.DestroyImmediate(cam.gameObject);
            }
        }

        internal static void PrimeShaderFrom(GameObject? sampleRoot) => VisualShader.PrimeFrom(sampleRoot);

        internal static void NormalizeAndPaint(GameObject root) =>
            NormalizeAndPaint(root, allowDiskFallback: true);


        internal static void NormalizeAndPaint(GameObject root, bool allowDiskFallback)
        {
            if (root == null)
                return;

            StripSceneJunk(root);

            Texture2D? kozuch = allowDiskFallback ? EnsureKozuchTexture() : null;
            int painted = 0;
            int missing = 0;

            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null)
                    continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                    continue;

                Material[] src = r.sharedMaterials;
                int slots = src != null && src.Length > 0 ? src.Length : 1;
                string meshName = r.gameObject.name;
                Material[] dst = new Material[slots];

                for (int m = 0; m < slots; m++)
                {
                    Material? old = src != null && m < src.Length ? src[m] : null;
                    Material mat = VisualShader.Make((old != null ? old.name : meshName) + "_runtime");

                    bool kozuchPart = allowDiskFallback && IsExactKozuchName(meshName);
                    Texture? tex = PeekAlbedo(old);
                    if (kozuchPart && kozuch != null)
                        tex = kozuch;
                    else if (tex == null && allowDiskFallback)
                        tex = VisualPartTextures.ForMesh(meshName);

                    if (tex != null)
                    {
                        WriteAlbedo(mat, tex);
                        CopyGloss(old, mat);
                        painted++;
                    }
                    else
                        missing++;

                    KillEmission(mat);
                    dst[m] = mat;
                }

                r.sharedMaterials = dst;
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            HydraPlugin.ModLog?.LogInfo(
                $"VisualMaterials: painted '{root.name}' renderers={rs.Length} texOk={painted} texMiss={missing} shader={VisualShader.Lit.name} templated={VisualShader.Template != null}");
        }

        internal static void MatchHostDrawState(GameObject vis, GameObject host)
        {
            if (vis == null || host == null)
                return;

            int layer = host.layer;
            uint mask = 1u;
            Renderer? donor = null;
            Renderer[] hostRs = host.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < hostRs.Length; i++)
            {
                Renderer r = hostRs[i];
                if (r == null)
                    continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                    continue;
                if (r.transform.name == "TorpedoVisual")
                    continue;
                Transform t = r.transform;
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
                if (underVis)
                    continue;
                donor = r;
                layer = r.gameObject.layer;
                mask = r.renderingLayerMask;
                break;
            }

            Transform[] all = vis.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    all[i].gameObject.layer = layer;
            }

            Renderer[] visRs = vis.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < visRs.Length; i++)
            {
                Renderer r = visRs[i];
                if (r == null)
                    continue;
                r.renderingLayerMask = mask;
                if (donor != null)
                {
                    r.lightProbeUsage = donor.lightProbeUsage;
                    r.reflectionProbeUsage = donor.reflectionProbeUsage;
                }
            }
        }

        private static bool IsExactKozuchName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string n = name!;
            return n.Equals("KozuchTorpedos", StringComparison.OrdinalIgnoreCase) ||
                   n.Equals("Kozuch", StringComparison.OrdinalIgnoreCase) ||
                   n.IndexOf("Кожух", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CopyGloss(Material? src, Material dst)
        {
            if (src == null || dst == null)
                return;
            if (src.HasProperty("_Metallic") && dst.HasProperty("_Metallic"))
                dst.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            if (src.HasProperty("_Glossiness") && dst.HasProperty("_Smoothness"))
                dst.SetFloat("_Smoothness", src.GetFloat("_Glossiness"));
            else if (src.HasProperty("_Smoothness") && dst.HasProperty("_Smoothness"))
                dst.SetFloat("_Smoothness", src.GetFloat("_Smoothness"));
            if (src.HasProperty("_Glossiness") && dst.HasProperty("_Glossiness"))
                dst.SetFloat("_Glossiness", src.GetFloat("_Glossiness"));
        }




        private static void KillEmission(Material mat)
        {
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", Color.black);
            if (mat.HasProperty("_EmissiveColor"))
                mat.SetColor("_EmissiveColor", Color.black);
            if (mat.HasProperty("_EmissionMap"))
                mat.SetTexture("_EmissionMap", null);
            if (mat.HasProperty("_EmissiveColorMap"))
                mat.SetTexture("_EmissiveColorMap", null);
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        private static Texture? PeekAlbedo(Material? mat)
        {
            if (mat == null)
                return null;
            if (mat.HasProperty("_BaseMap"))
            {
                Texture t = mat.GetTexture("_BaseMap");
                if (t != null)
                    return t;
            }
            if (mat.HasProperty("_MainTex"))
            {
                Texture t = mat.GetTexture("_MainTex");
                if (t != null)
                    return t;
            }
            return null;
        }

        private static void WriteAlbedo(Material mat, Texture tex)
        {
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                VisualShader.ResetSt(mat, "_BaseMap");
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
                VisualShader.ResetSt(mat, "_MainTex");
            }
            if (mat.HasProperty("_BaseColorMap"))
            {
                mat.SetTexture("_BaseColorMap", tex);
                VisualShader.ResetSt(mat, "_BaseColorMap");
            }
        }

        private static Texture2D? EnsureKozuchTexture()
        {
            if (_texTried)
                return _kozuch;
            _texTried = true;

            try
            {
                string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(dir))
                    return null;

                string path = Path.Combine(dir, TorpedoConstants.KozuchTextureFileName);
                if (!File.Exists(path))
                    path = Path.Combine(dir, "Textures", "KozuchTorpedos.png");
                if (!File.Exists(path))
                {
                    HydraPlugin.ModLog?.LogWarning($"Missing {TorpedoConstants.KozuchTextureFileName} next to DLL.");
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear: false);
                if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
                {
                    HydraPlugin.ModLog?.LogWarning("Kozuch texture LoadImage failed.");
                    return null;
                }

                tex.name = "KozuchTorpedoTexture";
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.anisoLevel = 4;
                _kozuch = tex;
                HydraPlugin.ModLog?.LogInfo($"Kozuch texture loaded ({tex.width}x{tex.height})");
                return _kozuch;
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogError($"Kozuch texture: {ex.Message}");
                return null;
            }
        }
    }
}
