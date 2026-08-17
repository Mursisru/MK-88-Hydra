using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Hydra.Blueprinter
{
    /// <summary>Loads TorpedoVisual from MK88Hydra.nobp (reuses an already-loaded bundle if present).</summary>
    internal static class NobpContent
    {
        private static AssetBundle? _bundle;
        private static GameObject? _visualPrefab;
        private static bool _tried;

        internal static GameObject? VisualPrefab => _visualPrefab;

        internal static void TryLoad()
        {
            if (_tried)
                return;
            _tried = true;

            try
            {
                _bundle = FindLoadedBundle() ?? LoadFromDiskOrEmbedded();
                if (_bundle == null)
                {
                    HydraPlugin.ModLog?.LogWarning("MK88Hydra.nobp not available — hangar/flight mesh stamp skipped.");
                    return;
                }

                _visualPrefab = _bundle.LoadAsset<GameObject>(TorpedoConstants.MeshPrefabAsset);
                if (_visualPrefab == null)
                {
                    GameObject[] all = _bundle.LoadAllAssets<GameObject>();
                    if (all != null)
                    {
                        foreach (GameObject go in all)
                        {
                            if (go == null)
                                continue;
                            if (go.name.IndexOf("Torpedo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                go.name.IndexOf("Realtorpedo", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _visualPrefab = go;
                                break;
                            }
                        }
                    }
                }

                if (_visualPrefab != null)
                    HydraPlugin.ModLog?.LogInfo($"Torpedo visual ready: '{_visualPrefab.name}'");
                else
                    HydraPlugin.ModLog?.LogWarning("nobp loaded but no TorpedoVisual found.");
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogError($"NobpContent: {ex}");
            }
        }

        private static AssetBundle? FindLoadedBundle()
        {
            foreach (AssetBundle b in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (b == null)
                    continue;
                try
                {
                    if (b.Contains(TorpedoConstants.MeshPrefabAsset))
                    {
                        HydraPlugin.ModLog?.LogInfo($"Reusing loaded AssetBundle '{b.name}'");
                        return b;
                    }
                }
                catch
                {
                    // ignore
                }
            }
            return null;
        }

        private static AssetBundle? LoadFromDiskOrEmbedded()
        {
            string? path = FindNobpPath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                AssetBundle? fromFile = AssetBundle.LoadFromFile(path);
                if (fromFile != null)
                {
                    HydraPlugin.ModLog?.LogInfo($"Loaded .nobp from file: {path}");
                    return fromFile;
                }
                HydraPlugin.ModLog?.LogWarning($"LoadFromFile returned null (already loaded?): {path}");
            }

            return LoadEmbeddedNobp();
        }

        private static string? FindNobpPath()
        {
            string? pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(pluginDir))
                return null;

            string? best = null;
            long bestSize = 0;

            void Consider(string candidate)
            {
                if (!File.Exists(candidate))
                    return;
                long len = new FileInfo(candidate).Length;
                if (len < 4096)
                    return;
                if (len > bestSize)
                {
                    bestSize = len;
                    best = candidate;
                }
            }

            Consider(Path.Combine(pluginDir, TorpedoConstants.NobpFileName));
            Consider(Path.Combine(pluginDir, "MissilePack.nobp"));
            Consider(Path.Combine(pluginDir, "missilepack.nobp"));

            foreach (string f in Directory.GetFiles(pluginDir, "*.nobp"))
            {
                string n = Path.GetFileName(f);
                if (n.IndexOf("MK88", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Hydra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("MissilePack", StringComparison.OrdinalIgnoreCase) >= 0)
                    Consider(f);
            }

            return best;
        }

        private static AssetBundle? LoadEmbeddedNobp()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (!name.EndsWith(".nobp", StringComparison.OrdinalIgnoreCase))
                    continue;
                using Stream? stream = asm.GetManifestResourceStream(name);
                if (stream == null)
                    continue;
                using MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                AssetBundle? b = AssetBundle.LoadFromMemory(ms.ToArray());
                if (b != null)
                {
                    HydraPlugin.ModLog?.LogInfo($"Loaded embedded .nobp: {name}");
                    return b;
                }
            }
            return null;
        }
    }
}
