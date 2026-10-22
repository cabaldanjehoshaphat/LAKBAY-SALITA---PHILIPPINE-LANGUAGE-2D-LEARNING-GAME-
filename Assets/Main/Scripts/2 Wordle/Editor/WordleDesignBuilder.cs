using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// WordleDesignBuilder
/// Editor tool that rebuilds a Wordle puzzle scene in the redesigned look, as real saved scene objects (so the design is
/// visible in the editor before pressing Play). It keeps each scene's own puzzle data (the WordleGenerator object with its
/// verifier JSON, grid size and timer durations) and replaces the old Dialog Panel and timer art with:
///  - a time-of-day sky (reuses MainMenuTheme), clouds, sun / moon and stars,
///  - a top bar (language chip, timer, tries dots, pause button), the clue card with a Hint button,
///  - the letter grid (WordleTile prefab), the on-screen keyboard, a toast, the pause window and the result card,
///  - a WordleGameController wired to all of it.
/// Menu: Tools > Wordle > "Rebuild design in open scene" or "Rebuild design in all 30 level scenes". Running it again is safe:
/// it deletes and recreates only its own objects (WordleUI, WordleOverlays, WordleController, WordleTheme).
/// The original scenes are archived in Assets/Main/Scenes/2 Wordle/Archive.
/// </summary>
public static class WordleDesignBuilder
{
    private const string TilePrefabPath = "Assets/Main/Prefabs/2 Wordle/WordleTile.prefab";
    private const string MaterialPath = "Assets/Main/Sprites/1 Menu/Main Menu/LiberationSans SDF Colorable.mat";
    private const string SpriteFolder = "Assets/Main/Sprites/1 Menu/Main Menu/";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Yellow = new Color32(232, 185, 35, 255);
    private static readonly Color Gray = new Color32(138, 138, 128, 255);
    private static readonly Color Purple = new Color32(138, 63, 199, 255);
    private static readonly Color Navy = new Color32(27, 58, 140, 255);
    private static readonly Color Orange = new Color32(232, 117, 26, 255);

    private static Material _mat;

    // ------------------------------------------------------------------ menu entries

    [MenuItem("Tools/Wordle/Rebuild design in open scene")]
    public static void BuildOpenScene()
    {
        Debug.Log(BuildCurrentScene());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Wordle/Rebuild design in all 30 level scenes")]
    public static void BuildAllMenu()
    {
        Debug.Log(BuildRange(0, 30));
    }

    /// <summary>Rebuilds levels start..start+count-1 (0-29: Cebuano 1-10, Ilonggo 1-10, Tagalog 1-10), saving each scene.</summary>
    public static string BuildRange(int start, int count)
    {
        var sb = new StringBuilder();
        string[] langs = { "1 - Puzzle - Cebuano|Cebuano", "2 - Puzzle - Ilonggo|Ilonggo", "3 - Puzzle - Tagalog|Tagalog" };
        for (int i = start; i < start + count && i < 30; i++)
        {
            string[] parts = langs[i / 10].Split('|');
            int level = (i % 10) + 1;
            string path = "Assets/Main/Scenes/2 Wordle/Archive/Level scenes before categories/" + parts[0] + "/puzzle-wordle-generator " + level + " " + parts[1] + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string result = BuildCurrentScene();
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine(scene.name + ": " + result);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ prefab

    /// <summary>Creates the WordleTile prefab (border + fill + letter + WordleTileView) if it does not exist yet.</summary>
    public static GameObject EnsureTilePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TilePrefabPath);
        if (existing != null)
        {
            return existing;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Main/Prefabs/2 Wordle"))
        {
            AssetDatabase.CreateFolder("Assets/Main/Prefabs", "2 Wordle");
        }

        Material mat = Mat();
        var root = new GameObject("WordleTile", typeof(RectTransform));
        root.layer = 5;
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(80f, 80f);

        Image border = NewImage("Border", rootRect, new Color(1f, 1f, 1f, 0.9f));
        Stretch(border.rectTransform, 0f);
        Image fill = NewImage("Fill", rootRect, new Color(1f, 1f, 1f, 0.38f));
        Stretch(fill.rectTransform, 5f);

        var letterGo = new GameObject("Letter", typeof(RectTransform));
        letterGo.layer = 5;
        letterGo.transform.SetParent(fill.transform, false);
        var letter = letterGo.AddComponent<TextMeshProUGUI>();
        letter.text = string.Empty;
        letter.fontSize = 52f;
        letter.fontStyle = FontStyles.Bold;
        letter.alignment = TextAlignmentOptions.Center;
        letter.color = Ink;
        letter.raycastTarget = false;
        letter.enableWordWrapping = false;
        letter.overflowMode = TextOverflowModes.Overflow;
        letter.fontSharedMaterial = mat;
        Stretch(letter.rectTransform, 0f);

        var view = root.AddComponent<WordleTileView>();
        var so = new SerializedObject(view);
        so.FindProperty("border").objectReferenceValue = border;
        so.FindProperty("fill").objectReferenceValue = fill;
        so.FindProperty("letter").objectReferenceValue = letter;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TilePrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = LibraryUiKit.Rounded;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2.2f;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

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

    // ------------------------------------------------------------------ small UI helpers

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
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
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
            Debug.LogWarning("WordleDesignBuilder: missing field " + name);
            return;
        }
        p.objectReferenceValue = value;
    }

    private static void SetArray<T>(SerializedObject so, string name, IList<T> items) where T : Object
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("WordleDesignBuilder: missing array " + name);
            return;
        }
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    private static void DestroyNamed(Transform parent, string name)
    {
        Transform t;
        while ((t = parent.Find(name)) != null)
        {
            t.SetParent(null, false);
            Object.DestroyImmediate(t.gameObject);
        }
    }

    // ------------------------------------------------------------------ the scene build

    /// <summary>Rebuilds the design in the currently open Wordle puzzle scene. Returns a one-line summary.</summary>
    public static string BuildCurrentScene()
    {
        var canvasGo = GameObject.Find("Canvas");
        var genGo = GameObject.Find("WordleGenerator");
        if (canvasGo == null || genGo == null)
        {
            return "SKIPPED (no Canvas or WordleGenerator)";
        }
        Transform canvas = canvasGo.transform;
        var generator = genGo.GetComponent<WordleRowsColumnGenerator>();
        var typer = genGo.GetComponent<WordleKeyboardTyper>();
        var verifier = genGo.GetComponent<WordleWordVerifier>();
        var timer = Object.FindObjectOfType<WordleTimer>();
        GameObject tilePrefab = EnsureTilePrefab();
        Material mat = Mat();
        string sceneName = canvasGo.scene.name;
        string language = sceneName.Contains("Cebuano") ? "Cebuano" : (sceneName.Contains("Tagalog") ? "Tagalog" : "Ilonggo");

        // ---- clean up the old design and any earlier run of this builder
        Transform gridParent = generator.gridParent;
        if (gridParent != null && gridParent.parent != canvas)
        {
            gridParent.SetParent(canvas, false);
        }
        DestroyNamed(canvas, "WordleUI");
        DestroyNamed(canvas, "WordleOverlays");
        DestroyNamed(canvas, "WordleController");
        DestroyNamed(canvas, "WordleTheme");
        DestroyNamed(canvas, "Dialog Panel");

        var textStyle = genGo.GetComponent<WordleTextStyleApplier>();
        if (textStyle != null)
        {
            Object.DestroyImmediate(textStyle);
        }

        // ---- canvas scaler (same as the menus)
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // ---- sky base (the old jigsaw background object becomes the solid sky colour)
        Transform bgT = canvas.Find("background image");
        if (bgT == null) bgT = canvas.Find("sky_base");
        var bg = bgT.GetComponent<Image>();
        bg.gameObject.name = "sky_base";
        bg.sprite = null;
        bg.color = new Color32(95, 180, 236, 255);
        bg.raycastTarget = false;
        bg.rectTransform.SetSiblingIndex(0);

        RectTransform ui = LibraryUiKit.NewRect("WordleUI", canvas);
        LibraryUiKit.Fill(ui);
        ui.SetSiblingIndex(1);

        // ---- sky layers
        Image skyTop = LibraryUiKit.Icon(ui, "sky_top", Spr("main_menu_sky_gradient.png"), new Color32(191, 230, 255, 255));
        LibraryUiKit.Fill(skyTop.rectTransform);

        CanvasGroup nightG = NewGroup(ui, "night_group");
        var rnd = new System.Random(11);
        for (int i = 0; i < 40; i++)
        {
            float sx = rnd.Next(20, 1900), sy = rnd.Next(170, 900), sz = rnd.Next(4, 10);
            if (sx > 560 && sx < 1360) sx = sx < 960 ? sx - 520 : sx + 520;
            Image star = LibraryUiKit.Icon(nightG.transform, "star", LibraryUiKit.Circle, new Color(1f, 1f, 0.92f, 0.8f));
            LibraryUiKit.Place(star.rectTransform, Mathf.Clamp(sx, 20, 1890), sy, sz, sz);
        }
        Image moonHalo = LibraryUiKit.Icon(nightG.transform, "moon_halo", LibraryUiKit.Circle, new Color(1f, 0.92f, 0.65f, 0.07f));
        LibraryUiKit.Place(moonHalo.rectTransform, 1640, 200, 152, 152);
        Image moon = LibraryUiKit.Icon(nightG.transform, "moon", LibraryUiKit.Circle, new Color32(255, 243, 196, 255));
        LibraryUiKit.Place(moon.rectTransform, 1660, 220, 112, 112);

        CanvasGroup sunG = NewGroup(ui, "sun_group");
        Image sunHalo = LibraryUiKit.Icon(sunG.transform, "sun_halo", LibraryUiKit.Circle, new Color(1f, 0.96f, 0.78f, 0.43f));
        LibraryUiKit.Place(sunHalo.rectTransform, 720, -200, 480, 480);
        Image sunCore = LibraryUiKit.Icon(sunG.transform, "sun_core", LibraryUiKit.Circle, new Color(1f, 0.93f, 0.67f, 0.9f));
        LibraryUiKit.Place(sunCore.rectTransform, 820, -100, 280, 280);

        CanvasGroup cloudG = NewGroup(ui, "clouds_group");
        float[][] cloudDefs =
        {
            new[] { 90f, 330f, 1.1f }, new[] { 1560f, 380f, 1.0f }, new[] { 150f, 700f, 0.8f }, new[] { 1640f, 740f, 0.9f }
        };
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

        // ---- board group: clue card, grid, keyboard
        CanvasGroup board = NewGroup(ui, "Board");
        board.interactable = true;
        board.blocksRaycasts = true;

        Image clueCard = LibraryUiKit.Box(board.transform, "clue_card", Cream, true);
        LibraryUiKit.Place(clueCard.rectTransform, 380, 164, 1160, 124);
        TextMeshProUGUI clueLabel = Lab(clueCard.transform, string.Empty, 32, InkSoft, false, TextAlignmentOptions.TopLeft, 28, 10, 900, 40);
        TextMeshProUGUI clueText = Lab(clueCard.transform, verifier != null ? verifier.Definition : string.Empty, 40, Ink, false,
            TextAlignmentOptions.MidlineLeft, 28, 50, 900, 66);
        clueText.enableWordWrapping = true;
        clueText.enableAutoSizing = true;
        clueText.fontSizeMin = 24f;
        clueText.fontSizeMax = 40f;
        TextMeshProUGUI hintLabel;
        Image hintPill = Pill(clueCard.transform, "hint_button", "Hint", 968, 34, 168, 56, Yellow, Ink, 34, true, out hintLabel);
        Button hintButton = MakeButton(hintPill);

        // grid parent moves into the board so the pause window can hide it
        if (gridParent != null)
        {
            gridParent.SetParent(board.transform, false);
            var gpr = (RectTransform)gridParent;
            gpr.anchorMin = gpr.anchorMax = new Vector2(0.5f, 0.5f);
            gpr.pivot = new Vector2(0.5f, 0.5f);
            gpr.anchoredPosition = new Vector2(0f, 4f);
            gpr.sizeDelta = Vector2.zero;
            gpr.localScale = Vector3.one;
            gridParent.SetSiblingIndex(1);
        }
        generator.cellPrefab = tilePrefab;
        generator.cellSize = new Vector2(80f, 80f);
        generator.spacing = 10f;
        generator.generateOnStart = true;
        EditorUtility.SetDirty(generator);
        generator.Generate();

        // verifier colours: green / yellow / calm gray
        if (verifier != null)
        {
            verifier.correctColor = Green;
            verifier.wrongPositionColor = Yellow;
            verifier.notInWordColor = Gray;
            EditorUtility.SetDirty(verifier);
        }

        // keyboard
        RectTransform keyboard = LibraryUiKit.NewRect("Keyboard", board.transform);
        LibraryUiKit.Fill(keyboard);
        var keys = new List<WordleKeyButton>();
        string[] rows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        float[] rowY = { 780f, 868f, 956f };
        for (int r = 0; r < 3; r++)
        {
            string letters = rows[r];
            bool last = r == 2;
            float width = letters.Length * 68f + (letters.Length - 1) * 8f + (last ? 2 * (112f + 8f) : 0f);
            float x = (1920f - width) / 2f;
            if (last)
            {
                keys.Add(MakeKey(keyboard, "key_enter", WordleKeyKind.Enter, '\n', "Enter", x, rowY[r], 112f, 76f, Green, Color.white, 30f));
                x += 112f + 8f;
            }
            foreach (char c in letters)
            {
                keys.Add(MakeKey(keyboard, "key_" + c, WordleKeyKind.Letter, c, c.ToString(), x, rowY[r], 68f, 76f, Cream, Ink, 36f));
                x += 68f + 8f;
            }
            if (last)
            {
                WordleKeyButton back = MakeKey(keyboard, "key_backspace", WordleKeyKind.Backspace, '\b', "", x, rowY[r], 112f, 76f, Cream, Ink, 30f);
                Image bar = LibraryUiKit.Box(back.transform, "bar", Ink, true);
                LibraryUiKit.Place(bar.rectTransform, 44, 34, 40, 8);
                Image head = LibraryUiKit.Icon(back.transform, "arrow_head", Spr("main_menu_play_triangle.png"), Ink);
                LibraryUiKit.Place(head.rectTransform, 26, 24, 26, 28);
                head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                head.rectTransform.anchoredPosition = new Vector2(26f + 13f, -(24f + 14f));
                head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                keys.Add(back);
            }
        }

        // ---- top bar
        TextMeshProUGUI langLabel;
        Pill(ui, "language_chip", language, 230, 60, 224, 64, Cream, Ink, 36, false, out langLabel);

        Image timerPill = LibraryUiKit.Box(ui, "timer_pill", Cream, true);
        LibraryUiKit.Place(timerPill.rectTransform, 760, 48, 400, 88);
        Image clock = LibraryUiKit.Icon(timerPill.transform, "clock", LibraryUiKit.Circle, new Color32(143, 208, 90, 255));
        LibraryUiKit.Place(clock.rectTransform, 22, 16, 56, 56);
        Image handA = LibraryUiKit.Box(clock.transform, "hand_a", new Color32(39, 80, 10, 255), false);
        LibraryUiKit.Place(handA.rectTransform, 26, 10, 5, 20);
        Image handB = LibraryUiKit.Box(clock.transform, "hand_b", new Color32(39, 80, 10, 255), false);
        LibraryUiKit.Place(handB.rectTransform, 26, 25, 16, 5);
        TextMeshProUGUI timerLabel = Lab(timerPill.transform, "05:00", 56, Ink, true, TextAlignmentOptions.Center, 100, 0, 280, 88);

        Image triesPill = LibraryUiKit.Box(ui, "tries_pill", Cream, true);
        LibraryUiKit.Place(triesPill.rectTransform, 1520, 48, 360, 88);
        Lab(triesPill.transform, "Tries", 36, InkSoft, false, TextAlignmentOptions.MidlineLeft, 28, 0, 110, 88);
        int rowCount = Mathf.Max(1, generator.rows);
        var dots = new List<Image>();
        for (int i = 0; i < rowCount; i++)
        {
            Image dot = LibraryUiKit.Icon(triesPill.transform, "dot_" + (i + 1), LibraryUiKit.Circle, Green);
            LibraryUiKit.Place(dot.rectTransform, 140 + i * 44, 28, 32, 32);
            dots.Add(dot);
        }

        Image pauseRing = LibraryUiKit.Icon(ui, "pause_button", LibraryUiKit.Circle, Orange);
        LibraryUiKit.Place(pauseRing.rectTransform, 48, 40, 112, 112);
        Image pauseInner = LibraryUiKit.Icon(pauseRing.transform, "inner", LibraryUiKit.Circle, Cream);
        LibraryUiKit.Place(pauseInner.rectTransform, 6, 6, 100, 100);
        Image bar1 = LibraryUiKit.Box(pauseRing.transform, "bar_1", Orange, true);
        LibraryUiKit.Place(bar1.rectTransform, 38, 30, 14, 52);
        Image bar2 = LibraryUiKit.Box(pauseRing.transform, "bar_2", Orange, true);
        LibraryUiKit.Place(bar2.rectTransform, 60, 30, 14, 52);
        Button pauseButton = MakeButton(pauseRing);

        // ---- toast
        Image toastBox = LibraryUiKit.Box(ui, "toast", new Color(0.23f, 0.16f, 0.1f, 0.94f), true);
        LibraryUiKit.Place(toastBox.rectTransform, 560, 300, 800, 72);
        TextMeshProUGUI toastLabel = Lab(toastBox.transform, "", 34, Cream, false, TextAlignmentOptions.Center, 0, 0, 800, 72);
        LibraryUiKit.Fill(toastLabel.rectTransform);
        var toast = toastBox.gameObject.AddComponent<HubToast>();
        var toastSo = new SerializedObject(toast);
        SetRef(toastSo, "label", toastLabel);
        toastSo.ApplyModifiedPropertiesWithoutUndo();
        toastBox.gameObject.SetActive(false);

        // back button stays above the board
        // the old yellow back arrow is gone: the pause button takes its place (Back to games is in the pause window)
        DestroyNamed(canvas, "back_to_main_menu_button");

        // ---- timer: keep the WordleTimer object, replace its old art with the new label
        if (timer != null)
        {
            var timerGo = timer.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(timerGo))
            {
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(timerGo), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            for (int i = timer.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = timer.transform.GetChild(i);
                child.SetParent(null, false);
                Object.DestroyImmediate(child.gameObject);
            }
            var timerSo = new SerializedObject(timer);
            SetRef(timerSo, "timerText", timerLabel);
            timerSo.FindProperty("applyTextStyle").boolValue = false;
            timerSo.FindProperty("applyPosition").boolValue = false;
            timerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- sky theme (time of day)
        RectTransform themeRect = LibraryUiKit.NewRect("WordleTheme", canvas);
        LibraryUiKit.Fill(themeRect);
        var theme = themeRect.gameObject.AddComponent<MainMenuTheme>();
        var themeSo = new SerializedObject(theme);
        SetRef(themeSo, "skyBase", bg);
        SetRef(themeSo, "skyTopOverlay", skyTop);
        SetRef(themeSo, "clouds", cloudG);
        SetRef(themeSo, "sun", sunG);
        SetRef(themeSo, "night", nightG);
        themeSo.ApplyModifiedPropertiesWithoutUndo();
        theme.LoadDefaultThemes();
        theme.PreviewPeriod(DayPeriod.Noon);

        // ---- overlays: result card and pause window (above everything)
        RectTransform overlays = LibraryUiKit.NewRect("WordleOverlays", canvas);
        LibraryUiKit.Fill(overlays);

        // result
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
        Image resultCardImg = LibraryUiKit.Box(resultDim.transform, "card", Cream, true);
        LibraryUiKit.Place(resultCardImg.rectTransform, 480, 124, 960, 880);
        resultCardImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        resultCardImg.rectTransform.anchoredPosition = new Vector2(480 + 480, -(124 + 440));
        TextMeshProUGUI resultTitle;
        Image ribbon = Pill(resultCardImg.transform, "ribbon", "Husto!", 200, -44, 560, 108, Navy, Color.white, 56, true, out resultTitle);
        TextMeshProUGUI resultSubtitle = Lab(resultCardImg.transform, "You got it", 40, InkSoft, false, TextAlignmentOptions.Center, 0, 72, 960, 60);
        RectTransform tilesRoot = LibraryUiKit.NewRect("tiles", resultCardImg.transform);
        LibraryUiKit.Place(tilesRoot, 60, 150, 840, 140);
        var hlg = tilesRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 16f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        GameObject tileTemplateGo = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, tilesRoot);
        tileTemplateGo.name = "TileTemplate";
        var tileTemplate = tileTemplateGo.GetComponent<WordleTileView>();
        tileTemplateGo.SetActive(false);
        TextMeshProUGUI resultDefinition = Lab(resultCardImg.transform, "", 40, Ink, false, TextAlignmentOptions.Center, 60, 310, 840, 100);
        resultDefinition.enableWordWrapping = true;
        resultDefinition.enableAutoSizing = true;
        resultDefinition.fontSizeMin = 26f;
        resultDefinition.fontSizeMax = 40f;
        TextMeshProUGUI resultMeaning = Lab(resultCardImg.transform, "", 34, InkSoft, false, TextAlignmentOptions.Center, 60, 416, 840, 50);

        RectTransform stats = LibraryUiKit.NewRect("stats", resultCardImg.transform);
        LibraryUiKit.Place(stats, 0, 480, 960, 124);
        TextMeshProUGUI statTime, statTries, statStreak;
        MakeStat(stats, "stat_time", "Time", 44, new Color32(239, 231, 212, 255), Ink, Ink, out statTime);
        MakeStat(stats, "stat_tries", "Tries", 350, new Color32(239, 231, 212, 255), Ink, Ink, out statTries);
        MakeStat(stats, "stat_streak", "Streak", 656, new Color32(251, 226, 196, 255), new Color32(122, 58, 10, 255), new Color32(122, 58, 10, 255), out statStreak);

        TextMeshProUGUI nextLabel;
        Image nextImg = Pill(resultCardImg.transform, "next_button", "Next word", 100, 616, 760, 116, Green, Color.white, 52, true, out nextLabel);
        Button nextButton = MakeButton(nextImg);
        TextMeshProUGUI libLabel;
        Image libImg = Pill(resultCardImg.transform, "library_button", "See in Library", 100, 750, 368, 92, Purple, Color.white, 36, true, out libLabel);
        Button libButton = MakeButton(libImg);
        Image backOuter = LibraryUiKit.Box(resultCardImg.transform, "back_button", Ink, true);
        LibraryUiKit.Place(backOuter.rectTransform, 492, 750, 368, 92);
        Image backInner = LibraryUiKit.Box(backOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(backInner.rectTransform, 4, 4, 360, 84);
        TextMeshProUGUI backLabel = Lab(backInner.transform, "Back to games", 36, Ink, false, TextAlignmentOptions.Center, 0, 0, 360, 84);
        LibraryUiKit.Fill(backLabel.rectTransform);
        Button resultBackButton = MakeButton(backOuter);

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
        Image timePill = LibraryUiKit.Box(pauseCard.transform, "time_pill", new Color32(239, 231, 212, 255), true);
        LibraryUiKit.Place(timePill.rectTransform, 110, 336, 540, 112);
        TextMeshProUGUI pauseTime = Lab(timePill.transform, "05:00", 60, Ink, true, TextAlignmentOptions.Center, 0, 0, 540, 112);
        LibraryUiKit.Fill(pauseTime.rectTransform);
        TextMeshProUGUI resumeLabel;
        Image resumeImg = Pill(pauseCard.transform, "resume_button", "Resume", 80, 480, 600, 112, Green, Color.white, 52, true, out resumeLabel);
        Button resumeButton = MakeButton(resumeImg);
        TextMeshProUGUI newWordLabel;
        Image newWordImg = Pill(pauseCard.transform, "new_word_button", "New word", 80, 616, 600, 92, Purple, Color.white, 40, true, out newWordLabel);
        Button newWordButton = MakeButton(newWordImg);
        Image pauseBackOuter = LibraryUiKit.Box(pauseCard.transform, "back_button", Ink, true);
        LibraryUiKit.Place(pauseBackOuter.rectTransform, 80, 728, 600, 80);
        Image pauseBackInner = LibraryUiKit.Box(pauseBackOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(pauseBackInner.rectTransform, 4, 4, 592, 72);
        TextMeshProUGUI pauseBackLabel = Lab(pauseBackInner.transform, "Back to games", 38, Ink, false, TextAlignmentOptions.Center, 0, 0, 592, 72);
        LibraryUiKit.Fill(pauseBackLabel.rectTransform);
        Button pauseBackButton = MakeButton(pauseBackOuter);

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("WordleController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<WordleGameController>();
        var cso = new SerializedObject(controller);
        SetRef(cso, "typer", typer);
        SetRef(cso, "verifier", verifier);
        SetRef(cso, "generator", generator);
        SetRef(cso, "timer", timer);
        string dictFile = language == "Cebuano" ? "cebuano.json" : (language == "Tagalog" ? "tagalog.json" : "hiligaynon.json");
        SetRef(cso, "dictionaryJson", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Main/Data/Language Dictionary/" + dictFile));
        SetRef(cso, "languageLabel", langLabel);
        SetRef(cso, "timerLabel", timerLabel);
        SetArray(cso, "triesDots", dots);
        SetRef(cso, "pauseButton", pauseButton);
        SetRef(cso, "board", board);
        SetRef(cso, "clueLabel", clueLabel);
        SetRef(cso, "clueText", clueText);
        SetRef(cso, "hintButton", hintButton);
        SetRef(cso, "hintButtonLabel", hintLabel);
        SetArray(cso, "keys", keys);
        SetRef(cso, "toast", toast);
        SetRef(cso, "pausePanel", pauseDim.gameObject);
        SetRef(cso, "pauseTimeLabel", pauseTime);
        SetRef(cso, "resumeButton", resumeButton);
        SetRef(cso, "pauseNewWordButton", newWordButton);
        SetRef(cso, "pauseBackButton", pauseBackButton);
        SetRef(cso, "resultPanel", resultDim.gameObject);
        SetRef(cso, "resultCard", resultCardImg.rectTransform);
        SetRef(cso, "resultTitle", resultTitle);
        SetRef(cso, "resultSubtitle", resultSubtitle);
        SetRef(cso, "resultTiles", tilesRoot);
        SetRef(cso, "resultTileTemplate", tileTemplate);
        SetRef(cso, "resultDefinition", resultDefinition);
        SetRef(cso, "resultMeaning", resultMeaning);
        SetRef(cso, "statsRoot", stats.gameObject);
        SetRef(cso, "statTime", statTime);
        SetRef(cso, "statTries", statTries);
        SetRef(cso, "statStreak", statStreak);
        SetRef(cso, "resultNextLabel", nextLabel);
        SetRef(cso, "resultNextButton", nextButton);
        SetRef(cso, "resultLibraryButton", libButton);
        SetRef(cso, "resultBackButton", resultBackButton);
        SetRef(cso, "confettiRoot", confetti);
        cso.ApplyModifiedPropertiesWithoutUndo();

        // panels start hidden in the saved scene
        resultDim.gameObject.SetActive(false);
        pauseDim.gameObject.SetActive(false);

        // final order: overlays on top, controller last
        overlays.SetAsLastSibling();
        controllerRect.SetAsLastSibling();

        // preview texts in the editor
        langLabel.text = language;
        EditorUtility.SetDirty(canvasGo);
        return "built (" + language + ", " + generator.columns + " letters, " + keys.Count + " keys, " + dots.Count + " tries)";
    }

    private static WordleKeyButton MakeKey(Transform parent, string name, WordleKeyKind kind, char letter, string text,
        float x, float y, float w, float h, Color bg, Color fg, float fontSize)
    {
        Image img = LibraryUiKit.Box(parent, name, bg, true);
        LibraryUiKit.Place(img.rectTransform, x, y, w, h);
        TextMeshProUGUI label = Lab(img.transform, text, fontSize, fg, true, TextAlignmentOptions.Center, 0, 0, w, h);
        LibraryUiKit.Fill(label.rectTransform);
        MakeButton(img);
        var key = img.gameObject.AddComponent<WordleKeyButton>();
        key.kind = kind;
        key.letter = letter;
        key.background = img;
        key.label = label;
        return key;
    }

    private static void MakeStat(Transform parent, string name, string caption, float x, Color bg, Color captionColor, Color valueColor,
        out TextMeshProUGUI value)
    {
        Image pill = LibraryUiKit.Box(parent, name, bg, true);
        LibraryUiKit.Place(pill.rectTransform, x, 0, 260, 124);
        Lab(pill.transform, caption, 32, new Color32(107, 82, 55, 255), false, TextAlignmentOptions.Center, 0, 12, 260, 40);
        value = Lab(pill.transform, "-", 48, valueColor, true, TextAlignmentOptions.Center, 0, 56, 260, 60);
    }
}
