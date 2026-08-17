using System.IO;
using UnityEditor;
using UnityEngine;

public static class BatchBuild
{
    // Unity -batchmode -projectPath UnityBake -executeMethod BatchBuild.Build -quit
    public static void Build()
    {
        Hydra.UnityBake.NobpBundleBuilder.Build();
    }
}
