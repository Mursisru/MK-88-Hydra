using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hydra.UnityBake
{
    /// <summary>Builds MK88Hydra.nobp (TorpedoVisual + patch_manifest).</summary>
    public static class NobpBundleBuilder
    {
        private const string PrefabName = "TorpedoVisual";
        private const string OutputName = "MK88Hydra.nobp";

        [MenuItem("Hydra/Build Nobp Bundle")]
        public static void Build()
        {
            string assetsRoot = "Assets/MissilePack";
            string buildDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
            Directory.CreateDirectory(buildDir);

            EnsurePrefab(assetsRoot);
            EnsureManifest(assetsRoot);

            string prefabPath = $"{assetsRoot}/{PrefabName}.prefab";
            string manifestPath = $"{assetsRoot}/patch_manifest.txt";

            List<string> assetNames = new List<string> { prefabPath, manifestPath };
            string matFolder = $"{assetsRoot}/Materials";
            if (AssetDatabase.IsValidFolder(matFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { matFolder }))
                    assetNames.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            string texFolder = $"{assetsRoot}/Textures";
            if (AssetDatabase.IsValidFolder(texFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder }))
                    assetNames.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            string fbxPath = FindAsset($"{assetsRoot}/RealtorpedoTransformMK54L.fbx");
            if (!string.IsNullOrEmpty(fbxPath) && !assetNames.Contains(fbxPath))
                assetNames.Add(fbxPath);

            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = OutputName,
                assetNames = assetNames.ToArray()
            };

            BuildPipeline.BuildAssetBundles(
                buildDir,
                new[] { build },
                BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);

            string produced = Path.Combine(buildDir, OutputName);
            string alt = Path.Combine(buildDir, OutputName.ToLowerInvariant());
            if (!File.Exists(produced) && File.Exists(alt))
                File.Copy(alt, produced, true);

            string pluginRes = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MK88Hydra", "Resources"));
            Directory.CreateDirectory(pluginRes);
            if (File.Exists(produced))
            {
                File.Copy(produced, Path.Combine(pluginRes, OutputName), true);
                string binRel = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MK88Hydra", "bin", "Release"));
                Directory.CreateDirectory(binRel);
                File.Copy(produced, Path.Combine(binRel, OutputName), true);
            }

            string deploy = @"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\MK-88-Hydra";
            Directory.CreateDirectory(deploy);
            if (File.Exists(produced))
            {
                File.Copy(produced, Path.Combine(deploy, OutputName), true);
                File.Copy(produced, Path.Combine(deploy, OutputName.ToLowerInvariant()), true);
            }

            string kozuch = Path.Combine(Application.dataPath, "MissilePack", "KozuchTorpedoTexture.png");
            if (File.Exists(kozuch))
                File.Copy(kozuch, Path.Combine(deploy, "KozuchTorpedoTexture.png"), true);

            Debug.Log($"Hydra: built {produced}");
            AssetDatabase.Refresh();
        }

        private static void EnsurePrefab(string assetsRoot)
        {
            string fbxPath = FindAsset($"{assetsRoot}/RealtorpedoTransformMK54L.fbx");
            if (string.IsNullOrEmpty(fbxPath))
            {
                Debug.LogError("FBX not found under Assets/MissilePack");
                return;
            }

            // Force reimport so English Blender names land in the prefab
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError("Failed to load FBX");
                return;
            }

            GameObject root = UnityEngine.Object.Instantiate(fbx);
            root.name = PrefabName;

            // Strip Blender scene junk that becomes white bloom in-game
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

            Texture2D kozuch = AssetDatabase.LoadAssetAtPath<Texture2D>($"{assetsRoot}/KozuchTorpedoTexture.png");
            Shader lit = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            if (lit == null)
            {
                Debug.LogError("MissilePack: Standard shader not found");
                return;
            }

            string matFolder = $"{assetsRoot}/Materials";
            if (!AssetDatabase.IsValidFolder(matFolder))
                AssetDatabase.CreateFolder(assetsRoot, "Materials");

            Dictionary<string, Material> fbxMats = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Texture> fbxTex = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (sub is Material mat)
                    fbxMats[mat.name] = mat;
                else if (sub is Texture tex)
                    fbxTex[tex.name] = tex;
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || lit == null)
                    continue;

                Material[] src = r.sharedMaterials;
                Material[] dst = new Material[Mathf.Max(1, src.Length)];
                bool isKozuch = IsKozuchName(r.gameObject.name);
                string meshName = r.gameObject.name;

                for (int i = 0; i < dst.Length; i++)
                {
                    Material imported = i < src.Length ? src[i] : null;
                    if (imported == null && fbxMats.TryGetValue(meshName, out Material byName))
                        imported = byName;
                    if (imported == null && fbxMats.TryGetValue(meshName + "_Mat", out Material byNameMat))
                        imported = byNameMat;

                    string matAssetPath = $"{matFolder}/{meshName}_Mat.mat";
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
                    if (mat == null)
                    {
                        mat = imported != null ? new Material(imported) : new Material(lit);
                        mat.name = meshName + "_Mat";
                        AssetDatabase.CreateAsset(mat, matAssetPath);
                    }
                    else if (imported != null)
                    {
                        mat.CopyPropertiesFromMaterial(imported);
                    }

                    if (mat.shader == null || mat.shader.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                        mat.shader = lit;

                    if (isKozuch && kozuch != null)
                    {
                        mat.SetTexture("_MainTex", kozuch);
                        if (mat.HasProperty("_BaseMap"))
                            mat.SetTexture("_BaseMap", kozuch);
                    }
                    else
                    {
                        Texture importAlbedo = imported != null ? PeekAlbedo(imported) : null;
                        if (importAlbedo != null)
                            WriteAlbedo(mat, importAlbedo);
                        else
                        {
                            Texture2D partTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{assetsRoot}/Textures/{meshName}.png");
                            if (partTex == null)
                                partTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{assetsRoot}/Textures/{meshName}.jpg");
                            if (partTex != null)
                                WriteAlbedo(mat, partTex);
                            else if (PeekAlbedo(mat) == null)
                            {
                                foreach (KeyValuePair<string, Texture> kv in fbxTex)
                                {
                                    if (kv.Key.IndexOf(meshName, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        WriteAlbedo(mat, kv.Value);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                    EditorUtility.SetDirty(mat);
                    dst[i] = mat;
                }

                r.sharedMaterials = dst;
            }

            AssetDatabase.SaveAssets();

            string prefabPath = $"{assetsRoot}/{PrefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"Hydra: prefab refreshed from FBX '{fbxPath}'");
        }

        private static void EnsureManifest(string assetsRoot)
        {
            string json =
@"{
  ""modName"": ""MK88Hydra"",
  ""schemaVersion"": 3,
  ""modVersion"": ""0.0.0"",
  ""Patches"": [],
  ""Ops"": [],
  ""Addressables"": []
}";
            string txtPath = Path.Combine(Application.dataPath, "MissilePack", "patch_manifest.txt");
            File.WriteAllText(txtPath, json);
            AssetDatabase.ImportAsset($"{assetsRoot}/patch_manifest.txt");
        }

        private static string FindAsset(string preferred)
        {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), preferred.Replace('/', Path.DirectorySeparatorChar))))
                return preferred;
            string[] guids = AssetDatabase.FindAssets("RealtorpedoTransformMK54L t:Model");
            if (guids.Length == 0)
                return null;
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }

        private static bool IsKozuchName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            return name.IndexOf("Kozuch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Кожух", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture PeekAlbedo(Material mat)
        {
            if (mat == null)
                return null;
            if (mat.HasProperty("_MainTex"))
            {
                Texture t = mat.GetTexture("_MainTex");
                if (t != null)
                    return t;
            }
            if (mat.HasProperty("_BaseMap"))
            {
                Texture t = mat.GetTexture("_BaseMap");
                if (t != null)
                    return t;
            }
            return null;
        }

        private static void WriteAlbedo(Material mat, Texture tex)
        {
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
        }
    }
}
