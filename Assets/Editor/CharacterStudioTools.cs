using Starquill.Studio;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// Tools > Character Studio: create the studio scene and build the app.
/// The scene is never added to the game's build scene list.
public static class CharacterStudioTools
{
    public const string ScenePath = "Assets/Scenes/CharacterStudio.unity";
    private const string LinuxPath = "Builds/CharacterStudio/Linux/StarquillCharacters.x86_64";
    private const string WindowsPath = "Builds/CharacterStudio/Windows/StarquillCharacters.exe";

    [MenuItem("Tools/Character Studio/Create Scene")]
    public static void CreateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; // never drop unsaved work
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.13f, 0.13f, 0.15f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        new GameObject("StudioApp", typeof(StudioApp));

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Character Studio scene saved: " + ScenePath);
    }

    [MenuItem("Tools/Character Studio/Build Linux")]
    public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, LinuxPath);

    [MenuItem("Tools/Character Studio/Build Windows")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsPath);

    /// Unity sometimes marks an open scene modified with no real change
    /// (seen 2026-11-08); a build then stops on a save prompt. A scene whose
    /// content equals its file is reopened from disk; real changes still prompt.
    private static void ClearFalseUnsavedFlags()
    {
        if (SceneManager.sceneCount != 1) return;
        var scene = SceneManager.GetActiveScene();
        if (!scene.isDirty || string.IsNullOrEmpty(scene.path)) return;
        const string copy = "Assets/__studio_unsaved_check.unity";
        EditorSceneManager.SaveScene(scene, copy, true);
        bool same = System.IO.File.ReadAllText(copy) == System.IO.File.ReadAllText(scene.path);
        AssetDatabase.DeleteAsset(copy);
        if (same) EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
    }

    public static BuildReport Build(BuildTarget target, string path)
    {
        var named = NamedBuildTarget.Standalone;
        string productName = PlayerSettings.productName;
        var backend = PlayerSettings.GetScriptingBackend(named);
        var mode = PlayerSettings.fullScreenMode;
        int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
        bool resizable = PlayerSettings.resizableWindow;
        var activeGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        var activeTarget = EditorUserBuildSettings.activeBuildTarget;
        try
        {
            PlayerSettings.productName = "Starquill Characters";
            if (backend != ScriptingImplementation.Mono2x) // setting it writes a line even when unchanged
                PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;

            ClearFalseUnsavedFlags();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.CompressWithLz4HC
            });
            var sum = report.summary;
            Debug.Log($"Character Studio build {sum.result}: {path} ({sum.totalSize / (1024 * 1024)} MB, {sum.totalTime.TotalSeconds:0} s)");
            return report;
        }
        finally
        {
            PlayerSettings.productName = productName;
            if (backend != ScriptingImplementation.Mono2x)
                PlayerSettings.SetScriptingBackend(named, backend);
            PlayerSettings.fullScreenMode = mode;
            PlayerSettings.defaultScreenWidth = width;
            PlayerSettings.defaultScreenHeight = height;
            PlayerSettings.resizableWindow = resizable;
            AssetDatabase.SaveAssets(); // the build saved the studio values to ProjectSettings.asset; write the game's back
            // BuildPlayer leaves the Editor on the built platform (on Windows, the IAP package then asks to install the Xbox GDK).
            if (EditorUserBuildSettings.activeBuildTarget != activeTarget)
                EditorUserBuildSettings.SwitchActiveBuildTarget(activeGroup, activeTarget);
        }
    }
}
