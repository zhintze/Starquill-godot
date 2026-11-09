using System.IO;
using Starquill.Display;
using Starquill.Studio.UI;
using UnityEngine;

namespace Starquill.Studio
{
    /// Entry point of the CharacterStudio scene. Builds every screen in code.
    public sealed class StudioApp : MonoBehaviour
    {
        private StudioSettings settings;
        private Canvas canvas;
        private DiskSpriteSource sprites;
        private StudioRenderer studioRenderer;

        public StudioSettings Settings => settings;
        public StudioData Data { get; private set; }
        public StudioRenderer Renderer => studioRenderer;
        private PresetStore presetFiles;
        public PresetStore PresetFiles => presetFiles ??= PresetStore.Default; // lazy: Application.dataPath is not allowed in a MonoBehaviour constructor
        /// Problems found while loading presets (P27), shown in the Warnings list.
        public System.Collections.Generic.List<string> LoadWarnings { get; } = new System.Collections.Generic.List<string>();
        public int WarningCount => Data.Issues.Count + Renderer.Resolver.MissingPaths.Count + LoadWarnings.Count;

        private void Start()
        {
            settings = StudioSettings.Load(StudioSettings.DefaultPath);
            if (Application.isEditor && !StudioData.LooksLikeRepo(settings.repoRoot))
                settings.repoRoot = Directory.GetParent(Application.dataPath).FullName;

            StudioUi.EnsureEventSystem();
            canvas = StudioUi.CreateCanvas("StudioCanvas");
            var bg = StudioUi.Panel(canvas.transform, "Background", StudioUi.Background);
            StudioUi.Stretch(bg);

            if (StudioData.LooksLikeRepo(settings.repoRoot)) OpenRepo(settings.repoRoot);
            else ShowRepoPicker();
        }

        public void ShowRepoPicker()
        {
            ClearScreens();
            new RepoPickerView(canvas.transform, settings.repoRoot, OpenRepo);
        }

        private void OpenRepo(string root)
        {
            ClearScreens();
            studioRenderer?.Dispose();
            sprites?.Dispose();

            settings.repoRoot = root;
            SaveSettings();
            Data = StudioData.Load(root);
            sprites = new DiskSpriteSource(root);
            studioRenderer = new StudioRenderer(Data, new ImageResolver(sprites), transform);
            new BulkScreen(canvas.transform, this);
        }

        public void SaveSettings()
        {
            try { settings.Save(StudioSettings.DefaultPath); }
            catch (IOException e) { Debug.LogWarning("Could not save studio settings: " + e.Message); }
        }

        private void ClearScreens()
        {
            for (int i = canvas.transform.childCount - 1; i >= 1; i--) // keep Background
                Destroy(canvas.transform.GetChild(i).gameObject);
        }

        private void OnDestroy()
        {
            studioRenderer?.Dispose();
            sprites?.Dispose();
        }
    }
}
