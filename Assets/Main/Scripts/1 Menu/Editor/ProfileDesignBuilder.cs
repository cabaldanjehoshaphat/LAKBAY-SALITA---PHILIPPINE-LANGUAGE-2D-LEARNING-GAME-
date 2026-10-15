using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ProfileDesignBuilder
/// Editor tool that rebuilds the "2 Profile Icon" scene in the redesigned look, as real saved scene objects (so the design is
/// visible in the editor before pressing Play): the time-of-day sky shared with the other screens, a profile card (avatar, name,
/// "Learning since", day streak, Edit profile, Reset progress), one compact row for each of Wordle, Crossword, Word Search and the Phrasebook
/// with the number solved (or learned) and a progress bar for each language (Cebuano, Ilonggo, Tagalog) side by side, a Words learned card, a
/// Favorites card, and the Edit profile and Reset progress dialogs, all driven by ProfileScreenController.
/// It keeps the scene's PlayerDatabase, SceneNavigator and the yellow back arrow (still wired to the main menu) and removes the
/// old circuit-board layout. Menu: Tools > Profile > Rebuild design in open scene. Safe to run again.
/// The previous designs are archived in Assets/Main/Scenes/1 Menu/Archive ("old design" = the circuit-board one, "2nd design" = the one
/// before the per-language game cards, "3rd design" = the one with three game cards before the Phrasebook). ArchiveAndRebuild() copies the open design to the archive first and then rebuilds it.
/// </summary>
public static class ProfileDesignBuilder
{
    private const string MaterialPath = "Assets/Main/Sprites/1 Menu/Main Menu/LiberationSans SDF Colorable.mat";
    private const string SpriteFolder = "Assets/Main/Sprites/1 Menu/Main Menu/";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Purple = new Color32(138, 63, 199, 255);
    private static readonly Color Navy = new Color32(27, 58, 140, 255);
    private static readonly Color Sand = new Color32(239, 231, 212, 255);
    private static readonly Color Red = new Color32(192, 57, 43, 255);

    private static Material _mat;

    [MenuItem("Tools/Profile/Rebuild design in open scene")]
    public static void BuildOpenScene()
    {
        Debug.Log(BuildCurrentScene());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private const string ProfileScenePath = "Assets/Main/Scenes/1 Menu/2 Profile Icon.unity";
    private const string ArchiveCopyPath = "Assets/Main/Scenes/1 Menu/Archive/2 Profile Icon (3rd design 2026-10-15).unity";

    /// <summary>
    /// Copies the current Profile scene to the archive ("3rd design 2026-10-15", only if that copy does not exist yet), then opens the
    /// Profile scene, rebuilds it in the current design (four game rows) and saves it. Returns a short summary.
    /// </summary>
    public static string ArchiveAndRebuild()
    {
        string archived;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArchiveCopyPath) != null)
        {
            archived = "archive copy already exists";
        }
        else if (AssetDatabase.CopyAsset(ProfileScenePath, ArchiveCopyPath))
        {
            archived = "copied to " + ArchiveCopyPath;
        }
        else
        {
            return "FAILED to archive " + ProfileScenePath;
        }

        Scene scene = EditorSceneManager.OpenScene(ProfileScenePath, OpenSceneMode.Single);
        string built = BuildCurrentScene();
        EditorSceneManager.SaveScene(scene);
        return archived + " | " + built;
    }

    [MenuItem("Tools/Profile/Archive current design and rebuild")]
    public static void ArchiveAndRebuildMenu()
    {
        Debug.Log(ArchiveAndRebuild());
    }

    // ------------------------------------------------------------------ helpers

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

    /// <summary>A 256 px circle sprite (sharper than the 64 px one used for small dots), falling back to the small one.</summary>
    private static Sprite HdCircle()
    {
        Sprite s = Spr("main_menu_circle_hd.png");
        return s != null ? s : LibraryUiKit.Circle;
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
            Debug.LogWarning("ProfileDesignBuilder: missing field " + name);
            return;
        }
        p.objectReferenceValue = value;
    }

    private static void SetArray<T>(SerializedObject so, string name, IList<T> items) where T : Object
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("ProfileDesignBuilder: missing array " + name);
            return;
        }
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    private static void SetCentered(RectTransform r, float x, float y, float w, float h)
    {
        LibraryUiKit.Place(r, x, y, w, h);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x + w / 2f, -(y + h / 2f));
    }

    /// <summary>Three columns on the right of a game row, side by side: the language name, a progress bar (track and fill) and the "solved/total" count.</summary>
    private static void AddLanguageBars(Transform card, string[] languages, Color fillColor, int[] perLanguageTotal, List<TextMeshProUGUI> countLabels,
        List<RectTransform> barFills)
    {
        for (int i = 0; i < languages.Length; i++)
        {
            float x = 520 + i * 205;
            Lab(card, languages[i], 26, InkSoft, false, TextAlignmentOptions.MidlineLeft, x, 24, 190, 34);
            Image track = LibraryUiKit.Box(card, "bar_track_" + (i + 1), Sand, true);
            LibraryUiKit.Place(track.rectTransform, x, 70, 190, 16);
            Image fill = LibraryUiKit.Box(card, "bar_fill_" + (i + 1), fillColor, true);
            LibraryUiKit.Place(fill.rectTransform, x, 70, 0, 16);
            barFills.Add(fill.rectTransform);
            countLabels.Add(Lab(card, "0/" + perLanguageTotal[i], 30, Ink, true, TextAlignmentOptions.MidlineLeft, x, 92, 190, 40));
        }
    }

    /// <summary>One compact game row (1140 x 150): the card, the icon square on the left, the game name and the summary line under it.</summary>
    private static Image AddGameRow(Transform ui, string name, float y, string title, string summary, Color iconColor, List<TextMeshProUGUI> summaryLabels, out Image icon)
    {
        Image row = LibraryUiKit.Box(ui, name, Cream, true);
        LibraryUiKit.Place(row.rectTransform, 700, y, 1140, 150);
        icon = LibraryUiKit.Box(row.transform, "icon", iconColor, true);
        LibraryUiKit.Place(icon.rectTransform, 24, 20, 110, 110);
        Lab(row.transform, title, 46, Ink, true, TextAlignmentOptions.MidlineLeft, 160, 24, 340, 58);
        summaryLabels.Add(Lab(row.transform, summary, 28, InkSoft, false, TextAlignmentOptions.MidlineLeft, 160, 86, 340, 38));
        return row;
    }

    // ------------------------------------------------------------------ the scene build

    /// <summary>Rebuilds the Profile design in the currently open "2 Profile Icon" scene. Returns a one-line summary.</summary>
    public static string BuildCurrentScene()
    {
        var canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null || GameObject.Find("User Image") == null && canvasGo.transform.Find("ProfileUI") == null)
        {
            return "SKIPPED (not the Profile scene)";
        }
        Transform canvas = canvasGo.transform;
        Material mat = Mat();

        // ---- remove the old layout and any earlier run of this builder (the background colour and back arrow are kept)
        for (int i = canvas.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.GetChild(i);
            if (child.name == "background color - image" || child.name == "sky_base" || child.name == "back_to_main_menu_button")
            {
                continue;
            }
            child.SetParent(null, false);
            Object.DestroyImmediate(child.gameObject);
        }
        var oldDisplay = GameObject.Find("ProfileDisplay");
        if (oldDisplay != null)
        {
            Object.DestroyImmediate(oldDisplay);
        }

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        Transform bgT = canvas.Find("background color - image");
        if (bgT == null) bgT = canvas.Find("sky_base");
        var bg = bgT.GetComponent<Image>();
        bg.gameObject.name = "sky_base";
        bg.sprite = null;
        bg.color = new Color32(95, 180, 236, 255);
        bg.raycastTarget = false;
        bg.rectTransform.SetSiblingIndex(0);

        RectTransform ui = LibraryUiKit.NewRect("ProfileUI", canvas);
        LibraryUiKit.Fill(ui);
        ui.SetSiblingIndex(1);

        // ---- sky layers
        Image skyTop = LibraryUiKit.Icon(ui, "sky_top", Spr("main_menu_sky_gradient.png"), new Color32(191, 230, 255, 255));
        LibraryUiKit.Fill(skyTop.rectTransform);

        CanvasGroup nightG = NewGroup(ui, "night_group");
        var rnd = new System.Random(33);
        for (int i = 0; i < 40; i++)
        {
            float sx = rnd.Next(20, 1900), sy = rnd.Next(150, 1040), sz = rnd.Next(4, 10);
            Image star = LibraryUiKit.Icon(nightG.transform, "star", HdCircle(), new Color(1f, 1f, 0.92f, 0.8f));
            LibraryUiKit.Place(star.rectTransform, sx, sy, sz, sz);
        }
        Image moon = LibraryUiKit.Icon(nightG.transform, "moon", HdCircle(), new Color32(255, 243, 196, 255));
        LibraryUiKit.Place(moon.rectTransform, 1500, 40, 96, 96);

        CanvasGroup sunG = NewGroup(ui, "sun_group");
        Image sunHalo = LibraryUiKit.Icon(sunG.transform, "sun_halo", HdCircle(), new Color(1f, 0.96f, 0.78f, 0.43f));
        LibraryUiKit.Place(sunHalo.rectTransform, 720, -220, 480, 480);
        Image sunCore = LibraryUiKit.Icon(sunG.transform, "sun_core", HdCircle(), new Color(1f, 0.93f, 0.67f, 0.9f));
        LibraryUiKit.Place(sunCore.rectTransform, 820, -120, 280, 280);

        CanvasGroup cloudG = NewGroup(ui, "clouds_group");
        float[][] cloudDefs = { new[] { 760f, 40f, 0.9f }, new[] { 1180f, 70f, 0.7f } };
        float[][] puffs = { new[] { 0f, 10f, 60f, 24f }, new[] { 14f, 0f, 40f, 30f }, new[] { 36f, 6f, 50f, 26f }, new[] { -10f, 14f, 40f, 18f } };
        for (int i = 0; i < cloudDefs.Length; i++)
        {
            RectTransform cr = LibraryUiKit.NewRect("cloud_" + (i + 1), cloudG.transform);
            LibraryUiKit.Place(cr, cloudDefs[i][0], cloudDefs[i][1], 10, 10);
            float s = cloudDefs[i][2] * 2f;
            foreach (float[] pf in puffs)
            {
                Image pu = LibraryUiKit.Icon(cr, "puff", HdCircle(), new Color(1, 1, 1, 0.82f));
                LibraryUiKit.Place(pu.rectTransform, pf[0] * s, pf[1] * s, pf[2] * s, pf[3] * s);
            }
        }

        // ---- back arrow (kept, still opens the main menu) and title
        Transform backBtn = canvas.Find("back_to_main_menu_button");
        if (backBtn != null)
        {
            var br = (RectTransform)backBtn;
            br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(150f, 150f);
            br.anchoredPosition = new Vector2(110f, -100f);
            br.SetSiblingIndex(ui.GetSiblingIndex() + 1);
        }
        TextMeshProUGUI titleLabel;
        Pill(ui, "title_pill", "Profile", 216, 56, 300, 88, Cream, Ink, 48, true, out titleLabel);

        // ---- profile card
        Image card = LibraryUiKit.Box(ui, "profile_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 80, 192, 580, 840);

        Image avatar = LibraryUiKit.Icon(card.transform, "avatar", HdCircle(), new Color32(91, 141, 220, 255));
        LibraryUiKit.Place(avatar.rectTransform, 166, 52, 248, 248);
        var mask = avatar.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        Image head = LibraryUiKit.Icon(avatar.transform, "head", HdCircle(), new Color32(240, 245, 255, 255));
        LibraryUiKit.Place(head.rectTransform, 84, 60, 80, 80);
        Image body = LibraryUiKit.Icon(avatar.transform, "body", HdCircle(), new Color32(240, 245, 255, 255));
        LibraryUiKit.Place(body.rectTransform, 24, 156, 200, 200);

        Image badgeOuter = LibraryUiKit.Icon(card.transform, "edit_avatar_button", HdCircle(), Ink);
        LibraryUiKit.Place(badgeOuter.rectTransform, 356, 228, 72, 72);
        Image badgeInner = LibraryUiKit.Icon(badgeOuter.transform, "inner", HdCircle(), Cream);
        LibraryUiKit.Place(badgeInner.rectTransform, 4, 4, 64, 64);
        RectTransform pencil = LibraryUiKit.NewRect("pencil", badgeOuter.transform);
        SetCentered(pencil, 29, 15, 14, 42);
        pencil.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image pencilBody = LibraryUiKit.Box(pencil, "body", new Color32(232, 185, 35, 255), true);
        LibraryUiKit.Place(pencilBody.rectTransform, 0, 0, 14, 30);
        Image pencilTip = LibraryUiKit.Box(pencil, "tip", Ink, true);
        LibraryUiKit.Place(pencilTip.rectTransform, 3, 30, 8, 12);
        Button editAvatarButton = MakeButton(badgeOuter);

        TextMeshProUGUI nameLabel = Lab(card.transform, "Player", 60, Ink, true, TextAlignmentOptions.Center, 0, 328, 580, 70);
        TextMeshProUGUI joinedLabel = Lab(card.transform, "Learning since", 30, InkSoft, false, TextAlignmentOptions.Center, 0, 398, 580, 44);

        Image streakCard = LibraryUiKit.Box(card.transform, "streak_card", new Color32(251, 226, 196, 255), true);
        LibraryUiKit.Place(streakCard.rectTransform, 48, 458, 484, 140);
        Image streakCircle = LibraryUiKit.Icon(streakCard.transform, "circle", HdCircle(), new Color32(232, 117, 26, 255));
        LibraryUiKit.Place(streakCircle.rectTransform, 28, 26, 88, 88);
        TextMeshProUGUI streakNumber = Lab(streakCircle.transform, "0", 48, Color.white, true, TextAlignmentOptions.Center, 0, 0, 88, 88);
        LibraryUiKit.Fill(streakNumber.rectTransform);
        Lab(streakCard.transform, "Day streak", 40, new Color32(122, 58, 10, 255), true, TextAlignmentOptions.MidlineLeft, 136, 22, 330, 56);
        TextMeshProUGUI bestStreak = Lab(streakCard.transform, "Best: 0 days", 30, new Color32(122, 58, 10, 255), false, TextAlignmentOptions.MidlineLeft, 136, 78, 330, 44);

        TextMeshProUGUI editLabel;
        Image editImg = Pill(card.transform, "edit_profile_button", "Edit profile", 48, 626, 484, 88, Purple, Color.white, 38, true, out editLabel);
        Button editProfileButton = MakeButton(editImg);

        Image resetLink = LibraryUiKit.Box(card.transform, "reset_link", new Color(0f, 0f, 0f, 0f), false);
        LibraryUiKit.Place(resetLink.rectTransform, 80, 742, 420, 56);
        Lab(resetLink.transform, "Reset progress", 30, new Color32(163, 45, 45, 255), false, TextAlignmentOptions.Center, 0, 0, 420, 56);
        Button resetLinkButton = MakeButton(resetLink);

        // ---- game rows: one compact row per game with a total and a progress bar for each language, side by side
        Color coral = new Color32(217, 96, 59, 255);
        Color blue = new Color32(55, 138, 221, 255);
        Color teal = new Color32(29, 158, 117, 255);
        string[] langNames = { "Cebuano", "Ilonggo", "Tagalog" };
        var summaryLabels = new List<TextMeshProUGUI>();
        var countLabels = new List<TextMeshProUGUI>();
        var barFills = new List<RectTransform>();
        int[] phraseTotals = { PhraseData.TotalPhrases(0), PhraseData.TotalPhrases(1), PhraseData.TotalPhrases(2) };
        Image icon;

        // Wordle
        Image wordleRow = AddGameRow(ui, "wordle_card", 192, "Wordle", "0 of 126 solved", Green, summaryLabels, out icon);
        Lab(icon.transform, "W", 64, Color.white, true, TextAlignmentOptions.Center, 0, 0, 110, 110);
        AddLanguageBars(wordleRow.transform, langNames, Green, new[] { 42, 42, 42 }, countLabels, barFills);

        // Crossword
        Image crossRow = AddGameRow(ui, "crossword_card", 358, "Crossword", "0 of 63 solved", blue, summaryLabels, out icon);
        foreach (float[] sq in new[] { new[] { 21f, 21f }, new[] { 59f, 21f }, new[] { 21f, 59f }, new[] { 59f, 59f } })
        {
            Image sqImg = LibraryUiKit.Box(icon.transform, "square", Color.white, true);
            LibraryUiKit.Place(sqImg.rectTransform, sq[0], sq[1], 30, 30);
        }
        AddLanguageBars(crossRow.transform, langNames, blue, new[] { 21, 21, 21 }, countLabels, barFills);

        // Word Search
        Image searchRow = AddGameRow(ui, "wordsearch_card", 524, "Word Search", "0 of 63 solved", coral, summaryLabels, out icon);
        Image ring = LibraryUiKit.Icon(icon.transform, "ring", HdCircle(), Color.white);
        LibraryUiKit.Place(ring.rectTransform, 21, 18, 54, 54);
        Image hole = LibraryUiKit.Icon(icon.transform, "hole", HdCircle(), coral);
        LibraryUiKit.Place(hole.rectTransform, 32, 29, 32, 32);
        Image handle = LibraryUiKit.Box(icon.transform, "handle", Color.white, true);
        SetCentered(handle.rectTransform, 61, 73, 36, 11);
        handle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        AddLanguageBars(searchRow.transform, langNames, coral, new[] { 21, 21, 21 }, countLabels, barFills);

        // Phrasebook (phrases learned in the Sentence builder)
        Image phraseRow = AddGameRow(ui, "phrasebook_card", 690, "Phrasebook", "0 of " + (phraseTotals[0] + phraseTotals[1] + phraseTotals[2]) + " learned", teal, summaryLabels, out icon);
        Image bubble = LibraryUiKit.Box(icon.transform, "bubble", Color.white, true);
        LibraryUiKit.Place(bubble.rectTransform, 15, 24, 80, 50);
        Image tail = LibraryUiKit.Box(icon.transform, "bubble_tail", Color.white, false);
        SetCentered(tail.rectTransform, 30, 62, 20, 20);
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        AddLanguageBars(phraseRow.transform, langNames, teal, phraseTotals, countLabels, barFills);

        // ---- words learned and favorites
        Image learnedCard = LibraryUiKit.Box(ui, "words_learned_card", Cream, true);
        LibraryUiKit.Place(learnedCard.rectTransform, 700, 860, 552, 172);
        Lab(learnedCard.transform, "Words learned", 30, InkSoft, false, TextAlignmentOptions.MidlineLeft, 28, 20, 500, 40);
        TextMeshProUGUI wordsLearned = Lab(learnedCard.transform, "0", 76, Ink, true, TextAlignmentOptions.MidlineLeft, 28, 66, 500, 90);

        Image favCard = LibraryUiKit.Box(ui, "favorites_card", Purple, true);
        LibraryUiKit.Place(favCard.rectTransform, 1288, 860, 552, 172);
        Lab(favCard.transform, "Favorites", 30, new Color32(232, 213, 247, 255), false, TextAlignmentOptions.MidlineLeft, 28, 20, 400, 40);
        TextMeshProUGUI favValue = Lab(favCard.transform, "0 words", 76, Color.white, true, TextAlignmentOptions.MidlineLeft, 28, 66, 440, 90);
        Image chevron = LibraryUiKit.Icon(favCard.transform, "chevron", Spr("main_menu_play_triangle.png"), Color.white);
        LibraryUiKit.Place(chevron.rectTransform, 484, 64, 36, 44);
        Button favButton = MakeButton(favCard);

        // ---- sky theme (time of day)
        RectTransform themeRect = LibraryUiKit.NewRect("ProfileTheme", canvas);
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

        // ---- dialogs
        RectTransform overlays = LibraryUiKit.NewRect("ProfileOverlays", canvas);
        LibraryUiKit.Fill(overlays);

        // edit profile
        Image editDim = LibraryUiKit.Box(overlays, "edit_panel", new Color(0.07f, 0.16f, 0.36f, 0.5f), false);
        LibraryUiKit.Fill(editDim.rectTransform);
        editDim.raycastTarget = true;
        Image editCard = LibraryUiKit.Box(editDim.transform, "card", Cream, true);
        LibraryUiKit.Place(editCard.rectTransform, 600, 170, 720, 700);
        TextMeshProUGUI editTitle;
        Pill(editCard.transform, "ribbon", "Edit profile", 160, -44, 400, 100, Navy, Color.white, 48, true, out editTitle);
        Lab(editCard.transform, "Choose an avatar", 32, InkSoft, false, TextAlignmentOptions.MidlineLeft, 48, 86, 624, 44);
        var choices = new List<Image>();
        var rings = new List<GameObject>();
        for (int i = 0; i < 10; i++)
        {
            float cx = 72 + (i % 5) * 122;
            float cy = 142 + (i / 5) * 120;
            Image ringImg = LibraryUiKit.Icon(editCard.transform, "ring_" + (i + 1), HdCircle(), Ink);
            LibraryUiKit.Place(ringImg.rectTransform, cx - 8, cy - 8, 104, 104);
            Image choice = LibraryUiKit.Icon(editCard.transform, "avatar_" + (i + 1), HdCircle(), Color.gray);
            LibraryUiKit.Place(choice.rectTransform, cx, cy, 88, 88);
            MakeButton(choice);
            choices.Add(choice);
            rings.Add(ringImg.gameObject);
        }
        Lab(editCard.transform, "Your name", 32, InkSoft, false, TextAlignmentOptions.MidlineLeft, 48, 392, 624, 40);
        Image inputOuter = LibraryUiKit.Box(editCard.transform, "name_border", Ink, true);
        LibraryUiKit.Place(inputOuter.rectTransform, 48, 436, 624, 84);
        Image inputBg = LibraryUiKit.Box(inputOuter.transform, "name_input", Color.white, true);
        LibraryUiKit.Place(inputBg.rectTransform, 4, 4, 616, 76);
        inputBg.raycastTarget = true;
        RectTransform area = LibraryUiKit.NewRect("TextArea", inputBg.transform);
        LibraryUiKit.Fill(area);
        area.offsetMin = new Vector2(20, 6);
        area.offsetMax = new Vector2(-20, -6);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = Lab(area, "Your name", 40, new Color32(155, 132, 104, 255), false, TextAlignmentOptions.MidlineLeft, 0, 0, 10, 10);
        placeholder.fontStyle = FontStyles.Italic;
        LibraryUiKit.Fill(placeholder.rectTransform);
        TextMeshProUGUI inputText = Lab(area, "", 40, Ink, false, TextAlignmentOptions.MidlineLeft, 0, 0, 10, 10);
        inputText.overflowMode = TextOverflowModes.Overflow;
        LibraryUiKit.Fill(inputText.rectTransform);
        var nameInput = inputBg.gameObject.AddComponent<TMP_InputField>();
        nameInput.textViewport = area;
        nameInput.textComponent = inputText;
        nameInput.placeholder = placeholder;
        nameInput.targetGraphic = inputBg;
        nameInput.lineType = TMP_InputField.LineType.SingleLine;
        nameInput.characterLimit = 16;
        nameInput.customCaretColor = true;
        nameInput.caretColor = Ink;
        nameInput.caretWidth = 3;

        TextMeshProUGUI saveLabel;
        Image saveImg = Pill(editCard.transform, "save_button", "Save", 48, 570, 300, 84, Green, Color.white, 40, true, out saveLabel);
        Button saveButton = MakeButton(saveImg);
        Image cancelOuter = LibraryUiKit.Box(editCard.transform, "cancel_button", Ink, true);
        LibraryUiKit.Place(cancelOuter.rectTransform, 372, 570, 300, 84);
        Image cancelInner = LibraryUiKit.Box(cancelOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(cancelInner.rectTransform, 4, 4, 292, 76);
        Lab(cancelInner.transform, "Cancel", 38, Ink, false, TextAlignmentOptions.Center, 0, 0, 292, 76);
        Button cancelButton = MakeButton(cancelOuter);

        // reset progress
        Image resetDim = LibraryUiKit.Box(overlays, "reset_panel", new Color(0.07f, 0.16f, 0.36f, 0.5f), false);
        LibraryUiKit.Fill(resetDim.rectTransform);
        resetDim.raycastTarget = true;
        Image resetCard = LibraryUiKit.Box(resetDim.transform, "card", Cream, true);
        LibraryUiKit.Place(resetCard.rectTransform, 660, 300, 600, 460);
        Image warnCircle = LibraryUiKit.Icon(resetCard.transform, "icon", HdCircle(), new Color32(247, 193, 193, 255));
        LibraryUiKit.Place(warnCircle.rectTransform, 240, -60, 120, 120);
        Image warnBar = LibraryUiKit.Box(warnCircle.transform, "bar", new Color32(163, 45, 45, 255), true);
        LibraryUiKit.Place(warnBar.rectTransform, 55, 24, 10, 48);
        Image warnDot = LibraryUiKit.Icon(warnCircle.transform, "dot", HdCircle(), new Color32(163, 45, 45, 255));
        LibraryUiKit.Place(warnDot.rectTransform, 54, 82, 12, 12);
        Lab(resetCard.transform, "Reset all progress?", 44, Ink, true, TextAlignmentOptions.Center, 0, 100, 600, 60);
        TextMeshProUGUI resetBody = Lab(resetCard.transform, "This clears your name, avatar, solved counts, learned phrases and streak. It can't be undone. Library favorites stay.", 32, InkSoft, false,
            TextAlignmentOptions.Top, 48, 176, 504, 150);
        resetBody.enableWordWrapping = true;
        Image keepOuter = LibraryUiKit.Box(resetCard.transform, "keep_button", Ink, true);
        LibraryUiKit.Place(keepOuter.rectTransform, 40, 340, 250, 84);
        Image keepInner = LibraryUiKit.Box(keepOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(keepInner.rectTransform, 4, 4, 242, 76);
        Lab(keepInner.transform, "Keep it", 38, Ink, false, TextAlignmentOptions.Center, 0, 0, 242, 76);
        Button keepButton = MakeButton(keepOuter);
        TextMeshProUGUI resetLabel;
        Image resetImg = Pill(resetCard.transform, "reset_button", "Reset", 310, 340, 250, 84, Red, Color.white, 40, true, out resetLabel);
        Button resetButton = MakeButton(resetImg);

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("ProfileController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<ProfileScreenController>();
        var so = new SerializedObject(controller);
        SetRef(so, "avatarImage", avatar);
        SetRef(so, "nameLabel", nameLabel);
        SetRef(so, "joinedLabel", joinedLabel);
        SetRef(so, "streakNumber", streakNumber);
        SetRef(so, "bestStreakLabel", bestStreak);
        SetRef(so, "editAvatarButton", editAvatarButton);
        SetRef(so, "editProfileButton", editProfileButton);
        SetRef(so, "resetLinkButton", resetLinkButton);
        SetArray(so, "gameSummaryLabels", summaryLabels);
        SetArray(so, "languageCountLabels", countLabels);
        SetArray(so, "languageBarFills", barFills);
        SetRef(so, "wordsLearnedValue", wordsLearned);
        SetRef(so, "favoritesValue", favValue);
        SetRef(so, "favoritesButton", favButton);
        so.FindProperty("barFullWidth").floatValue = 190f;
        SetRef(so, "editPanel", editDim.gameObject);
        SetArray(so, "avatarChoices", choices);
        SerializedProperty ringProp = so.FindProperty("avatarChoiceRings");
        ringProp.arraySize = rings.Count;
        for (int i = 0; i < rings.Count; i++) ringProp.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
        SetRef(so, "nameInput", nameInput);
        SetRef(so, "saveButton", saveButton);
        SetRef(so, "cancelButton", cancelButton);
        SetRef(so, "resetPanel", resetDim.gameObject);
        SetRef(so, "keepButton", keepButton);
        SetRef(so, "resetButton", resetButton);
        so.ApplyModifiedPropertiesWithoutUndo();

        // preview colours of the avatar choices (so the saved scene shows the palette)
        Color[] palette =
        {
            new Color32(91, 141, 220, 255), new Color32(93, 160, 42, 255), new Color32(232, 117, 26, 255), new Color32(138, 63, 199, 255),
            new Color32(212, 83, 126, 255), new Color32(232, 185, 35, 255), new Color32(29, 158, 117, 255), new Color32(55, 138, 221, 255),
            new Color32(153, 60, 29, 255), new Color32(95, 94, 90, 255)
        };
        for (int i = 0; i < choices.Count; i++) choices[i].color = palette[i];
        for (int i = 0; i < rings.Count; i++) rings[i].SetActive(i == 0);

        editDim.gameObject.SetActive(false);
        resetDim.gameObject.SetActive(false);
        overlays.SetAsLastSibling();
        controllerRect.SetAsLastSibling();

        EditorUtility.SetDirty(canvasGo);
        return "built profile screen";
    }
}
