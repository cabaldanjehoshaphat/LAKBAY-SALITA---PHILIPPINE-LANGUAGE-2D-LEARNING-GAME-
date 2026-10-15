using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static LearnSceneBuilder;

/// <summary>
/// SettingsPanelBuilder
/// Editor tool that replaces the music chip of the main menu with a gear button and builds the settings window behind it. Menu:
/// Tools > Settings > "Build gear button and settings window". It
///  - draws the gear icon (Assets/Main/Sprites/1 Menu/Main Menu/main_menu_gear.png) if it is missing,
///  - turns the "music_chip" (the round chip left of Profile) into "settings_chip": the gear icon, and MusicToggleButton is removed,
///  - builds the hidden settings window "settings_panel" under MenuUI: a Settings ribbon, the Sound effects On / Off switch, the Music
///    volume slider with its percentage, and a Done button, and adds SettingsPanel to the main menu to drive it.
/// Safe to run again: the window is rebuilt.
/// </summary>
public static class SettingsPanelBuilder
{
    private const string MenuPath = "Assets/Main/Scenes/1 Menu/1 Main_menu.unity";
    private const string GearPath = "Assets/Main/Sprites/1 Menu/Main Menu/main_menu_gear.png";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Sand = new Color32(239, 231, 212, 255);
    private static readonly Color Navy = new Color32(30, 58, 138, 255);

    [MenuItem("Tools/Settings/Build gear button and settings window")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        EditorSceneManager.SaveOpenScenes();
        Sprite gear = EnsureGear();
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

        // ---- the window
        Transform old = ui.transform.Find("settings_panel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform oldController = ui.transform.Find("SettingsPanelController");
        if (oldController != null) Object.DestroyImmediate(oldController.gameObject);

        Image dim = LibraryUiKit.Box(ui.transform, "settings_panel", new Color(0.08f, 0.13f, 0.24f, 0.5f), false);
        LibraryUiKit.Fill(dim.rectTransform);
        dim.raycastTarget = true;
        Image card = LibraryUiKit.Box(dim.transform, "settings_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 560, 230, 800, 600);
        TextMeshProUGUI ribbonLabel;
        Pill(card.transform, "ribbon", "Settings", 200, -48, 400, 100, Navy, Color.white, 54, true, out ribbonLabel);

        Lab(card.transform, "Sound effects", 40, Ink, true, TextAlignmentOptions.MidlineLeft, 60, 120, 440, 80);
        TextMeshProUGUI sfxLabel;
        Image sfxTrack = Pill(card.transform, "sfx_switch", "On", 540, 128, 200, 64, Green, Color.white, 34, true, out sfxLabel);
        Button sfxButton = MakeButton(sfxTrack);

        Lab(card.transform, "Music volume", 40, Ink, true, TextAlignmentOptions.MidlineLeft, 60, 250, 440, 60);
        Slider slider = MakeSlider(card.transform, "volume_slider", 60, 330, 560, 56);
        TextMeshProUGUI percent = Lab(card.transform, "60%", 38, Ink, true, TextAlignmentOptions.MidlineRight, 630, 322, 110, 72);
        TextMeshProUGUI hint = Lab(card.transform, "Drag all the way left to turn the music off", 26, InkSoft, false, TextAlignmentOptions.Center, 40, 420, 720, 44);
        hint.fontStyle = FontStyles.Italic;

        TextMeshProUGUI doneLabel;
        Image doneImg = Pill(card.transform, "close_button", "Done", 200, 480, 400, 84, Green, Color.white, 44, true, out doneLabel);
        Button done = MakeButton(doneImg);
        dim.gameObject.SetActive(false);

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("SettingsPanelController", ui.transform);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<SettingsPanel>();
        var so = new SerializedObject(controller);
        SetRef(so, "panel", dim.gameObject);
        SetRef(so, "openButton", chipButton);
        SetRef(so, "closeButton", done);
        SetRef(so, "sfxButton", sfxButton);
        SetRef(so, "sfxTrack", sfxTrack);
        SetRef(so, "sfxLabel", sfxLabel);
        SetRef(so, "volumeSlider", slider);
        SetRef(so, "volumeLabel", percent);
        so.ApplyModifiedPropertiesWithoutUndo();
        controllerRect.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "gear chip and settings window built";
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
