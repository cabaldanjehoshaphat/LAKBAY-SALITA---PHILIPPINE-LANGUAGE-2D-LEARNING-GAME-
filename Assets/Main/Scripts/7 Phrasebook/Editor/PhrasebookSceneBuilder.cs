using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static LearnSceneBuilder;

/// <summary>
/// PhrasebookSceneBuilder
/// Editor tool that sets up the Phrasebook and the Sentence builder as real saved scenes, in the look of the other menus. Menu: Tools > Phrasebook >
/// "Build Phrasebook scenes and menu card". It
///  - points LevelDataRegistry (Phrasebook Files) at Assets/Main/Data/Phrasebook/phrases_*.json,
///  - builds "Phrasebook - Topics" (seven topic cards with Learn and Build buttons, PhraseTopicsController),
///  - builds "Phrasebook - Cards" (the phrase cards with word-by-word meanings, PhraseCardsController),
///  - builds "Phrasebook - Builder" (the Sentence builder: prompt, answer slots, word tiles, Clear / Hint / Check and the result card,
///    PhraseBuilderController),
///  - adds a third card, "Phrasebook", to the main menu (1 Main_menu) under Play and Library, and
///  - adds the three scenes to Build Settings.
/// The scenes are saved in Assets/Main/Scenes/7 Phrasebook. Safe to run again: the scenes are rebuilt and the menu card is replaced.
/// </summary>
public static class PhrasebookSceneBuilder
{
    private const string SceneFolder = "Assets/Main/Scenes/7 Phrasebook";
    private const string TopicsPath = SceneFolder + "/Phrasebook - Topics.unity";
    private const string CardsPath = SceneFolder + "/Phrasebook - Cards.unity";
    private const string BuilderPath = SceneFolder + "/Phrasebook - Builder.unity";
    private const string MenuPath = "Assets/Main/Scenes/1 Menu/1 Main_menu.unity";
    private const string RegistryPath = "Assets/Main/Resources/LevelDataRegistry.asset";
    private const string DataFolder = "Assets/Main/Data/Phrasebook/";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Amber = new Color32(245, 166, 35, 255);
    private static readonly Color Gold = new Color32(232, 185, 35, 255);
    private static readonly Color Sand = new Color32(239, 231, 212, 255);
    private static readonly Color Accent = new Color32(29, 158, 117, 255);
    private static readonly Color Navy = new Color32(30, 58, 138, 255);
    private static readonly Color EmptySlot = new Color32(233, 242, 252, 255);

    [MenuItem("Tools/Phrasebook/Build Phrasebook scenes and menu card")]
    public static void BuildAllMenu()
    {
        Debug.Log(BuildAll());
    }

    /// <summary>Builds the three scenes, the main menu card and the registry entry. Returns a short summary.</summary>
    public static string BuildAll()
    {
        EditorSceneManager.SaveOpenScenes();
        if (!AssetDatabase.IsValidFolder(SceneFolder))
        {
            AssetDatabase.CreateFolder("Assets/Main/Scenes", "7 Phrasebook");
        }
        string registry = AssignRegistry();
        PhraseData.Reload();
        string topics = BuildTopicsScene();
        string cards = BuildCardsScene();
        string builder = BuildBuilderScene();
        string hub = InstallMenuCard();
        AddToBuildSettings(TopicsPath);
        AddToBuildSettings(CardsPath);
        AddToBuildSettings(BuilderPath);
        AssetDatabase.SaveAssets();
        return "Registry: " + registry + " | Topics: " + topics + " | Cards: " + cards + " | Builder: " + builder + " | Menu: " + hub;
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

    private static string AssignRegistry()
    {
        var registry = AssetDatabase.LoadAssetAtPath<LevelDataRegistry>(RegistryPath);
        if (registry == null)
        {
            return "MISSING " + RegistryPath;
        }
        string[] names = { "cebuano", "ilonggo", "tagalog" };
        registry.phrasebookFiles = new TextAsset[3];
        for (int i = 0; i < 3; i++)
        {
            registry.phrasebookFiles[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + "phrases_" + names[i] + ".json");
        }
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        return "3 phrase files assigned";
    }

    // ------------------------------------------------------------------ shared pieces

    private static Button AddBackButton(RectTransform ui)
    {
        Image ring = LibraryUiKit.Icon(ui, "back_button", LibraryUiKit.Circle, Cream);
        LibraryUiKit.Place(ring.rectTransform, 48, 40, 112, 112);
        Image arrow = LibraryUiKit.Icon(ring.transform, "arrow", Spr("main_menu_play_triangle.png"), Ink);
        LibraryUiKit.Place(arrow.rectTransform, 36, 30, 40, 52);
        arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.anchoredPosition = new Vector2(36f + 20f, -(30f + 26f));
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        return MakeButton(ring);
    }

    /// <summary>A small speech-bubble drawing (white rounded box and a tail) inside a square at (x, y) of size s.</summary>
    private static void AddBubbleIcon(Transform parent, float x, float y, float s)
    {
        Image bubble = LibraryUiKit.Box(parent, "bubble", Color.white, true);
        LibraryUiKit.Place(bubble.rectTransform, x + s * 0.14f, y + s * 0.2f, s * 0.72f, s * 0.46f);
        Image tail = LibraryUiKit.Box(parent, "bubble_tail", Color.white, false);
        LibraryUiKit.Place(tail.rectTransform, x + s * 0.3f, y + s * 0.56f, s * 0.2f, s * 0.2f);
        tail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tail.rectTransform.anchoredPosition = new Vector2(x + s * 0.4f, -(y + s * 0.66f));
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private static void StretchTop(RectTransform r, float top, float height)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, -top);
        r.sizeDelta = new Vector2(0f, height);
    }

    private static TextMeshProUGUI AutoLabel(Transform parent, string text, float max, float min, Color color, bool bold,
        float x, float y, float w, float h)
    {
        TextMeshProUGUI t = Lab(parent, text, max, color, bold, TextAlignmentOptions.Center, x, y, w, h);
        t.enableAutoSizing = true;
        t.fontSizeMax = max;
        t.fontSizeMin = min;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // ------------------------------------------------------------------ topics scene

    private static string BuildTopicsScene()
    {
        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("PhrasebookUI", canvas);
        LibraryUiKit.Fill(ui);
        BuildSky(canvas, ui, bg, "PhrasebookTheme");

        Button back = AddBackButton(ui);
        Image titleChip = LibraryUiKit.Box(ui, "title_chip", Cream, true);
        LibraryUiKit.Place(titleChip.rectTransform, 190, 52, 370, 88);
        Image icon = LibraryUiKit.Box(titleChip.transform, "icon", Accent, true);
        LibraryUiKit.Place(icon.rectTransform, 16, 16, 56, 56);
        AddBubbleIcon(icon.transform, 0, 0, 56);
        Lab(titleChip.transform, "Phrasebook", 44, Ink, true, TextAlignmentOptions.MidlineLeft, 90, 0, 270, 88);
        // language selection, like the Library tabs: the picked language is dark
        string[] languageNames = { "Cebuano", "Ilonggo", "Tagalog" };
        var languageButtons = new List<Button>();
        var languageImages = new List<Image>();
        var languageLabels = new List<TMP_Text>();
        for (int i = 0; i < 3; i++)
        {
            TextMeshProUGUI chipLabel;
            Image chip = Pill(ui, "language_" + languageNames[i].ToLowerInvariant(), languageNames[i], 590 + i * 212, 58, 200, 72, i == 0 ? Ink : Cream, i == 0 ? Cream : Ink, 36, true, out chipLabel);
            languageImages.Add(chip);
            languageLabels.Add(chipLabel);
            languageButtons.Add(MakeButton(chip));
        }
        TextMeshProUGUI randomLabel;
        Image randomImg = Pill(ui, "random_button", "Random phrase", 1520, 52, 360, 88, Accent, Color.white, 40, true, out randomLabel);
        Button randomButton = MakeButton(randomImg);

        string[] fallback = { "Greetings", "Thanks and sorry", "About me", "Food and market", "Directions", "Numbers and time", "Help and safety" };
        var names = new List<TMP_Text>();
        var learnedLabels = new List<TMP_Text>();
        var learnedPills = new List<Image>();
        var examples = new List<TMP_Text>();
        var learnButtons = new List<Button>();
        var buildButtons = new List<Button>();
        for (int i = 0; i < 7; i++)
        {
            float x = 60 + (i % 4) * 460;
            float y = 216 + (i / 4) * 420;
            PhraseTopicDef def = PhraseData.Topic(0, i);
            Image card = LibraryUiKit.Box(ui, "topic_" + (i + 1), Cream, true);
            LibraryUiKit.Place(card.rectTransform, x, y, 420, 380);
            Image dot = LibraryUiKit.Icon(card.transform, "dot", LibraryUiKit.Circle, WordleData.CategoryColor(i));
            LibraryUiKit.Place(dot.rectTransform, 28, 24, 52, 52);
            TextMeshProUGUI nameLabel = Lab(card.transform, def != null ? def.name : fallback[i], 40, Ink, true, TextAlignmentOptions.MidlineLeft, 92, 16, 300, 68);
            nameLabel.enableWordWrapping = true;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 26f;
            nameLabel.fontSizeMax = 40f;
            nameLabel.overflowMode = TextOverflowModes.Overflow;
            names.Add(nameLabel);

            TextMeshProUGUI learnedLabel;
            Image pill = Pill(card.transform, "learned_tag", "0 of 1 learned", 28, 100, 230, 44, Sand, Ink, 24, true, out learnedLabel);
            learnedLabels.Add(learnedLabel);
            learnedPills.Add(pill);

            Image exampleBox = LibraryUiKit.Box(card.transform, "example_box", Sand, true);
            LibraryUiKit.Place(exampleBox.rectTransform, 28, 162, 364, 80);
            Lab(exampleBox.transform, "e.g.", 22, InkSoft, false, TextAlignmentOptions.Center, 0, 4, 364, 28);
            examples.Add(AutoLabel(exampleBox.transform, "Good morning", 30, 20, Ink, true, 0, 34, 364, 40));

            TextMeshProUGUI learnLabel, buildLabel;
            Image learnImg = Pill(card.transform, "learn_button", "Learn", 28, 262, 172, 92, Amber, Ink, 42, true, out learnLabel);
            Image buildImg = Pill(card.transform, "build_button", "Build", 216, 262, 176, 92, Green, Color.white, 42, true, out buildLabel);
            learnButtons.Add(MakeButton(learnImg));
            buildButtons.Add(MakeButton(buildImg));
        }

        Image info = LibraryUiKit.Box(ui, "info_card", Navy, true);
        LibraryUiKit.Place(info.rectTransform, 60 + 3 * 460, 216 + 420, 420, 380);
        Lab(info.transform, "Say it for real", 44, Gold, true, TextAlignmentOptions.TopLeft, 28, 36, 364, 70);
        TextMeshProUGUI infoText = Lab(info.transform, "Learn shows each phrase with what every word means. Build puts the words in order to make the sentence yourself.", 28,
            Color.white, false, TextAlignmentOptions.TopLeft, 28, 118, 364, 230);
        infoText.enableWordWrapping = true;
        infoText.overflowMode = TextOverflowModes.Overflow;

        RectTransform controllerRect = LibraryUiKit.NewRect("PhraseTopicsController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<PhraseTopicsController>();
        var so = new SerializedObject(controller);
        SetRef(so, "backButton", back);
        SetRef(so, "randomButton", randomButton);
        SetArray(so, "languageButtons", languageButtons);
        SetArray(so, "languageImages", languageImages);
        SetArray(so, "languageLabels", languageLabels);
        SetArray(so, "topicNames", names);
        SetArray(so, "learnedLabels", learnedLabels);
        SetArray(so, "learnedPills", learnedPills);
        SetArray(so, "exampleLabels", examples);
        SetArray(so, "learnButtons", learnButtons);
        SetArray(so, "buildButtons", buildButtons);
        so.ApplyModifiedPropertiesWithoutUndo();
        controllerRect.SetAsLastSibling();

        PhraseSession.Language = 0;
        controller.Refresh();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), TopicsPath);
        return "saved " + TopicsPath;
    }

    // ------------------------------------------------------------------ cards scene

    private static string BuildCardsScene()
    {
        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("PhrasebookCardsUI", canvas);
        LibraryUiKit.Fill(ui);
        BuildSky(canvas, ui, bg, "PhrasebookTheme");

        Button back = AddBackButton(ui);
        TextMeshProUGUI titleLabel, langLabel, topicLabel;
        Pill(ui, "title_chip", "Phrasebook", 190, 52, 330, 88, Cream, Ink, 44, true, out titleLabel);
        Pill(ui, "language_chip", "Cebuano", 540, 60, 224, 64, Cream, Ink, 36, false, out langLabel);
        Pill(ui, "topic_chip", "Greetings", 784, 52, 330, 88, Cream, Ink, 38, true, out topicLabel);
        topicLabel.enableAutoSizing = true;
        topicLabel.fontSizeMin = 24f;
        topicLabel.fontSizeMax = 38f;
        topicLabel.overflowMode = TextOverflowModes.Overflow;

        Image card = LibraryUiKit.Box(ui, "phrase_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 330, 190, 1260, 660);
        TextMeshProUGUI counter = Lab(card.transform, "Phrase 1 of 5", 34, InkSoft, true, TextAlignmentOptions.MidlineLeft, 50, 34, 520, 50);
        TextMeshProUGUI english = AutoLabel(card.transform, "Good morning", 64, 36, InkSoft, false, 60, 100, 1140, 90);
        TextMeshProUGUI phrase = AutoLabel(card.transform, "Maayong buntag", 120, 56, Ink, true, 30, 200, 1200, 170);

        RectTransform glossRow = LibraryUiKit.NewRect("gloss_row", card.transform);
        LibraryUiKit.Place(glossRow, 60, 420, 1140, 140);
        var hlg = glossRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        Image tpl = LibraryUiKit.Box(glossRow, "gloss_template", Sand, true);
        var layout = tpl.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 240f;
        layout.preferredHeight = 120f;
        TextMeshProUGUI w = Lab(tpl.transform, "Maayong", 36, Ink, true, TextAlignmentOptions.Center, 0, 0, 10, 10);
        w.gameObject.name = "Word";
        StretchTop(w.rectTransform, 8f, 52f);
        TextMeshProUGUI m = Lab(tpl.transform, "good", 30, Green, false, TextAlignmentOptions.Center, 0, 0, 10, 10);
        m.gameObject.name = "Meaning";
        StretchTop(m.rectTransform, 64f, 46f);
        tpl.gameObject.SetActive(false);

        TextMeshProUGUI prevLabel, nextLabel, buildLabel;
        Image prevImg = Pill(ui, "previous_button", "Back", 330, 880, 300, 96, Cream, Ink, 42, true, out prevLabel);
        Image nextImg = Pill(ui, "next_button", "Next", 660, 880, 300, 96, Amber, Ink, 42, true, out nextLabel);
        Image buildImg = Pill(ui, "build_button", "Build this topic", 1060, 880, 530, 96, Green, Color.white, 42, true, out buildLabel);
        Button prev = MakeButton(prevImg), next = MakeButton(nextImg), build = MakeButton(buildImg);

        RectTransform controllerRect = LibraryUiKit.NewRect("PhraseCardsController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<PhraseCardsController>();
        var so = new SerializedObject(controller);
        SetRef(so, "backButton", back);
        SetRef(so, "languageLabel", langLabel);
        SetRef(so, "topicLabel", topicLabel);
        SetRef(so, "counterLabel", counter);
        SetRef(so, "englishLabel", english);
        SetRef(so, "phraseLabel", phrase);
        SetRef(so, "glossRow", glossRow);
        SetRef(so, "glossTemplate", tpl.gameObject);
        SetRef(so, "previousButton", prev);
        SetRef(so, "nextButton", next);
        SetRef(so, "buildButton", build);
        so.ApplyModifiedPropertiesWithoutUndo();
        controllerRect.SetAsLastSibling();

        PhraseSession.Language = 0;
        PhraseSession.Topic = 0;
        PhraseSession.Phrase = 0;
        controller.Refresh();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), CardsPath);
        return "saved " + CardsPath;
    }

    // ------------------------------------------------------------------ Sentence builder scene

    private static RectTransform MakeRow(Transform parent, string name, float x, float y, float w, float h)
    {
        RectTransform row = LibraryUiKit.NewRect(name, parent);
        LibraryUiKit.Place(row, x, y, w, h);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        return row;
    }

    /// <summary>A hidden template for the slots and tiles: rounded box, layout element, button and a centred label.</summary>
    private static GameObject MakeTemplate(Transform parent, string name, Color color)
    {
        Image img = LibraryUiKit.Box(parent, name, color, true);
        var layout = img.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 240f;
        layout.preferredHeight = 92f;
        MakeButton(img);
        img.raycastTarget = true;
        TextMeshProUGUI label = Lab(img.transform, "word", 44, Ink, true, TextAlignmentOptions.Center, 0, 0, 10, 10);
        LibraryUiKit.Fill(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(10f, 0f);
        label.rectTransform.offsetMax = new Vector2(-10f, 0f);
        label.enableAutoSizing = true;
        label.fontSizeMax = 44f;
        label.fontSizeMin = 22f;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        img.gameObject.SetActive(false);
        return img.gameObject;
    }

    private static string BuildBuilderScene()
    {
        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("SentenceBuilderUI", canvas);
        LibraryUiKit.Fill(ui);
        BuildSky(canvas, ui, bg, "PhrasebookTheme");

        // ---- top bar
        Button back = AddBackButton(ui);
        TextMeshProUGUI langLabel;
        Pill(ui, "language_chip", "Cebuano", 190, 60, 224, 64, Cream, Ink, 36, false, out langLabel);
        Image topicChip = LibraryUiKit.Box(ui, "topic_chip", Cream, true);
        LibraryUiKit.Place(topicChip.rectTransform, 434, 48, 440, 88);
        TextMeshProUGUI topicLabel = AutoLabel(topicChip.transform, "Greetings", 34, 22, Ink, true, 0, 6, 440, 44);
        TextMeshProUGUI progressLabel = Lab(topicChip.transform, "Sentence 1 of 5", 26, InkSoft, false, TextAlignmentOptions.Center, 0, 50, 440, 34);
        Image triesChip = LibraryUiKit.Box(ui, "tries_chip", Cream, true);
        LibraryUiKit.Place(triesChip.rectTransform, 1500, 48, 380, 88);
        Lab(triesChip.transform, "Tries", 36, InkSoft, false, TextAlignmentOptions.MidlineLeft, 30, 0, 150, 88);
        var dots = new List<Image>();
        for (int i = 0; i < 4; i++)
        {
            Image dot = LibraryUiKit.Icon(triesChip.transform, "try_" + (i + 1), LibraryUiKit.Circle, Green);
            LibraryUiKit.Place(dot.rectTransform, 180 + i * 46, 28, 32, 32);
            dots.Add(dot);
        }

        // ---- prompt
        Image prompt = LibraryUiKit.Box(ui, "prompt_card", Cream, true);
        LibraryUiKit.Place(prompt.rectTransform, 330, 190, 1260, 210);
        TextMeshProUGUI promptLabel = Lab(prompt.transform, "Say this in Cebuano", 30, InkSoft, false, TextAlignmentOptions.Center, 0, 22, 1260, 44);
        TextMeshProUGUI english = AutoLabel(prompt.transform, "Good morning", 80, 40, Ink, true, 40, 76, 1180, 120);

        // ---- sentence
        TextMeshProUGUI answerCaption = Lab(ui, "Your sentence", 28, InkSoft, true, TextAlignmentOptions.Center, 330, 420, 1260, 40);
        RectTransform answerRow = MakeRow(ui, "answer_row", 160, 470, 1600, 100);
        GameObject slotTemplate = MakeTemplate(answerRow, "slot_template", EmptySlot);
        TextMeshProUGUI bankCaption = Lab(ui, "Tap the words", 28, InkSoft, true, TextAlignmentOptions.Center, 330, 630, 1260, 40);
        RectTransform bankRow = MakeRow(ui, "bank_row", 160, 680, 1600, 100);
        GameObject tileTemplate = MakeTemplate(bankRow, "tile_template", Cream);
        TextMeshProUGUI message = Lab(ui, "", 30, InkSoft, false, TextAlignmentOptions.Center, 330, 788, 1260, 50);
        message.fontStyle = FontStyles.Italic;

        TextMeshProUGUI clearLabel, hintLabel, checkLabel;
        Image clearImg = Pill(ui, "clear_button", "Clear", 420, 860, 300, 96, Cream, Ink, 42, true, out clearLabel);
        Image hintImg = Pill(ui, "hint_button", "Hint · 2", 760, 860, 400, 96, Gold, Ink, 42, true, out hintLabel);
        Image checkImg = Pill(ui, "check_button", "Check", 1200, 860, 300, 96, Green, Color.white, 42, true, out checkLabel);
        Button clear = MakeButton(clearImg), hint = MakeButton(hintImg), check = MakeButton(checkImg);

        // ---- result card (hidden until the sentence is finished)
        Image dim = LibraryUiKit.Box(ui, "result_panel", new Color(0.08f, 0.13f, 0.24f, 0.45f), false);
        LibraryUiKit.Fill(dim.rectTransform);
        dim.raycastTarget = true;
        Image card = LibraryUiKit.Box(dim.transform, "result_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 480, 120, 960, 800);
        TextMeshProUGUI titleLabel;
        Pill(card.transform, "ribbon", "Husto!", 200, -48, 560, 108, Navy, Color.white, 64, true, out titleLabel);
        TextMeshProUGUI subtitle = Lab(card.transform, "You built the sentence", 36, InkSoft, false, TextAlignmentOptions.Center, 0, 90, 960, 50);
        TextMeshProUGUI sentence = AutoLabel(card.transform, "Maayong buntag", 64, 30, Green, true, 40, 150, 880, 130);
        TextMeshProUGUI resultEnglish = AutoLabel(card.transform, "Good morning", 40, 24, InkSoft, true, 40, 290, 880, 60);
        string[] statNames = { "Time", "Tries", "Streak" };
        string[] statStart = { "00:00", "1 of 4", "1 day" };
        var statValues = new List<TextMeshProUGUI>();
        for (int i = 0; i < 3; i++)
        {
            Image box = LibraryUiKit.Box(card.transform, "stat_" + statNames[i].ToLowerInvariant(), i == 2 ? new Color32(250, 224, 196, 255) : Sand, true);
            LibraryUiKit.Place(box.rectTransform, 50 + i * 290, 380, 270, 130);
            Lab(box.transform, statNames[i], 28, InkSoft, false, TextAlignmentOptions.Center, 0, 10, 270, 40);
            statValues.Add(Lab(box.transform, statStart[i], 48, Ink, true, TextAlignmentOptions.Center, 0, 54, 270, 66));
        }
        TextMeshProUGUI nextLabel, phrasebookLabel, backLabel;
        Image nextImg = Pill(card.transform, "next_button", "Next sentence", 80, 560, 800, 100, Green, Color.white, 44, true, out nextLabel);
        Image phrasebookImg = Pill(card.transform, "phrasebook_button", "See in Phrasebook", 80, 680, 430, 80, Accent, Color.white, 32, true, out phrasebookLabel);
        Image backOuter = LibraryUiKit.Box(card.transform, "topics_button", Ink, true);
        LibraryUiKit.Place(backOuter.rectTransform, 530, 680, 350, 80);
        Image backInner = LibraryUiKit.Box(backOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(backInner.rectTransform, 4, 4, 342, 72);
        backLabel = Lab(backInner.transform, "Back to topics", 30, Ink, false, TextAlignmentOptions.Center, 0, 0, 342, 72);
        Button nextButton = MakeButton(nextImg), phrasebookButton = MakeButton(phrasebookImg), topicsButton = MakeButton(backOuter);
        dim.gameObject.SetActive(false);

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("PhraseBuilderController", canvas);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<PhraseBuilderController>();
        var so = new SerializedObject(controller);
        SetRef(so, "backButton", back);
        SetRef(so, "languageLabel", langLabel);
        SetRef(so, "topicLabel", topicLabel);
        SetRef(so, "progressLabel", progressLabel);
        SetArray(so, "triesDots", dots);
        SetRef(so, "promptLabel", promptLabel);
        SetRef(so, "englishLabel", english);
        SetRef(so, "answerRow", answerRow);
        SetRef(so, "bankRow", bankRow);
        SetRef(so, "slotTemplate", slotTemplate);
        SetRef(so, "tileTemplate", tileTemplate);
        SetRef(so, "clearButton", clear);
        SetRef(so, "hintButton", hint);
        SetRef(so, "hintLabel", hintLabel);
        SetRef(so, "checkButton", check);
        SetRef(so, "messageLabel", message);
        SetRef(so, "resultPanel", dim.gameObject);
        SetRef(so, "resultTitle", titleLabel);
        SetRef(so, "resultSubtitle", subtitle);
        SetRef(so, "resultSentence", sentence);
        SetRef(so, "resultEnglish", resultEnglish);
        SetRef(so, "statTime", statValues[0]);
        SetRef(so, "statTries", statValues[1]);
        SetRef(so, "statStreak", statValues[2]);
        SetRef(so, "resultNextLabel", nextLabel);
        SetRef(so, "resultNextButton", nextButton);
        SetRef(so, "resultPhrasebookButton", phrasebookButton);
        SetRef(so, "resultBackButton", topicsButton);
        so.ApplyModifiedPropertiesWithoutUndo();
        controllerRect.SetAsLastSibling();

        PhraseSession.Language = 0;
        PhraseSession.Topic = 2;
        PhraseSession.Phrase = 0;
        controller.LoadPhrase();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), BuilderPath);
        return "saved " + BuilderPath;
    }

    // ------------------------------------------------------------------ main menu card

    private static string InstallMenuCard()
    {
        var scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        GameObject play = GameObject.Find("play_card");
        GameObject library = GameObject.Find("library_card");
        if (play == null || library == null)
        {
            return "SKIPPED (no play_card or library_card on the main menu)";
        }
        GameObject old = GameObject.Find("phrasebook_card");
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        GameObject card = Object.Instantiate(library, library.transform.parent);
        card.name = "phrasebook_card";
        card.transform.SetSiblingIndex(library.transform.GetSiblingIndex() + 1);

        // three cards of 140 px: Play, Library, Phrasebook
        float[] ys = { 352f, 508f, 664f };
        GameObject[] cards = { play, library, card };
        for (int i = 0; i < cards.Length; i++)
        {
            LibraryUiKit.Place((RectTransform)cards[i].transform, 580, ys[i], 760, 140);
            LibraryUiKit.Place((RectTransform)cards[i].transform.Find("icon"), 32, 18, 104, 104);
        }

        // the copy must not open the Library
        var button = card.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        card.AddComponent<PhraseMenuButton>();

        // icon: teal square with a speech bubble
        Transform iconT = card.transform.Find("icon");
        iconT.GetComponent<Image>().color = Accent;
        for (int i = iconT.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(iconT.GetChild(i).gameObject);
        }
        AddBubbleIcon(iconT, 0, 0, 104);

        var texts = new List<TMP_Text>();
        foreach (Transform child in card.transform)
        {
            var t = child.GetComponent<TMP_Text>();
            if (t != null) texts.Add(t);
        }
        if (texts.Count >= 2)
        {
            texts[0].text = "Phrasebook";
            texts[1].text = "Say real sentences";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Phrasebook card added";
    }
}
