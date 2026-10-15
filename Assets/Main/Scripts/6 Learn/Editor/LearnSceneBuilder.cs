using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// LearnSceneBuilder
/// Editor tool that sets up the "learn the words" idea as real saved scenes. Menu: Tools > Learn > "Build Learn scene and buttons".
///  - Creates "Learn - Cards" (Assets/Main/Scenes/6 Learn): the Learn screen in the same look as the other menus, driven by LearnController
///    (back button, language and topic chips, set chips A/B/C, a big word card with its English meaning and a dot per word, Back / Next /
///    Start puzzle buttons) and adds it to Build Settings.
///  - Adds a "Learn" button (LearnTopicButton) to every topic card of "Wordle - Start" and "Crossword - Start" (next to Play, which gets
///    narrower) and to every card of "WordSearch - Start" (small button at the top right of the card), and adds a hint to the info card.
///  - Adds a Save star next to the meaning on the Wordle result card of "Wordle - Play" and wires it to WordleGameController.
/// Safe to run again: existing Learn buttons and the star are replaced, the Learn scene is rebuilt.
/// </summary>
public static class LearnSceneBuilder
{
    private const string SceneFolder = "Assets/Main/Scenes/6 Learn";
    private const string LearnPath = SceneFolder + "/Learn - Cards.unity";
    private const string WordleStartPath = "Assets/Main/Scenes/2 Wordle/Wordle - Start.unity";
    private const string WordlePlayPath = "Assets/Main/Scenes/2 Wordle/Wordle - Play.unity";
    private const string CrosswordStartPath = "Assets/Main/Scenes/3 Crossword/Crossword - Start.unity";
    private const string WordSearchStartPath = "Assets/Main/Scenes/4 Word Search/WordSearch - Start.unity";
    private const string MaterialPath = "Assets/Main/Sprites/1 Menu/Main Menu/LiberationSans SDF Colorable.mat";
    private const string SpriteFolder = "Assets/Main/Sprites/1 Menu/Main Menu/";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Amber = new Color32(245, 166, 35, 255);
    private static readonly Color Sand = new Color32(239, 231, 212, 255);

    private static Material _mat;

    [MenuItem("Tools/Learn/Build Learn scene and buttons")]
    public static void BuildAllMenu()
    {
        Debug.Log(BuildAll());
    }

    /// <summary>Builds the Learn scene, installs the Learn buttons and the Wordle Save star. Returns a short summary.</summary>
    public static string BuildAll()
    {
        EditorSceneManager.SaveOpenScenes();
        string learn = BuildLearnScene();
        string a = InstallTopicButtons(WordleStartPath, LearnGame.Wordle, true);
        string b = InstallTopicButtons(CrosswordStartPath, LearnGame.Crossword, true);
        string c = InstallTopicButtons(WordSearchStartPath, LearnGame.WordSearch, false);
        string star = InstallWordleSaveStar();
        AddToBuildSettings(LearnPath);
        AssetDatabase.SaveAssets();
        return "Learn: " + learn + " | Wordle start: " + a + " | Crossword start: " + b + " | Word Search start: " + c + " | Wordle result: " + star;
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

    public static Material Mat()
    {
        if (_mat == null)
        {
            _mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }
        return _mat;
    }

    public static Sprite Spr(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + file);
    }

    public static TextMeshProUGUI Lab(Transform parent, string text, float size, Color color, bool bold,
        TextAlignmentOptions align, float x, float y, float w, float h)
    {
        TextMeshProUGUI t = LibraryUiKit.Label(parent, text, size, color, bold ? FontStyles.Bold : FontStyles.Normal, align);
        t.fontSharedMaterial = Mat();
        LibraryUiKit.Place(t.rectTransform, x, y, w, h);
        return t;
    }

    public static Image Pill(Transform parent, string name, string text, float x, float y, float w, float h, Color bg, Color fg,
        float fontSize, bool bold, out TextMeshProUGUI label)
    {
        Image img = LibraryUiKit.Box(parent, name, bg, true);
        LibraryUiKit.Place(img.rectTransform, x, y, w, h);
        label = Lab(img.transform, text, fontSize, fg, bold, TextAlignmentOptions.Center, 0, 0, w, h);
        LibraryUiKit.Fill(label.rectTransform);
        return img;
    }

    public static Button MakeButton(Image target)
    {
        Button b = LibraryUiKit.MakeButton(target, null);
        b.transition = Selectable.Transition.ColorTint;
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        return b;
    }

    public static CanvasGroup NewGroup(Transform parent, string name)
    {
        RectTransform r = LibraryUiKit.NewRect(name, parent);
        LibraryUiKit.Fill(r);
        var cg = r.gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        return cg;
    }

    public static void SetRef(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("LearnSceneBuilder: missing field " + name);
            return;
        }
        p.objectReferenceValue = value;
    }

    public static void SetArray<T>(SerializedObject so, string name, IList<T> items) where T : Object
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("LearnSceneBuilder: missing array " + name);
            return;
        }
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    /// <summary>Creates an empty scene with a camera, a 1920x1080 canvas (screen-space camera), an EventSystem and the sky base colour.</summary>
    public static Transform NewScene(out Image skyBase)
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
    public static void BuildSky(Transform canvas, RectTransform ui, Image skyBase, string themeName = "LearnTheme")
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

        RectTransform themeRect = LibraryUiKit.NewRect(themeName, canvas);
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

    // ------------------------------------------------------------------ the Learn scene

    private static string BuildLearnScene()
    {
        if (!AssetDatabase.IsValidFolder(SceneFolder))
        {
            AssetDatabase.CreateFolder("Assets/Main/Scenes", "6 Learn");
        }

        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("LearnUI", canvas);
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
        Pill(ui, "title_chip", "Learn", 190, 52, 230, 88, Cream, Ink, 48, true, out titleLabel);
        TextMeshProUGUI langLabel;
        Pill(ui, "language_chip", "Cebuano", 440, 60, 224, 64, Cream, Ink, 36, false, out langLabel);
        TextMeshProUGUI topicLabel;
        Pill(ui, "topic_chip", "Animals", 684, 52, 440, 88, Cream, Ink, 40, true, out topicLabel);
        topicLabel.enableAutoSizing = true;
        topicLabel.fontSizeMin = 24f;
        topicLabel.fontSizeMax = 40f;
        topicLabel.overflowMode = TextOverflowModes.Overflow;

        TextMeshProUGUI setCaption;
        Pill(ui, "set_caption", "Set", 1440, 60, 120, 64, Cream, InkSoft, 34, false, out setCaption);
        string[] letters = { "A", "B", "C" };
        var setButtons = new List<Button>();
        var setImages = new List<Image>();
        for (int i = 0; i < 3; i++)
        {
            TextMeshProUGUI l;
            Image chip = Pill(ui, "set_" + letters[i], letters[i], 1580 + i * 100, 52, 88, 88, i == 0 ? new Color32(232, 185, 35, 255) : Sand, Ink, 46, true, out l);
            setImages.Add(chip);
            setButtons.Add(MakeButton(chip));
        }

        // ---- word card
        Image card = LibraryUiKit.Box(ui, "word_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 330, 210, 1260, 620);
        TextMeshProUGUI counter = Lab(card.transform, "Word 1 of 7", 34, InkSoft, true, TextAlignmentOptions.MidlineLeft, 50, 34, 520, 50);
        TextMeshProUGUI word = Lab(card.transform, "BAKI", 190, Ink, true, TextAlignmentOptions.Center, 30, 110, 1200, 250);
        word.enableWordWrapping = false;
        word.enableAutoSizing = true;
        word.fontSizeMin = 90f;
        word.fontSizeMax = 190f;
        word.overflowMode = TextOverflowModes.Overflow;
        TextMeshProUGUI meaning = Lab(card.transform, "frog", 80, Green, true, TextAlignmentOptions.Center, 60, 370, 1140, 120);
        meaning.enableAutoSizing = true;
        meaning.fontSizeMin = 36f;
        meaning.fontSizeMax = 80f;
        meaning.overflowMode = TextOverflowModes.Overflow;
        var dots = new List<Image>();
        const int maxDots = 8;
        float dotsX = (1260f - (maxDots * 24f + (maxDots - 1) * 22f)) / 2f;
        for (int i = 0; i < maxDots; i++)
        {
            Image dot = LibraryUiKit.Icon(card.transform, "dot_" + (i + 1), LibraryUiKit.Circle, i == 0 ? Green : Sand);
            LibraryUiKit.Place(dot.rectTransform, dotsX + i * 46f, 545, 24, 24);
            dots.Add(dot);
        }

        // ---- buttons under the card
        TextMeshProUGUI prevLabel, nextLabel, startLabel;
        Image prevImg = Pill(ui, "previous_button", "Back", 330, 880, 300, 96, Cream, Ink, 42, true, out prevLabel);
        Image nextImg = Pill(ui, "next_button", "Next", 930, 880, 300, 96, Amber, Ink, 42, true, out nextLabel);
        Image startImg = Pill(ui, "start_button", "Start puzzle", 1260, 880, 330, 96, Green, Color.white, 42, true, out startLabel);
        Button prevButton = MakeButton(prevImg);
        Button nextButton = MakeButton(nextImg);
        Button startButton = MakeButton(startImg);

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("LearnController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<LearnController>();
        var so = new SerializedObject(controller);
        SetRef(so, "backButton", backButton);
        SetRef(so, "languageLabel", langLabel);
        SetRef(so, "topicLabel", topicLabel);
        SetArray(so, "setButtons", setButtons);
        SetArray(so, "setImages", setImages);
        SetRef(so, "counterLabel", counter);
        SetRef(so, "wordLabel", word);
        SetRef(so, "meaningLabel", meaning);
        SetArray(so, "dots", dots);
        SetRef(so, "previousButton", prevButton);
        SetRef(so, "nextButton", nextButton);
        SetRef(so, "startButton", startButton);
        so.ApplyModifiedPropertiesWithoutUndo();
        controllerRect.SetAsLastSibling();

        LearnSession.Game = LearnGame.Wordle;
        LearnSession.Language = 0;
        LearnSession.Category = 0;
        LearnSession.Set = 0;
        controller.Refresh();

        Scene scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, LearnPath);
        return "saved " + LearnPath;
    }

    // ------------------------------------------------------------------ Learn buttons on the start scenes

    private static string InstallTopicButtons(string scenePath, LearnGame game, bool splitPlay)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int added = 0;
        for (int i = 0; i < 7; i++)
        {
            GameObject cardGo = GameObject.Find("category_" + (i + 1));
            if (cardGo == null)
            {
                continue;
            }
            Transform card = cardGo.transform;
            Transform old = card.Find("learn_button");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            Image img;
            TextMeshProUGUI label;
            if (splitPlay)
            {
                Transform play = card.Find("play_button");
                if (play != null)
                {
                    LibraryUiKit.Place((RectTransform)play, 212, 262, 180, 92);
                }
                img = Pill(card, "learn_button", "Learn", 28, 262, 170, 92, Amber, Ink, 42, true, out label);
            }
            else
            {
                img = Pill(card, "learn_button", "Learn", 244, 34, 148, 56, Amber, Ink, 30, true, out label);
            }
            MakeButton(img);
            var topic = img.gameObject.AddComponent<LearnTopicButton>();
            topic.game = game;
            topic.category = i;
            added++;
        }

        // hint on the info card
        GameObject info = GameObject.Find("info_card");
        if (info != null)
        {
            foreach (TMP_Text t in info.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.fontSize < 40f && !t.text.Contains("Learn"))
                {
                    t.text = t.text + " New to a topic? Tap Learn first.";
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return added + " Learn buttons";
    }

    // ------------------------------------------------------------------ Wordle result card: Save star

    private static string InstallWordleSaveStar()
    {
        Scene scene = EditorSceneManager.OpenScene(WordlePlayPath, OpenSceneMode.Single);
        var controller = Object.FindObjectOfType<WordleGameController>();
        if (controller == null)
        {
            return "SKIPPED (no WordleGameController)";
        }
        var so = new SerializedObject(controller);
        var meaning = so.FindProperty("resultMeaning").objectReferenceValue as TMP_Text;
        if (meaning == null)
        {
            return "SKIPPED (no result meaning label)";
        }
        Transform card = meaning.transform.parent;

        Transform old = card.Find("save_star");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        // the meaning on its own: big and green
        meaning.fontSize = 44f;
        meaning.fontStyle = FontStyles.Bold;
        meaning.color = Green;
        LibraryUiKit.Place(meaning.rectTransform, 120, 408, 720, 62);

        Image star = LibraryUiKit.Icon(card, "save_star", LibraryUiKit.Star, new Color32(185, 183, 172, 255));
        LibraryUiKit.Place(star.rectTransform, 862, 410, 58, 58);
        star.raycastTarget = true;
        Button starButton = LibraryUiKit.MakeButton(star, null);
        starButton.transition = Selectable.Transition.None;
        starButton.navigation = new Navigation { mode = Navigation.Mode.None };

        SetRef(so, "resultSaveButton", starButton);
        SetRef(so, "resultSaveStar", star);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "star added";
    }
}
