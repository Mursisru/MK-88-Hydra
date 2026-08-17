using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Optional per-mesh PNGs: Textures/TorpedoMain.png next to DLL or in plugin folder.</summary>
    internal static class VisualPartTextures
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static string? _texDir;
        private static bool _dirTried;

        internal static Texture2D? ForMesh(string meshName)
        {
            if (string.IsNullOrEmpty(meshName))
                return null;

            if (Cache.TryGetValue(meshName, out Texture2D hit))
                return hit;

            EnsureDir();
            if (string.IsNullOrEmpty(_texDir))
                return null;

            string[] candidates =
            {
                Path.Combine(_texDir, meshName + ".png"),
                Path.Combine(_texDir, meshName + ".jpg"),
                Path.Combine(_texDir, meshName + "_Mat.png")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!File.Exists(candidates[i]))
                    continue;

                try
                {
                    byte[] bytes = File.ReadAllBytes(candidates[i]);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear: false);
                    if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
                        continue;
                    tex.name = meshName;
                    tex.wrapMode = TextureWrapMode.Repeat;
                    tex.filterMode = FilterMode.Bilinear;
                    Cache[meshName] = tex;
                    HydraPlugin.ModLog?.LogInfo($"Part texture '{meshName}' from {candidates[i]}");
                    return tex;
                }
                catch (Exception ex)
                {
                    HydraPlugin.ModLog?.LogWarning($"Part texture {candidates[i]}: {ex.Message}");
                }
            }

            Cache[meshName] = null!;
            return null;
        }

        private static void EnsureDir()
        {
            if (_dirTried)
                return;
            _dirTried = true;

            string? plugin = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(plugin))
                return;

            string local = Path.Combine(plugin, "Textures");
            if (Directory.Exists(local))
            {
                _texDir = local;
                return;
            }

            string flat = Path.Combine(plugin, "PartTextures");
            if (Directory.Exists(flat))
                _texDir = flat;
        }
    }
}
