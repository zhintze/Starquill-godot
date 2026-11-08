using System.IO;
using System.Linq;
using Starquill.Data;
using Starquill.Data.Art;
using Starquill.Display;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Turns Assets/Data/ArtTables/*.csv + Assets/Resources/Images into
/// Assets/Resources/ArtTables.asset (generated, gitignored).
/// Runs: on Editor load if the asset is missing, whenever a CSV or art PNG
/// changes, before every build (fails the build on table errors), and from
/// Tools > Art Tables.
public static class ArtTablesBaker
{
    public const string AssetPath = "Assets/Resources/ArtTables.asset";

    [MenuItem("Tools/Art Tables/Bake Now")]
    public static void BakeFromMenu() => Bake();

    [MenuItem("Tools/Art Tables/Check Images (duplicates, empty)")]
    public static void AuditFromMenu()
    {
        var issues = Audit();
        foreach (var i in issues) Debug.LogWarning("Art images: " + i);
        Debug.Log($"Art images checked: {issues.Count} warning(s).");
    }

    public static ArtBakeResult Bake()
    {
        var result = ArtTablesConverter.Convert(ArtRepo.ReadSources(ProjectRoot));

        var asset = AssetDatabase.LoadAssetAtPath<ArtTables>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ArtTables>();
            asset.data = result.Data;
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        else
        {
            asset.data = result.Data;
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();

        foreach (var issue in result.Issues)
        {
            if (issue.level == ArtIssueLevel.Error) Debug.LogError("Art tables: " + issue);
            else Debug.LogWarning("Art tables: " + issue);
        }
        int errors = result.Issues.Count(i => i.level == ArtIssueLevel.Error);
        Debug.Log($"Art tables baked: {result.Data.equipment.Count} equipment, {result.Data.weapons.Count} weapons, " +
                  $"{result.Data.species.Count} species, {result.Data.partGroups.Count} part groups; " +
                  $"{errors} error(s), {result.Issues.Count - errors} warning(s).");
        return result;
    }

    public static System.Collections.Generic.List<ArtIssue> Audit()
    {
        var src = ArtRepo.ReadSources(ProjectRoot);
        return ArtImageAudit.Run(Path.Combine(ProjectRoot, ArtRepo.ImagesDir), src.ImageFiles);
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    internal static bool IsArtPath(string p) =>
        p.StartsWith(ArtRepo.TablesDir + "/") || p.StartsWith(ArtRepo.ImagesDir + "/");
}

[InitializeOnLoad]
internal static class ArtTablesBootstrap
{
    static ArtTablesBootstrap()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ArtTablesBaker.AssetPath)) ArtTablesBaker.Bake();
        };
    }
}

internal class ArtTablesPostprocessor : AssetPostprocessor
{
    private static bool pending;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (pending) return;
        if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(ArtTablesBaker.IsArtPath)) return;
        pending = true;
        EditorApplication.delayCall += () =>
        {
            pending = false;
            ArtTablesBaker.Bake();
        };
    }

    /// New art PNGs get the paper-doll sprite settings automatically
    /// (replaces the old manual Tools > Fix Sprite Import Settings).
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtRepo.ImagesDir + "/") || !assetImporter.importSettingsMissing) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 256;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
    }
}

internal class ArtTablesBuildStep : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        foreach (var issue in ArtTablesBaker.Audit()) Debug.LogWarning("Art images: " + issue);
        var result = ArtTablesBaker.Bake();
        if (result.HasErrors)
            throw new BuildFailedException("Art tables have errors (see Console lines starting 'Art tables: Error'). Fix those CSV rows and build again.");
    }
}
