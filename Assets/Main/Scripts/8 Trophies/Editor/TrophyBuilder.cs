using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static LearnSceneBuilder;

/// <summary>
/// TrophyBuilder
/// Editor tool for the leaderboards and achievements. Menu: Tools > Trophies > "Build Trophy screen and buttons". It
///  - makes the trophy and medal pictures in Assets/Main/Resources/Trophies usable as sprites,
///  - builds the scene "6 Trophies" (Assets/Main/Scenes/1 Menu): back arrow, title chip, three tabs (Leaderboard, My records, Badges), a status chip and one empty root per tab
///    on the usual sky, driven by TrophyRoomController, and adds it to Build Settings after "5 Options",
///  - adds a round trophy button on the Main menu, left of the gear (x 1360, y 48, 96 x 96), which opens the scene,
///  - on the Profile screen narrows the "Words learned" card (x 700, y 860, 266 x 172), adds a Badges card beside it (x 986, y 860, 266 x 172) and a rank chip at the top right
///    (x 1440, y 56, 400 x 88), driven by BadgeRankCard. ProfileDesignBuilder calls AddToOpenProfile at the end of a rebuild, so they are never lost.
/// Safe to run again: earlier ones are replaced. All coordinates are in the 1920x1080 layout, measured from the top left.
/// </summary>
public static class TrophyBuilder
{
    private const string ScenePath = "Assets/Main/Scenes/1 Menu/6 Trophies.unity";
    private const string MenuPath = "Assets/Main/Scenes/1 Menu/1 Main_menu.unity";
    private const string ProfilePath = "Assets/Main/Scenes/1 Menu/2 Profile Icon.unity";
    private const string TrophySprite = "Assets/Main/Resources/Trophies/trophy.png";
    private const string MedalSprite = "Assets/Main/Resources/Trophies/medal.png";
    private const string BackSprite = "Assets/Main/Sprites/1 Menu/back_button.png";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Orange = new Color32(232, 116, 28, 255);
    private static readonly Color Purple = new Color32(138, 64, 199, 255);

    [MenuItem("Tools/Trophies/Build Trophy screen and buttons")]
    public static void BuildMenu()
    {
        Debug.Log(BuildAll());
    }

    public static string BuildAll()
    {
        EnsureSprite(TrophySprite);
        EnsureSprite(MedalSprite);
        string scene = BuildScene();
        string menu = AddToMenu();

        var profile = EditorSceneManager.OpenScene(ProfilePath, OpenSceneMode.Single);
        string profileResult = AddToOpenProfile();
        EditorSceneManager.MarkSceneDirty(profile);
        EditorSceneManager.SaveScene(profile);
        return scene + " | " + menu + " | " + profileResult;
    }

    private static void EnsureSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static string BuildScene()
    {
        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("TrophiesUI", canvas);
        LibraryUiKit.Fill(ui);
        BuildSky(canvas, ui, bg, "TrophiesTheme");

        // ---- top bar
        Sprite arrow = AssetDatabase.LoadAssetAtPath<Sprite>(BackSprite);
        Image back;
        if (arrow != null)
        {
            back = LibraryUiKit.Icon(ui, "back_button", arrow, Color.white);
            back.preserveAspect = true;
            LibraryUiKit.Place(back.rectTransform, 42, 36, 138, 128);
        }
        else
        {
            back = LibraryUiKit.Icon(ui, "back_button", LibraryUiKit.Circle, Cream);
            LibraryUiKit.Place(back.rectTransform, 48, 40, 112, 112);
            Image triangle = LibraryUiKit.Icon(back.transform, "arrow", Spr("main_menu_play_triangle.png"), Ink);
            LibraryUiKit.Place(triangle.rectTransform, 36, 30, 40, 52);
            triangle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            triangle.rectTransform.anchoredPosition = new Vector2(56f, -56f);
            triangle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }
        Button backButton = MakeButton(back);

        TextMeshProUGUI title;
        Pill(ui, "title_chip", "Leaderboard", 216, 56, 430, 88, Cream, Ink, 48, true, out title);

        // ---- the three tabs
        string[] names = { "Leaderboard", "My records", "Badges" };
        float[] widths = { 226, 206, 150 };
        float x = 664;
        var buttons = new List<Button>();
        var images = new List<Image>();
        var labels = new List<TMP_Text>();
        for (int i = 0; i < 3; i++)
        {
            TextMeshProUGUI label;
            Image img = Pill(ui, "tab_" + i, names[i], x, 64, widths[i], 72, Cream, Ink, 29, false, out label);
            buttons.Add(MakeButton(img));
            images.Add(img);
            labels.Add(label);
            x += widths[i] + 8;
        }

        // ---- status chip
        Image status = LibraryUiKit.Box(ui, "status_chip", Cream, true);
        LibraryUiKit.Place(status.rectTransform, 1266, 56, 574, 88);
        Image dot = LibraryUiKit.Icon(status.transform, "dot", LibraryUiKit.Circle, Orange);
        LibraryUiKit.Place(dot.rectTransform, 28, 34, 20, 20);
        TextMeshProUGUI statusLabel = Lab(status.transform, "", 28, InkSoft, false, TextAlignmentOptions.MidlineLeft, 60, 0, 500, 88);
        statusLabel.richText = true;

        // ---- the tab contents are built by the controller
        var roots = new List<RectTransform>();
        for (int i = 0; i < 3; i++)
        {
            RectTransform root = LibraryUiKit.NewRect("tab_root_" + i, ui);
            LibraryUiKit.Fill(root);
            roots.Add(root);
        }

        RectTransform controllerRect = LibraryUiKit.NewRect("TrophyController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<TrophyRoomController>();
        var so = new SerializedObject(controller);
        SetRef(so, "backButton", backButton);
        SetRef(so, "titleLabel", title);
        SetRef(so, "statusLabel", statusLabel);
        SetRef(so, "statusDot", dot);
        SetArray(so, "tabRoots", roots);
        SetArray(so, "tabButtons", buttons);
        SetArray(so, "tabImages", images);
        SetArray(so, "tabLabels", labels);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AddToBuildSettings();
        return "built " + ScenePath;
    }

    private static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath))
        {
            return;
        }
        int at = scenes.FindIndex(s => s.path.EndsWith("5 Options.unity"));
        scenes.Insert(at >= 0 ? at + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static string AddToMenu()
    {
        var scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        GameObject gear = GameObject.Find("settings_chip");
        GameObject navGo = GameObject.Find("SceneManager");
        SceneNavigator nav = navGo != null ? navGo.GetComponent<SceneNavigator>() : null;
        if (gear == null || nav == null)
        {
            return "SKIPPED main menu (settings chip or SceneNavigator not found)";
        }
        Transform parent = gear.transform.parent;
        Transform old = parent.Find("trophy_chip");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Image chip = LibraryUiKit.Box(parent, "trophy_chip", Cream, true);
        LibraryUiKit.Place(chip.rectTransform, 1360, 48, 96, 96);
        Image icon = LibraryUiKit.Icon(chip.transform, "icon", AssetDatabase.LoadAssetAtPath<Sprite>(TrophySprite), Orange);
        LibraryUiKit.Place(icon.rectTransform, 19, 19, 58, 58);
        Button button = MakeButton(chip);
        UnityEventTools.AddVoidPersistentListener(button.onClick, nav.LoadTrophies);
        chip.transform.SetSiblingIndex(gear.transform.GetSiblingIndex());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "trophy button added to the Main menu";
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>Adds the badges card and the rank chip to the Profile scene that is open.</summary>
    public static string AddToOpenProfile()
    {
        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            return "SKIPPED trophies (no Canvas)";
        }
        Transform learned = FindDeep(canvasGo.transform, "words_learned_card");
        if (learned == null)
        {
            return "SKIPPED trophies (the Profile screen was not found)";
        }
        Transform ui = learned.parent;
        foreach (string name in new[] { "badges_card", "rank_chip", "TrophyProfileController" })
        {
            Transform old = FindDeep(canvasGo.transform, name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // ---- the words learned card becomes half as wide
        LibraryUiKit.Place((RectTransform)learned, 700, 860, 266, 172);
        foreach (TMP_Text label in learned.GetComponentsInChildren<TMP_Text>(true))
        {
            var r = label.rectTransform;
            r.sizeDelta = new Vector2(232f, r.sizeDelta.y);
            label.overflowMode = TextOverflowModes.Overflow;
        }

        // ---- the badges card
        Image card = LibraryUiKit.Box(ui, "badges_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 986, 860, 266, 172);
        Lab(card.transform, "Badges", 30, InkSoft, false, TextAlignmentOptions.MidlineLeft, 28, 20, 210, 40);
        TextMeshProUGUI value = Lab(card.transform, "0<size=40>/16</size>", 76, Ink, true, TextAlignmentOptions.MidlineLeft, 28, 66, 170, 90);
        value.richText = true;
        value.overflowMode = TextOverflowModes.Overflow;
        Image medal = LibraryUiKit.Icon(card.transform, "medal", AssetDatabase.LoadAssetAtPath<Sprite>(MedalSprite), Color.white);
        LibraryUiKit.Place(medal.rectTransform, 182, 74, 72, 72);
        Button badges = MakeButton(card);
        card.transform.SetSiblingIndex(learned.GetSiblingIndex() + 1);

        // ---- the rank chip
        Image chip = LibraryUiKit.Box(ui, "rank_chip", Cream, true);
        LibraryUiKit.Place(chip.rectTransform, 1440, 56, 400, 88);
        Image trophy = LibraryUiKit.Icon(chip.transform, "icon", AssetDatabase.LoadAssetAtPath<Sprite>(TrophySprite), Purple);
        LibraryUiKit.Place(trophy.rectTransform, 42, 19, 50, 50);
        TextMeshProUGUI rank = Lab(chip.transform, "Leaderboard", 32, Ink, true, TextAlignmentOptions.MidlineLeft, 104, 0, 280, 88);
        Button rankButton = MakeButton(chip);

        // ---- the logic
        RectTransform controllerRect = LibraryUiKit.NewRect("TrophyProfileController", canvasGo.transform);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<BadgeRankCard>();
        var so = new SerializedObject(controller);
        SetRef(so, "badgesValue", value);
        SetRef(so, "badgesButton", badges);
        SetRef(so, "rankLabel", rank);
        SetRef(so, "rankButton", rankButton);
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---- the reset warning mentions what else is cleared
        foreach (TMP_Text label in canvasGo.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.text.StartsWith("This clears your name, avatar, solved counts, learned phrases"))
            {
                label.text = label.text.Replace("solved counts, learned phrases", "solved counts, scores, badges, learned phrases");
                EditorUtility.SetDirty(label);
            }
        }
        Transform overlays = FindDeep(canvasGo.transform, "ProfileOverlays");
        if (overlays != null) overlays.SetAsLastSibling();
        controllerRect.SetAsLastSibling();
        EditorUtility.SetDirty(canvasGo);
        return "badges card and rank chip added to the Profile";
    }
}
