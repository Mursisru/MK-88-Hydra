using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>Contour HUD icon — white strokes, dilated for HUD readability.</summary>
    internal static class HydraWeaponIcon
    {
        private static Sprite? _sprite;
        private static bool _tried;

        internal static Sprite? Get()
        {
            if (_sprite != null)
                return _sprite;
            if (_tried)
                return null;
            _tried = true;

            byte[]? bytes = ReadBytes();
            if (bytes == null || bytes.Length == 0)
            {
                HydraPlugin.ModLog?.LogWarning("Hydra preview icon not found.");
                return null;
            }

            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear: false);
                tex.name = "PreviewHydra";
                tex.filterMode = FilterMode.Bilinear;
                if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
                {
                    UnityEngine.Object.Destroy(tex);
                    HydraPlugin.ModLog?.LogWarning("Hydra preview icon LoadImage failed.");
                    return null;
                }

                DilateStrokesToAlpha(tex);
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

                _sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                _sprite.name = "PreviewHydra";
                HydraPlugin.ModLog?.LogInfo($"Hydra preview icon {tex.width}x{tex.height}");
                return _sprite;
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogWarning($"Hydra preview icon: {ex.Message}");
                return null;
            }
        }

        /// <summary>Line art only: key black, dilate ink, full white alpha.</summary>
        private static void DilateStrokesToAlpha(Texture2D tex)
        {
            int w = tex.width;
            int h = tex.height;
            Color32[] src = tex.GetPixels32();
            int inkMin = TorpedoConstants.PreviewIconInkMin;
            int baseA = TorpedoConstants.PreviewIconAlphaBase;
            int radius = TorpedoConstants.PreviewIconStrokeRadius;
            if (radius < 0)
                radius = 0;

            var ink = new bool[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Color32 c = src[i];
                if (c.a == 0)
                    continue;
                int luma = (c.r * 299 + c.g * 587 + c.b * 114) / 1000;
                ink[i] = luma >= inkMin;
            }

            int radius2 = radius * radius;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x;
                    bool visible = false;
                    for (int ny = y - radius; ny <= y + radius && !visible; ny++)
                    {
                        if ((uint)ny >= (uint)h)
                            continue;
                        int nRow = ny * w;
                        for (int nx = x - radius; nx <= x + radius; nx++)
                        {
                            if ((uint)nx >= (uint)w)
                                continue;
                            int dx = nx - x;
                            int dy = ny - y;
                            if (dx * dx + dy * dy > radius2)
                                continue;
                            if (ink[nRow + nx])
                            {
                                visible = true;
                                break;
                            }
                        }
                    }

                    src[i] = visible
                        ? new Color32(255, 255, 255, (byte)baseA)
                        : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(src);
        }

        private static byte[]? ReadBytes()
        {
            string? path = FindPath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return File.ReadAllBytes(path);

            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream(TorpedoConstants.PreviewIconResource);
            if (s == null)
                return null;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        private static string? FindPath()
        {
            string? pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(pluginDir))
                return null;
            return Path.Combine(pluginDir, TorpedoConstants.PreviewIconFileName);
        }
    }
}
