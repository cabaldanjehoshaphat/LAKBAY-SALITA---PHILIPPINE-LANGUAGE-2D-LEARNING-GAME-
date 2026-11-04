using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// WordSearchDesignBuilder
/// Editor tool that creates the two Word Search scenes as real saved scene objects (so the design is visible in the editor before
/// pressing Play), in the same look as the menus, Wordle and Crossword:
///  - "WordSearch - Start": back button, title, language chip, a "Random puzzle" button, seven category cards with puzzle chips
///    A, B, C and an info card, driven by WordSearchStartController;
///  - "WordSearch - Play": the time-of-day sky (MainMenuTheme), top bar (pause, language, stopwatch, found counter), the 10x10 letter
///    grid (WordSearchBoardView), the word list, the Hint button, a toast, the pause window and the win card, driven by
///    WordSearchGameController.
/// Both scenes are saved in Assets/Main/Scenes/4 Word Search and added to Build Settings. Menu: Tools > Word Search > "Build Start and
/// Play scenes". Safe to run again: it recreates both scenes from scratch.
/// </summary>
public static class WordSearchDesignBuilder
{
    private const string SceneFolder = "Assets/Main/Scenes/4 Word Search";
    private const string StartPath = SceneFolder + "/WordSearch - Start.unity";
    private const string PlayPath = SceneFolder + "/WordSearch - Play.unity";
    private const string MaterialPath = "Assets/Main/Sprites/1 Menu/Main Menu/LiberationSans SDF Colorable.mat";
    private const string SpriteFolder = "Assets/Main/Sprites/1 Menu/Main Menu/";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Yellow = new Color32(232, 185, 35, 255);
    private static readonly Color Purple = new Color32(138, 63, 199, 255);
    private static readonly Color Navy = new Color32(27, 58, 140, 255);
    private static readonly Color Orange = new Color32(232, 117, 26, 255);
    private static readonly Color Sand = new Color32(239, 231, 212, 255);
    private static readonly Color Coral = new Color32(217, 96, 59, 255);

    private static Material _mat;

    // ------------------------------------------------------------------ menu entries

    [MenuItem("Tools/Word Search/Build Start and Play scenes")]
    public static void BuildAllMenu()
    {
        Debug.Log(BuildAll());
    }

    /// <summary>Builds both scenes, saves them and adds them to Build Settings. Returns a short summary.</summary>
    public static string BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(SceneFolder))
        {
            AssetDatabase.CreateFolder("Assets/Main/Scenes", "4 Word Search");
        }
        string start = BuildStartScene();
        string play = BuildPlayScene();
        AddToBuildSettings(StartPath);
        AddToBuildSettings(PlayPath);
        AssetDatabase.SaveAssets();
        return "Start: " + start + " | Play: " + play;
    }

    /// <summary>Rebuilds only the start scene (and keeps it in Build Settings). Returns a short summary.</summary>
    public static string BuildStartOnly()
    {
        if (!AssetDatabase.IsValidFolder(SceneFolder))
        {
            AssetDatabase.CreateFolder("Assets/Main/Scenes", "4 Word Search");
        }
        string start = BuildStartScene();
        AddToBuildSettings(StartPath);
        AssetDatabase.SaveAssets();
        return "Start: " + start;
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene s in scenes)
        {
            if (s.path == path)
            {
                s.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ------------------------------------------------------------------ small helpers

    private static Material Mat()
    {
        if (_mat == null)
        {
            _mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }
        return _mat;
    }

    private static Sprite Spr(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + file);
    }

    private static TextMeshProUGUI Lab(Transform parent, string text, float size, Color color, bool bold,
        TextAlignmentOptions align, float x, float y, float w, float h)
    {
        TextMeshProUGUI t = LibraryUiKit.Label(parent, text, size, color, bold ? FontStyles.Bold : FontStyles.Normal, align);
        t.fontSharedMaterial = Mat();
        LibraryUiKit.Place(t.rectTransform, x, y, w, h);
        return t;
    }

    private static Image Pill(Transform parent, string name, string text, float x, float y, float w, float h, Color bg, Color fg,
        float fontSize, bool bold, out TextMeshProUGUI label)
    {
        Image img = LibraryUiKit.Box(parent, name, bg, true);
        LibraryUiKit.Place(img.rectTransform, x, y, w, h);
        label = Lab(img.transform, text, fontSize, fg, bold, TextAlignmentOptions.Center, 0, 0, w, h);
        LibraryUiKit.Fill(label.rectTransform);
        return img;
    }

    private static Button MakeButton(Image target)
    {
        Button b = LibraryUiKit.MakeButton(target, null);
        b.transition = Selectable.Transition.ColorTint;
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        return b;
    }

    private static CanvasGroup NewGroup(Transform parent, string name)
    {
        RectTransform r = LibraryUiKit.NewRect(name, parent);
        LibraryUiKit.Fill(r);
        var cg = r.gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        return cg;
    }

    private static void SetRef(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("WordSearchDesignBuilder: missing field " + name);
            return;
        }
        p.objectReferenceValue = value;
    }

    private static void SetArray<T>(SerializedObject so, string name, IList<T> items) where T : Object
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("WordSearchDesignBuilder: missing array " + name);
            return;
        }
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    private static void SetGameObjects(SerializedObject so, string name, IList<GameObject> items)
    {
        SerializedProperty p = so.FindProperty(name);
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    /// <summary>Creates an empty scene with a camera, a 1920x1080 canvas (screen-space camera), an EventSystem and the sky base colour.</summary>
    private static Transform NewScene(out Image skyBase)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(95, 180, 236, 255);
        camGo.AddComponent<AudioListener>();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.layer = 5;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 100f;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasGo.AddComponent<GraphicRaycaster>();

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var bgGo = new GameObject("sky_base", typeof(RectTransform));
        bgGo.layer = 5;
        bgGo.transform.SetParent(canvasGo.transform, false);
        skyBase = bgGo.AddComponent<Image>();
        skyBase.color = new Color32(95, 180, 236, 255);
        skyBase.raycastTarget = false;
        LibraryUiKit.Fill(skyBase.rectTransform);
        return canvasGo.transform;
    }

    /// <summary>Sky layers (gradient, stars and moon, sun, clouds) plus the MainMenuTheme that fades them by time of day.</summary>
    private static void BuildSky(Transform canvas, RectTransform ui, Image skyBase)
    {
        Image skyTop = LibraryUiKit.Icon(ui, "sky_top", Spr("main_menu_sky_gradient.png"), new Color32(191, 230, 255, 255));
        LibraryUiKit.Fill(skyTop.rectTransform);

        CanvasGroup nightG = NewGroup(ui, "night_group");
        var rnd = new System.Random(21);
        for (int i = 0; i < 40; i++)
        {
            float sx = rnd.Next(20, 1900), sy = rnd.Next(150, 1040), sz = rnd.Next(4, 10);
            Image star = LibraryUiKit.Icon(nightG.transform, "star", LibraryUiKit.Circle, new Color(1f, 1f, 0.92f, 0.8f));
            LibraryUiKit.Place(star.rectTransform, sx, sy, sz, sz);
        }
        Image moon = LibraryUiKit.Icon(nightG.transform, "moon", LibraryUiKit.Circle, new Color32(255, 243, 196, 255));
        LibraryUiKit.Place(moon.rectTransform, 1130, 36, 96, 96);

        CanvasGroup sunG = NewGroup(ui, "sun_group");
        Image sunHalo = LibraryUiKit.Icon(sunG.transform, "sun_halo", LibraryUiKit.Circle, new Color(1f, 0.96f, 0.78f, 0.43f));
        LibraryUiKit.Place(sunHalo.rectTransform, 720, -220, 480, 480);
        Image sunCore = LibraryUiKit.Icon(sunG.transform, "sun_core", LibraryUiKit.Circle, new Color(1f, 0.93f, 0.67f, 0.9f));
        LibraryUiKit.Place(sunCore.rectTransform, 820, -120, 280, 280);

        CanvasGroup cloudG = NewGroup(ui, "clouds_group");
        float[][] cloudDefs = { new[] { 830f, 34f, 0.9f }, new[] { 1240f, 60f, 0.7f } };
        float[][] puffs = { new[] { 0f, 10f, 60f, 24f }, new[] { 14f, 0f, 40f, 30f }, new[] { 36f, 6f, 50f, 26f }, new[] { -10f, 14f, 40f, 18f } };
        for (int i = 0; i < cloudDefs.Length; i++)
        {
            RectTransform cr = LibraryUiKit.NewRect("cloud_" + (i + 1), cloudG.transform);
            LibraryUiKit.Place(cr, cloudDefs[i][0], cloudDefs[i][1], 10, 10);
            float s = cloudDefs[i][2] * 2f;
            foreach (float[] pf in puffs)
            {
                Image pu = LibraryUiKit.Icon(cr, "puff", LibraryUiKit.Circle, new Color(1, 1, 1, 0.82f));
                LibraryUiKit.Place(pu.rectTransform, pf[0] * s, pf[1] * s, pf[2] * s, pf[3] * s);
            }
        }

        RectTransform themeRect = LibraryUiKit.NewRect("WordSearchTheme", canvas);
        LibraryUiKit.Fill(themeRect);
        var theme = themeRect.gameObject.AddComponent<MainMenuTheme>();
        var themeSo = new SerializedObject(theme);
        SetRef(themeSo, "skyBase", skyBase);
        SetRef(themeSo, "skyTopOverlay", skyTop);
        SetRef(themeSo, "clouds", cloudG);
        SetRef(themeSo, "sun", sunG);
        SetRef(themeSo, "night", nightG);
        themeSo.ApplyModifiedPropertiesWithoutUndo();
        theme.LoadDefaultThemes();
        theme.PreviewPeriod(DayPeriod.Noon);
    }

    // ------------------------------------------------------------------ start scene

    private static string BuildStartScene()
    {
        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("WordSearchStartUI", canvas);
        LibraryUiKit.Fill(ui);
        BuildSky(canvas, ui, bg);

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
        Pill(ui, "title_chip", "Word Search", 190, 52, 380, 88, Cream, Ink, 48, true, out titleLabel);
        TextMeshProUGUI langLabel;
        Pill(ui, "language_chip", "Cebuano", 590, 60, 224, 64, Cream, Ink, 36, false, out langLabel);

        // (no Classic / Meaning buttons: the game always plays in Classic mode, see WordSearchSession.MeaningModeAvailable)

        // "Random puzzle" sits in the top right corner, right edge aligned with Wordle's "Random word" button (40 px wider for the longer label)
        TextMeshProUGUI randomLabel;
        Image randomImg = Pill(ui, "random_button", "Random puzzle", 1520, 52, 360, 88, Coral, Color.white, 40, true, out randomLabel);
        Button randomButton = MakeButton(randomImg);

        // ---- category cards: 4 on the first row, 3 + the info card on the second
        string[] names = { "Animals", "Food and drink", "Body", "Family and people", "Home and things", "Nature and weather", "Numbers" };
        Color[] dots =
        {
            Coral, Yellow, Green, Purple, new Color32(55, 138, 221, 255), new Color32(29, 158, 117, 255), Orange
        };
        var categoryNames = new List<TMP_Text>();
        var categoryProgress = new List<TMP_Text>();
        var chipButtons = new List<Button>();
        var chipImages = new List<Image>();
        for (int i = 0; i < 7; i++)
        {
            float x = 60 + (i % 4) * 460;
            float y = 216 + (i / 4) * 420;
            Image card = LibraryUiKit.Box(ui, "category_" + (i + 1), Cream, true);
            LibraryUiKit.Place(card.rectTransform, x, y, 420, 380);
            Image dot = LibraryUiKit.Icon(card.transform, "dot", LibraryUiKit.Circle, dots[i]);
            LibraryUiKit.Place(dot.rectTransform, 28, 28, 72, 72);
            TextMeshProUGUI nameLabel = Lab(card.transform, names[i], 50, Ink, true, TextAlignmentOptions.TopLeft, 28, 116, 364, 110);
            nameLabel.enableWordWrapping = true;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 30f;
            nameLabel.fontSizeMax = 50f;
            categoryNames.Add(nameLabel);
            categoryProgress.Add(Lab(card.transform, "0 of 3 solved", 30, InkSoft, false, TextAlignmentOptions.MidlineLeft, 28, 226, 364, 40));
            for (int p = 0; p < 3; p++)
            {
                TextMeshProUGUI chipLabel;
                // the three puzzles of a category are Level 1 to Level 3: a small "Level" above a big number
                Image chip = Pill(card.transform, "chip_" + (p + 1), "<size=48%>Level</size>\n<size=84%>" + (p + 1) + "</size>", 28 + p * 126, 282, 112, 72, Sand, Ink, 44, true, out chipLabel);
                chipLabel.lineSpacing = -18f;
                chipLabel.enableWordWrapping = false;
                Button chipButton = MakeButton(chip);
                chipButtons.Add(chipButton);
                chipImages.Add(chip);
            }
        }

        Image info = LibraryUiKit.Box(ui, "info_card", Navy, true);
        LibraryUiKit.Place(info.rectTransform, 60 + 3 * 460, 216 + 420, 420, 380);
        Lab(info.transform, "Pick a topic", 52, Yellow, true, TextAlignmentOptions.TopLeft, 28, 36, 364, 70);
        TextMeshProUGUI infoText = Lab(info.transform, "Then tap Level 1, 2 or 3. Gold chips are solved levels.", 30, Color.white, false,
            TextAlignmentOptions.TopLeft, 28, 118, 364, 130);
        infoText.enableWordWrapping = true;
        infoText.overflowMode = TextOverflowModes.Overflow;

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("WordSearchStartController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<WordSearchStartController>();
        var so = new SerializedObject(controller);
        SetRef(so, "languageLabel", langLabel);
        SetRef(so, "backButton", backButton);
        SetRef(so, "randomButton", randomButton);
        SetArray(so, "categoryNames", categoryNames);
        SetArray(so, "categoryProgress", categoryProgress);
        SetArray(so, "chipButtons", chipButtons);
        SetArray(so, "chipImages", chipImages);
        so.ApplyModifiedPropertiesWithoutUndo();
        controllerRect.SetAsLastSibling();
        controller.Refresh();

        Scene scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, StartPath);
        return "saved " + StartPath + " (" + chipButtons.Count + " chips)";
    }

    // ------------------------------------------------------------------ play scene

    private static string BuildPlayScene()
    {
        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("WordSearchUI", canvas);
        LibraryUiKit.Fill(ui);
        BuildSky(canvas, ui, bg);

        // ---- board group: grid card, word list, hint button (hidden while paused)
        CanvasGroup boardGroup = NewGroup(ui, "Board");
        boardGroup.interactable = true;
        boardGroup.blocksRaycasts = true;

        Image gridCard = LibraryUiKit.Box(boardGroup.transform, "grid_card", Cream, true);
        LibraryUiKit.Place(gridCard.rectTransform, 60, 176, 860, 860);

        Image gridImage = LibraryUiKit.Box(gridCard.transform, "grid", new Color(0f, 0f, 0f, 0f), false);
        gridImage.raycastTarget = true;
        LibraryUiKit.Place(gridImage.rectTransform, 20, 20, 820, 820);
        RectTransform capsuleLayer = LibraryUiKit.NewRect("capsules", gridImage.transform);
        LibraryUiKit.Fill(capsuleLayer);
        RectTransform letterLayer = LibraryUiKit.NewRect("letters", gridImage.transform);
        LibraryUiKit.Fill(letterLayer);
        var cellLabels = new List<TMP_Text>();
        for (int r = 0; r < WordSearchGrid.Size; r++)
        {
            for (int c = 0; c < WordSearchGrid.Size; c++)
            {
                TextMeshProUGUI cell = Lab(letterLayer, "A", 48, Ink, true, TextAlignmentOptions.Center, c * 82, r * 82, 82, 82);
                cell.name = "cell_" + r + "_" + c;
                cellLabels.Add(cell);
            }
        }
        var boardView = gridImage.gameObject.AddComponent<WordSearchBoardView>();
        var bso = new SerializedObject(boardView);
        SetRef(bso, "boardRect", gridImage.rectTransform);
        SetRef(bso, "capsuleLayer", capsuleLayer);
        SetArray(bso, "cellLabels", cellLabels);
        bso.ApplyModifiedPropertiesWithoutUndo();

        Image listCard = LibraryUiKit.Box(boardGroup.transform, "list_card", Cream, true);
        LibraryUiKit.Place(listCard.rectTransform, 960, 176, 900, 700);
        TextMeshProUGUI listTitle = Lab(listCard.transform, "Words", 44, Ink, true, TextAlignmentOptions.MidlineLeft, 28, 14, 844, 64);
        var rowRoots = new List<GameObject>();
        var rowLabels = new List<TMP_Text>();
        var rowDots = new List<Image>();
        for (int i = 0; i < 8; i++)
        {
            Image row = LibraryUiKit.Box(listCard.transform, "row_" + (i + 1), Sand, true);
            LibraryUiKit.Place(row.rectTransform, 28, 92 + i * 74, 844, 64);
            Image dot = LibraryUiKit.Icon(row.transform, "dot", LibraryUiKit.Circle, new Color32(214, 205, 183, 255));
            LibraryUiKit.Place(dot.rectTransform, 16, 16, 32, 32);
            TextMeshProUGUI label = Lab(row.transform, "WORD · 5 letters", 36, Ink, false, TextAlignmentOptions.MidlineLeft, 68, 0, 764, 64);
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 36f;
            rowRoots.Add(row.gameObject);
            rowLabels.Add(label);
            rowDots.Add(dot);
        }

        TextMeshProUGUI hintLabel;
        Image hintImg = Pill(boardGroup.transform, "hint_button", "Hint · 3", 960, 900, 900, 100, Yellow, Ink, 48, true, out hintLabel);
        Button hintButton = MakeButton(hintImg);

        // ---- top bar
        Image pauseRing = LibraryUiKit.Icon(ui, "pause_button", LibraryUiKit.Circle, Orange);
        LibraryUiKit.Place(pauseRing.rectTransform, 48, 40, 112, 112);
        Image pauseInner = LibraryUiKit.Icon(pauseRing.transform, "inner", LibraryUiKit.Circle, Cream);
        LibraryUiKit.Place(pauseInner.rectTransform, 6, 6, 100, 100);
        Image bar1 = LibraryUiKit.Box(pauseRing.transform, "bar_1", Orange, true);
        LibraryUiKit.Place(bar1.rectTransform, 38, 30, 14, 52);
        Image bar2 = LibraryUiKit.Box(pauseRing.transform, "bar_2", Orange, true);
        LibraryUiKit.Place(bar2.rectTransform, 60, 30, 14, 52);
        Button pauseButton = MakeButton(pauseRing);

        TextMeshProUGUI langLabel;
        Pill(ui, "language_chip", "Cebuano", 230, 60, 224, 64, Cream, Ink, 36, false, out langLabel);

        Image timerPill = LibraryUiKit.Box(ui, "timer_pill", Cream, true);
        LibraryUiKit.Place(timerPill.rectTransform, 480, 48, 300, 88);
        Image clock = LibraryUiKit.Icon(timerPill.transform, "clock", LibraryUiKit.Circle, new Color32(143, 208, 90, 255));
        LibraryUiKit.Place(clock.rectTransform, 22, 16, 56, 56);
        Image handA = LibraryUiKit.Box(clock.transform, "hand_a", new Color32(39, 80, 10, 255), false);
        LibraryUiKit.Place(handA.rectTransform, 26, 10, 5, 20);
        Image handB = LibraryUiKit.Box(clock.transform, "hand_b", new Color32(39, 80, 10, 255), false);
        LibraryUiKit.Place(handB.rectTransform, 26, 25, 16, 5);
        TextMeshProUGUI timerLabel = Lab(timerPill.transform, "00:00", 52, Ink, true, TextAlignmentOptions.Center, 90, 0, 190, 88);

        TextMeshProUGUI foundLabel;
        Pill(ui, "found_pill", "Found 0 of 8", 1500, 48, 380, 88, Cream, Ink, 40, true, out foundLabel);

        // ---- toast
        Image toastBox = LibraryUiKit.Box(ui, "toast", new Color(0.23f, 0.16f, 0.1f, 0.94f), true);
        LibraryUiKit.Place(toastBox.rectTransform, 800, 56, 680, 72);
        TextMeshProUGUI toastLabel = Lab(toastBox.transform, "", 30, Cream, false, TextAlignmentOptions.Center, 0, 0, 680, 72);
        LibraryUiKit.Fill(toastLabel.rectTransform);
        toastLabel.enableAutoSizing = true;
        toastLabel.fontSizeMin = 20f;
        toastLabel.fontSizeMax = 30f;
        var toast = toastBox.gameObject.AddComponent<HubToast>();
        var toastSo = new SerializedObject(toast);
        SetRef(toastSo, "label", toastLabel);
        toastSo.ApplyModifiedPropertiesWithoutUndo();
        toastBox.gameObject.SetActive(false);

        // ---- overlays: win card and pause window
        RectTransform overlays = LibraryUiKit.NewRect("WordSearchOverlays", canvas);
        LibraryUiKit.Fill(overlays);

        Image resultDim = LibraryUiKit.Box(overlays, "result_panel", new Color(0.07f, 0.16f, 0.36f, 0.55f), false);
        LibraryUiKit.Fill(resultDim.rectTransform);
        resultDim.raycastTarget = true;
        RectTransform confetti = LibraryUiKit.NewRect("confetti", resultDim.transform);
        LibraryUiKit.Fill(confetti);
        Color[] confettiColors = { Yellow, Green, Orange, Purple };
        var crnd = new System.Random(5);
        for (int i = 0; i < 18; i++)
        {
            Image piece = LibraryUiKit.Box(confetti, "piece_" + i, confettiColors[i % 4], true);
            LibraryUiKit.Place(piece.rectTransform, crnd.Next(80, 1840), crnd.Next(0, 1000), 28, 16);
            piece.rectTransform.localRotation = Quaternion.Euler(0f, 0f, crnd.Next(0, 180));
        }
        Image card = LibraryUiKit.Box(resultDim.transform, "card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 400, 120, 1120, 920);
        card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        card.rectTransform.anchoredPosition = new Vector2(400 + 560, -(120 + 460));
        TextMeshProUGUI resultTitle;
        Pill(card.transform, "ribbon", "Husto! All found", 200, -48, 720, 108, Navy, Color.white, 52, true, out resultTitle);
        Lab(card.transform, "Words you found", 36, InkSoft, false, TextAlignmentOptions.Center, 0, 84, 1120, 50);

        RectTransform learnedContent;
        LibraryUiKit.MakeScroll(card.transform, "learned_scroll", 56, 140, 1008, 500, out learnedContent);
        learnedContent.name = "learned_list";
        var vlg = learnedContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var fitter = learnedContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image rowTemplate = LibraryUiKit.Box(learnedContent, "RowTemplate", Sand, true);
        rowTemplate.rectTransform.sizeDelta = new Vector2(0f, 72f);
        Lab(rowTemplate.transform, "WORD", 38, Ink, true, TextAlignmentOptions.MidlineLeft, 24, 0, 270, 72).name = "Word";
        TextMeshProUGUI meaning = Lab(rowTemplate.transform, "meaning", 30, InkSoft, false, TextAlignmentOptions.MidlineLeft, 302, 0, 600, 72);
        meaning.name = "Meaning";
        meaning.enableWordWrapping = true;
        meaning.enableAutoSizing = true;
        meaning.fontSizeMin = 20f;
        meaning.fontSizeMax = 30f;
        Image starIcon = LibraryUiKit.Icon(rowTemplate.transform, "Star", LibraryUiKit.Star, new Color32(185, 183, 172, 255));
        LibraryUiKit.Place(starIcon.rectTransform, 930, 12, 48, 48);
        MakeButton(starIcon);
        rowTemplate.gameObject.SetActive(false);

        Lab(card.transform, "Tap a star to save a word to your favorites", 28, InkSoft, false, TextAlignmentOptions.Center, 0, 652, 1120, 40);

        RectTransform stats = LibraryUiKit.NewRect("stats", card.transform);
        LibraryUiKit.Place(stats, 56, 704, 1008, 92);
        TextMeshProUGUI statTime, statHints, statStreak;
        MakeStat(stats, "stat_time", "Time", 0, 300, Sand, Ink, out statTime);
        MakeStat(stats, "stat_hints", "Hints used", 334, 340, Sand, Ink, out statHints);
        MakeStat(stats, "stat_streak", "Streak", 708, 300, new Color32(251, 226, 196, 255), new Color32(122, 58, 10, 255), out statStreak);

        TextMeshProUGUI nextLabel;
        Image nextImg = Pill(card.transform, "next_button", "Next puzzle", 56, 816, 480, 84, Green, Color.white, 40, true, out nextLabel);
        Button resultNext = MakeButton(nextImg);
        TextMeshProUGUI libLabel;
        Image libImg = Pill(card.transform, "library_button", "Library", 556, 816, 260, 84, Purple, Color.white, 36, true, out libLabel);
        Button resultLibrary = MakeButton(libImg);
        Image backOuter = LibraryUiKit.Box(card.transform, "back_button", Ink, true);
        LibraryUiKit.Place(backOuter.rectTransform, 836, 816, 228, 84);
        Image backInner = LibraryUiKit.Box(backOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(backInner.rectTransform, 4, 4, 220, 76);
        TextMeshProUGUI backLabel = Lab(backInner.transform, "Games", 36, Ink, false, TextAlignmentOptions.Center, 0, 0, 220, 76);
        LibraryUiKit.Fill(backLabel.rectTransform);
        Button resultBack = MakeButton(backOuter);

        // pause window
        Image pauseDim = LibraryUiKit.Box(overlays, "pause_panel", new Color(0.07f, 0.16f, 0.36f, 0.45f), false);
        LibraryUiKit.Fill(pauseDim.rectTransform);
        pauseDim.raycastTarget = true;
        Image pauseCard = LibraryUiKit.Box(pauseDim.transform, "card", Cream, true);
        LibraryUiKit.Place(pauseCard.rectTransform, 580, 120, 760, 840);
        TextMeshProUGUI pausedTitle;
        Pill(pauseCard.transform, "ribbon", "Paused", 140, -40, 480, 104, Navy, Color.white, 56, true, out pausedTitle);
        Image pauseIcon = LibraryUiKit.Icon(pauseCard.transform, "icon", LibraryUiKit.Circle, new Color32(251, 226, 196, 255));
        LibraryUiKit.Place(pauseIcon.rectTransform, 312, 108, 136, 136);
        Image pb1 = LibraryUiKit.Box(pauseIcon.transform, "bar_1", Orange, true);
        LibraryUiKit.Place(pb1.rectTransform, 38, 36, 24, 64);
        Image pb2 = LibraryUiKit.Box(pauseIcon.transform, "bar_2", Orange, true);
        LibraryUiKit.Place(pb2.rectTransform, 74, 36, 24, 64);
        Lab(pauseCard.transform, "Your timer is stopped", 40, InkSoft, false, TextAlignmentOptions.Center, 0, 262, 760, 56);
        Image timePill = LibraryUiKit.Box(pauseCard.transform, "time_pill", Sand, true);
        LibraryUiKit.Place(timePill.rectTransform, 110, 336, 540, 112);
        TextMeshProUGUI pauseTime = Lab(timePill.transform, "00:00", 60, Ink, true, TextAlignmentOptions.Center, 0, 0, 540, 112);
        LibraryUiKit.Fill(pauseTime.rectTransform);
        TextMeshProUGUI resumeLabel;
        Image resumeImg = Pill(pauseCard.transform, "resume_button", "Resume", 80, 480, 600, 112, Green, Color.white, 52, true, out resumeLabel);
        Button resumeButton = MakeButton(resumeImg);
        TextMeshProUGUI chooseLabel;
        Image chooseImg = Pill(pauseCard.transform, "choose_puzzle_button", "Choose puzzle", 80, 616, 600, 92, Purple, Color.white, 40, true, out chooseLabel);
        Button chooseButton = MakeButton(chooseImg);
        Image pauseBackOuter = LibraryUiKit.Box(pauseCard.transform, "back_button", Ink, true);
        LibraryUiKit.Place(pauseBackOuter.rectTransform, 80, 728, 600, 80);
        Image pauseBackInner = LibraryUiKit.Box(pauseBackOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(pauseBackInner.rectTransform, 4, 4, 592, 72);
        TextMeshProUGUI pauseBackLabel = Lab(pauseBackInner.transform, "Back to games", 38, Ink, false, TextAlignmentOptions.Center, 0, 0, 592, 72);
        LibraryUiKit.Fill(pauseBackLabel.rectTransform);
        Button pauseBackButton = MakeButton(pauseBackOuter);

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("WordSearchController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<WordSearchGameController>();
        var ko = new SerializedObject(controller);
        SetRef(ko, "board", boardView);
        SetRef(ko, "toast", toast);
        SetRef(ko, "languageLabel", langLabel);
        SetRef(ko, "timerLabel", timerLabel);
        SetRef(ko, "foundLabel", foundLabel);
        SetRef(ko, "pauseButton", pauseButton);
        SetRef(ko, "boardGroup", boardGroup);
        SetRef(ko, "listTitle", listTitle);
        SetGameObjects(ko, "rowRoots", rowRoots);
        SetArray(ko, "rowLabels", rowLabels);
        SetArray(ko, "rowDots", rowDots);
        SetRef(ko, "hintButton", hintButton);
        SetRef(ko, "hintLabel", hintLabel);
        SetRef(ko, "pausePanel", pauseDim.gameObject);
        SetRef(ko, "pauseTimeLabel", pauseTime);
        SetRef(ko, "resumeButton", resumeButton);
        SetRef(ko, "pauseChooseButton", chooseButton);
        SetRef(ko, "pauseBackButton", pauseBackButton);
        SetRef(ko, "resultPanel", resultDim.gameObject);
        SetRef(ko, "resultCard", card.rectTransform);
        SetRef(ko, "resultTitle", resultTitle);
        SetRef(ko, "resultListContent", learnedContent);
        SetRef(ko, "resultRowTemplate", rowTemplate.gameObject);
        SetRef(ko, "statTime", statTime);
        SetRef(ko, "statHints", statHints);
        SetRef(ko, "statStreak", statStreak);
        SetRef(ko, "resultNextButton", resultNext);
        SetRef(ko, "resultLibraryButton", resultLibrary);
        SetRef(ko, "resultBackButton", resultBack);
        SetRef(ko, "confettiRoot", confetti);
        ko.ApplyModifiedPropertiesWithoutUndo();

        // panels start hidden in the saved scene
        resultDim.gameObject.SetActive(false);
        pauseDim.gameObject.SetActive(false);

        overlays.SetAsLastSibling();
        controllerRect.SetAsLastSibling();

        // show the first puzzle in edit mode so the design is visible before Play
        WordSearchSession.Language = 0;
        WordSearchSession.Category = 0;
        WordSearchSession.Puzzle = 0;
        controller.RefreshPreview();

        Scene scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, PlayPath);
        return "saved " + PlayPath + " (" + cellLabels.Count + " letters, " + rowRoots.Count + " list rows)";
    }

    private static void MakeStat(Transform parent, string name, string caption, float x, float w, Color bg, Color valueColor,
        out TextMeshProUGUI value)
    {
        Image pill = LibraryUiKit.Box(parent, name, bg, true);
        LibraryUiKit.Place(pill.rectTransform, x, 0, w, 92);
        Lab(pill.transform, caption, 28, InkSoft, false, TextAlignmentOptions.Center, 0, 4, w, 36);
        value = Lab(pill.transform, "-", 40, valueColor, true, TextAlignmentOptions.Center, 0, 38, w, 50);
    }
}
