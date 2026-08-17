using System;
using System.Reflection;
using Mirage;
using Hydra.Bootstrap;
using UnityEngine;

namespace Hydra.Runtime
{
    /// <summary>
    /// Runtime Instantiate+DontDestroyOnLoad copies SceneId → Mirage treats the GO as a scene object.
    /// Spawn then logs "already been spawned" (NetID 0) and destroys the missile the same frame.
    /// </summary>
    internal static class NetworkPrefabPrep
    {
        private static readonly FieldInfo? SpawnedFromInstantiateField =
            typeof(NetworkIdentity).GetField("<SpawnedFromInstantiate>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void PrepareTemplate(GameObject root)
        {
            if (root == null)
                return;

            try
            {
                StripChildIdentitiesUnderVisual(root);
                NetworkIdentity[] ids = root.GetComponentsInChildren<NetworkIdentity>(true);
                for (int i = 0; i < ids.Length; i++)
                    ResetUnspawned(ids[i]);
            }
            catch (Exception ex)
            {
                HydraPlugin.ModLog?.LogError($"NetworkPrefabPrep.PrepareTemplate: {ex}");
            }
        }

        internal static void ResetUnspawned(NetworkIdentity? identity)
        {
            if (identity == null || identity.IsSpawned)
                return;

            identity.ClearSceneId();
            SpawnedFromInstantiateField?.SetValue(identity, false);
        }

        internal static void LogState(string tag, GameObject root)
        {
            if (root == null)
                return;
            NetworkIdentity? ni = root.GetComponent<NetworkIdentity>() ??
                                  root.GetComponentInChildren<NetworkIdentity>(true);
            if (ni == null)
            {
                HydraPlugin.ModLog?.LogWarning($"[{tag}] '{root.name}' has no NetworkIdentity");
                return;
            }

            HydraPlugin.ModLog?.LogInfo(
                $"[{tag}] '{root.name}' hash={ni.PrefabHash} sceneId={ni.SceneId} sceneObj={ni.IsSceneObject} prefab={ni.IsPrefab} spawned={ni.IsSpawned} fromInst={ni.SpawnedFromInstantiate} netId={ni.NetId}");
        }

        private static void StripChildIdentitiesUnderVisual(GameObject root)
        {
            NetworkIdentity[] ids = root.GetComponentsInChildren<NetworkIdentity>(true);
            for (int i = ids.Length - 1; i >= 0; i--)
            {
                NetworkIdentity ni = ids[i];
                if (ni == null || ni.gameObject == root)
                    continue;
                if (!PrefabFactory.IsVisualRoot(ni.transform))
                    continue;
                UnityEngine.Object.DestroyImmediate(ni);
            }
        }

    }
}
