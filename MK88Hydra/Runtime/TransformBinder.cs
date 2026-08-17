using System;
using UnityEngine;

namespace Hydra.Runtime
{
    internal static class TransformBinder
    {
        internal static Transform? FindByAliases(Transform root, string[] aliases)
        {
            if (root == null || aliases == null || aliases.Length == 0)
                return null;

            Transform? exact = null;
            Transform? contains = null;
            FindRecursive(root, aliases, ref exact, ref contains, substringFallback: true);
            return exact != null ? exact : contains;
        }

        /// <summary>Exact node name only — avoids "EW" matching WarewindVisual.</summary>
        internal static Transform? FindExactByAliases(Transform root, string[] aliases)
        {
            if (root == null || aliases == null || aliases.Length == 0)
                return null;

            Transform? exact = null;
            Transform? unused = null;
            FindRecursive(root, aliases, ref exact, ref unused, substringFallback: false);
            return exact;
        }

        internal static void CollectByAliases(Transform root, string[] aliases, System.Collections.Generic.List<Transform> results)
        {
            if (root == null || aliases == null || results == null)
                return;
            CollectRecursive(root, aliases, results, exactOnly: true);
        }

        private static void FindRecursive(
            Transform t, string[] aliases, ref Transform? exact, ref Transform? contains, bool substringFallback)
        {
            string n = t.name ?? string.Empty;
            foreach (string a in aliases)
            {
                if (string.IsNullOrEmpty(a))
                    continue;
                if (string.Equals(n, a, StringComparison.OrdinalIgnoreCase))
                {
                    exact = t;
                    return;
                }
                if (substringFallback && contains == null && n.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)
                    contains = t;
            }

            for (int i = 0; i < t.childCount; i++)
            {
                FindRecursive(t.GetChild(i), aliases, ref exact, ref contains, substringFallback);
                if (exact != null)
                    return;
            }
        }

        private static void CollectRecursive(Transform t, string[] aliases, System.Collections.Generic.List<Transform> results, bool exactOnly)
        {
            string n = t.name ?? string.Empty;
            foreach (string a in aliases)
            {
                if (string.IsNullOrEmpty(a))
                    continue;
                bool hit = string.Equals(n, a, StringComparison.OrdinalIgnoreCase) ||
                           (!exactOnly && n.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit)
                {
                    results.Add(t);
                    break;
                }
            }
            for (int i = 0; i < t.childCount; i++)
                CollectRecursive(t.GetChild(i), aliases, results, exactOnly);
        }
    }
}
