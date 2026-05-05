using UnityEditor;
using System.IO;

public static class BuildAssetBundles
{
    [MenuItem("Tools/Build KeyViewer AssetBundle")]
    public static void Build()
    {
        string outputDir = "AssetBundles";
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        BuildPipeline.BuildAssetBundles(outputDir,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);

        System.Diagnostics.Process.Start("explorer.exe", Path.GetFullPath(outputDir));
    }
}
