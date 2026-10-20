using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static LearnSceneBuilder;

/// <summary>
/// SettingsPanelBuilder
/// Editor tool for the gear chip and the Options scene. Menu: Tools > Settings > "Build gear button and Options scene". It
///  - draws the gear icon (Assets/Main/Sprites/1 Menu/Main Menu/main_menu_gear.png) if it is missing,
///  - turns the "music_chip" (the round chip left of Profile) into "settings_chip": the gear icon, and MusicToggleButton is removed; the chip
///    opens the Options scene (SceneNavigator.LoadOptionsMenu) and the old settings window inside the Main menu is removed,
///  - builds the scene "5 Options" (Assets/Main/Scenes/1 Menu): back arrow and "Options" title chip on the usual sky, and a card with the
///    Sound effects On / Off switch, the Music volume slider with its percentage, the Display choice (Windowed / Fullscreen), the Window size
///    chips (800 x 600, 1024 x 768, 1920 x 1080) and a Done button, driven by SettingsPanel (Done, back arrow and Esc return to "1 Main_menu").
///    The scene is added to Build Settings after the Library scene.
/// Safe to run again: the Options scene is rebuilt.
/// </summary>
public static class SettingsPanelBuilder
{
    private const string MenuPath = "Assets/Main/Scenes/1 Menu/1 Main_menu.unity";
    private const string OptionsPath = "Assets/Main/Scenes/1 Menu/5 Options.unity";
    private const string GearPath = "Assets/Main/Sprites/1 Menu/Main Menu/main_menu_gear.png";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Sand = new Color32(239, 231, 212, 255);

    [MenuItem("Tools/Settings/Build gear button and Options scene")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        EditorSceneManager.SaveOpenScenes();
        Sprite gear = EnsureGear();
        string menu = UpdateMenu(gear);
        string options = BuildOptionsScene();
        AddToBuildSettings(OptionsPath);
        // leave the Main menu open, as the other builders do
        EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        return "Main menu: " + menu + " | Options scene: " + options;
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in scenes)
        {
            if (s.path == path)
            {
                s.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }
        // right after the Library scene, so the menu scenes stay together
        int at = scenes.FindIndex(delegate (EditorBuildSettingsScene s) { return s.path.EndsWith("4 Library_menu.unity"); });
        scenes.Insert(at >= 0 ? at + 1 : scenes.Count, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    /// <summary>Main menu: the gear chip opens the Options scene (like the Profile chip opens its scene); the old settings window is removed.</summary>
    private static string UpdateMenu(Sprite gear)
    {
        var scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        GameObject ui = GameObject.Find("MenuUI");
        GameObject chipGo = GameObject.Find("music_chip");
        if (chipGo == null) chipGo = GameObject.Find("settings_chip");
        if (ui == null || chipGo == null)
        {
            return "SKIPPED (no MenuUI or music chip)";
        }

        // ---- the gear chip
        chipGo.name = "settings_chip";
        var oldToggle = chipGo.GetComponent("MusicToggleButton");
        if (oldToggle != null) Object.DestroyImmediate(oldToggle);
        Image icon = chipGo.transform.Find("icon").GetComponent<Image>();
        icon.sprite = gear;
        icon.color = Color.white;
        icon.preserveAspect = true;
        var chipButton = chipGo.GetComponent<Button>();
        for (int i = chipButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(chipButton.onClick, i);
        }
        GameObject navGo = GameObject.Find("SceneManager");
        SceneNavigator nav = navGo != null ? navGo.GetComponent<SceneNavigator>() : null;
        if (nav != null)
        {
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(chipButton.onClick, nav.LoadOptionsMenu);
        }

        // ---- the old window inside the menu is gone: the options live in their own scene now
        foreach (string old in new[] { "settings_panel", "SettingsPanelController" })
        {
            Transform t = ui.transform.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return nav != null ? "gear chip opens the Options scene" : "gear chip built, but no SceneNavigator found to open the scene";
    }

    /// <summary>"5 Options": back arrow and title chip on the usual sky, and one cream card with the sound, display and window size settings.</summary>
    private static string BuildOptionsScene()
    {
        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("OptionsUI", canvas);
        LibraryUiKit.Fill(ui);
        BuildSky(canvas, ui, bg, "OptionsTheme");

        // ---- top bar
        Image backRing = LibraryUiKit.Icon(ui, "back_button", LibraryUiKit.Circle, Cream);
        LibraryUiKit.Place(backRing.rectTransform, 48, 40, 112, 112);
        Image backArrow = LibraryUiKit.Icon(backRing.transform, "arrow", Spr("main_menu_play_triangle.png"), Ink);
        LibraryUiKit.Place(backArrow.rectTransform, 36, 30, 40, 52);
        backArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        backArrow.rectTransform.anchoredPosition = new Vector2(36f + 20f, -(30f + 26f));
        backArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        Button backButton = MakeButton(backRing);
        TextMeshProUGUI titleLabel;
        Pill(ui, "title_chip", "Options", 190, 52, 270, 88, Cream, Ink, 48, true, out titleLabel);

        // ---- the card
        Image card = LibraryUiKit.Box(ui, "options_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 560, 170, 800, 850);

        // sound
        Lab(card.transform, "Sound effects", 40, Ink, true, TextAlignmentOptions.MidlineLeft, 60, 40, 440, 80);
        TextMeshProUGUI sfxLabel;
        Image sfxTrack = Pill(card.transform, "sfx_switch", "On", 540, 48, 200, 64, Green, Color.white, 34, true, out sfxLabel);
        Button sfxButton = MakeButton(sfxTrack);

        Lab(card.transform, "Music volume", 40, Ink, true, TextAlignmentOptions.MidlineLeft, 60, 155, 440, 60);
        Slider slider = MakeSlider(card.transform, "volume_slider", 60, 225, 560, 56);
        TextMeshProUGUI percent = Lab(card.transform, "60%", 38, Ink, true, TextAlignmentOptions.MidlineRight, 630, 217, 110, 72);
        TextMeshProUGUI hint = Lab(card.transform, "Drag all the way left to turn the music off", 26, InkSoft, false, TextAlignmentOptions.Center, 40, 300, 720, 44);
        hint.fontStyle = FontStyles.Italic;

        // display mode: Windowed or Fullscreen
        Lab(card.transform, "Display", 40, Ink, true, TextAlignmentOptions.MidlineLeft, 60, 378, 190, 70);
        TextMeshProUGUI windowedLabel, fullscreenLabel;
        Image windowedImg = Pill(card.transform, "windowed_chip", "Windowed", 270, 382, 230, 64, Sand, Ink, 32, true, out windowedLabel);
        Button windowedButton = MakeButton(windowedImg);
        Image fullscreenImg = Pill(card.transform, "fullscreen_chip", "Fullscreen", 515, 382, 230, 64, Sand, Ink, 32, true, out fullscreenLabel);
        Button fullscreenButton = MakeButton(fullscreenImg);

        // window size: the three standard sizes
        Lab(card.transform, "Window size", 40, Ink, true, TextAlignmentOptions.MidlineLeft, 60, 480, 440, 60);
        var sizeButtons = new System.Collections.Generic.List<Button>();
        var sizeImages = new System.Collections.Generic.List<Image>();
        var sizeLabels = new System.Collections.Generic.List<TextMeshProUGUI>();
        for (int i = 0; i < ScreenFit.Presets.Length; i++)
        {
            TextMeshProUGUI sizeLabel;
            string text = ScreenFit.Presets[i].x + " x " + ScreenFit.Presets[i].y;
            Image chip = Pill(card.transform, "size_chip_" + (i + 1), text, 60 + i * 240, 550, 220, 64, Sand, Ink, 32, true, out sizeLabel);
            sizeButtons.Add(MakeButton(chip));
            sizeImages.Add(chip);
            sizeLabels.Add(sizeLabel);
        }
        TextMeshProUGUI sizeHint = Lab(card.transform, "Sizes bigger than your screen are dimmed.", 24, InkSoft, false, TextAlignmentOptions.Center, 40, 632, 720, 44);
        sizeHint.fontStyle = FontStyles.Italic;

        TextMeshProUGUI doneLabel;
        Image doneImg = Pill(card.transform, "close_button", "Done", 200, 710, 400, 84, Green, Color.white, 44, true, out doneLabel);
        Button done = MakeButton(doneImg);

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("OptionsController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<SettingsPanel>();
        var so = new SerializedObject(controller);
        SetRef(so, "panel", card.gameObject);
        SetRef(so, "closeButton", done);
        SetRef(so, "backButton", backButton);
        so.FindProperty("returnScene").stringValue = "1 Main_menu";
        so.FindProperty("hideOnStart").boolValue = false;
        SetRef(so, "sfxButton", sfxButton);
        SetRef(so, "sfxTrack", sfxTrack);
        SetRef(so, "sfxLabel", sfxLabel);
        SetRef(so, "volumeSlider", slider);
        SetRef(so, "volumeLabel", percent);
        SetRef(so, "windowedButton", windowedButton);
        SetRef(so, "windowedImage", windowedImg);
        SetRef(so, "windowedLabel", windowedLabel);
        SetRef(so, "fullscreenButton", fullscreenButton);
        SetRef(so, "fullscreenImage", fullscreenImg);
        SetRef(so, "fullscreenLabel", fullscreenLabel);
        SetArray(so, "sizeButtons", sizeButtons);
        SetArray(so, "sizeImages", sizeImages);
        SetArray(so, "sizeLabels", sizeLabels);
        so.ApplyModifiedPropertiesWithoutUndo();
        controller.Refresh();

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), OptionsPath);
        return "saved " + OptionsPath;
    }

    /// <summary>A horizontal slider (0 to 1) with a sand track, a green fill and a round white knob.</summary>
    private static Slider MakeSlider(Transform parent, string name, float x, float y, float w, float h)
    {
        RectTransform root = LibraryUiKit.NewRect(name, parent);
        LibraryUiKit.Place(root, x, y, w, h);
        var slider = root.gameObject.AddComponent<Slider>();

        Image bg = LibraryUiKit.Box(root, "Background", Sand, true);
        bg.raycastTarget = true;
        bg.rectTransform.anchorMin = new Vector2(0f, 0.25f);
        bg.rectTransform.anchorMax = new Vector2(1f, 0.75f);
        bg.rectTransform.offsetMin = Vector2.zero;
        bg.rectTransform.offsetMax = Vector2.zero;
        bg.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        RectTransform fillArea = LibraryUiKit.NewRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.25f);
        fillArea.anchorMax = new Vector2(1f, 0.75f);
        fillArea.offsetMin = new Vector2(h * 0.5f, 0f);
        fillArea.offsetMax = new Vector2(-h * 0.5f, 0f);
        fillArea.pivot = new Vector2(0.5f, 0.5f);
        Image fill = LibraryUiKit.Box(fillArea, "Fill", Green, true);
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        fill.rectTransform.sizeDelta = new Vector2(h, 0f);

        RectTransform slideArea = LibraryUiKit.NewRect("Handle Slide Area", root);
        slideArea.anchorMin = Vector2.zero;
        slideArea.anchorMax = Vector2.one;
        slideArea.offsetMin = new Vector2(h * 0.5f, 0f);
        slideArea.offsetMax = new Vector2(-h * 0.5f, 0f);
        slideArea.pivot = new Vector2(0.5f, 0.5f);
        Image handle = LibraryUiKit.Icon(slideArea, "Handle", LibraryUiKit.Circle, Color.white);
        handle.raycastTarget = true;
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(h, 0f);
        Image ring = LibraryUiKit.Icon(handle.transform, "ring", LibraryUiKit.Circle, Green);
        LibraryUiKit.Place(ring.rectTransform, h * 0.22f, h * 0.22f, h * 0.56f, h * 0.56f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.6f;
        return slider;
    }

    // ------------------------------------------------------------------ gear sprite

    private static Sprite EnsureGear()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(GearPath);
        if (existing != null)
        {
            return existing;
        }
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)(n - 1) - 0.5f, v = y / (float)(n - 1) - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);
                float outer = Mathf.Cos(8f * angle) > 0.25f ? 0.46f : 0.34f;   // 8 teeth
                float edge = 0.012f;
                float body = Mathf.Clamp01((outer - r) / edge + 0.5f);
                float hole = Mathf.Clamp01((r - 0.15f) / edge + 0.5f);
                float a = Mathf.Min(body, hole);
                tex.SetPixel(x, y, new Color(Ink.r, Ink.g, Ink.b, a));
            }
        }
        tex.Apply();
        File.WriteAllBytes(GearPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(GearPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(GearPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(GearPath);
    }
}
