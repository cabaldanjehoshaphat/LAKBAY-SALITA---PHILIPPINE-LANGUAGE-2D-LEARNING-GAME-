using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TapToHearInstaller
/// Editor tool that puts the "Tap to hear" buttons (WordAudio) into the scenes. Menu: Tools > Audio > "Add Tap to hear buttons".
///  - draws the speaker icon sprite (Assets/Main/Sprites/6 Learn/speaker_icon.png) if it is missing,
///  - Learn - Cards: a blue "Tap to hear" button at the top right of the word card (LearnController),
///  - Phrasebook - Cards: the same button for the whole phrase, and every word tile under the phrase becomes tappable,
///  - Phrasebook - Builder: a speaker button next to the sentence on the result card,
///  - Wordle - Play: a speaker button beside the meaning on the result card,
///  - WordSearch - Play and Crossword - Play: a speaker beside the star of every row of the "words you learned" list.
/// Safe to run again: existing speaker buttons are replaced.
/// </summary>
public static class TapToHearInstaller
{
    private const string SpritePath = "Assets/Main/Sprites/6 Learn/speaker_icon.png";
    private static readonly Color Blue = new Color32(58, 142, 219, 255);

    [MenuItem("Tools/Audio/Hide Tap to hear buttons in the scenes")]
    public static void HideMenu()
    {
        Debug.Log(SetButtonsActive(false));
    }

    [MenuItem("Tools/Audio/Show Tap to hear buttons in the scenes")]
    public static void ShowMenu()
    {
        Debug.Log(SetButtonsActive(true));
    }

    /// <summary>
    /// Shows or hides the saved Tap to hear buttons in every scene that has them, so the scenes look the same in the editor as in the game.
    /// At Play the buttons follow WordAudio.Enabled.
    /// </summary>
    public static string SetButtonsActive(bool active)
    {
        EditorSceneManager.SaveOpenScenes();
        string[] paths =
        {
            "Assets/Main/Scenes/6 Learn/Learn - Cards.unity",
            "Assets/Main/Scenes/7 Phrasebook/Phrasebook - Cards.unity",
            "Assets/Main/Scenes/7 Phrasebook/Phrasebook - Builder.unity",
            "Assets/Main/Scenes/2 Wordle/Wordle - Play.unity",
            "Assets/Main/Scenes/4 Word Search/WordSearch - Play.unity",
            "Assets/Main/Scenes/3 Crossword/Crossword - Play.unity"
        };
        int changed = 0;
        foreach (string path in paths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.gameObject.scene == scene && (t.name == "speak_button" || t.name == "Speaker") && t.gameObject.activeSelf != active)
                {
                    t.gameObject.SetActive(active);
                    changed++;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        return changed + " Tap to hear buttons " + (active ? "shown" : "hidden") + " in the scenes";
    }

    [MenuItem("Tools/Audio/Add Tap to hear buttons")]
    public static void InstallMenu()
    {
        Debug.Log(InstallAll());
    }

    public static string InstallAll()
    {
        EditorSceneManager.SaveOpenScenes();
        Sprite speaker = EnsureSprite();
        string a = Learn(speaker);
        string b = PhraseCards(speaker);
        string c = PhraseBuilder(speaker);
        string d = WordleResult(speaker);
        string e = ResultRows(speaker, "Assets/Main/Scenes/4 Word Search/WordSearch - Play.unity");
        string f = ResultRows(speaker, "Assets/Main/Scenes/3 Crossword/Crossword - Play.unity");
        AssetDatabase.SaveAssets();
        return "Learn: " + a + " | Phrase cards: " + b + " | Builder: " + c + " | Wordle result: " + d + " | Word Search rows: " + e + " | Crossword rows: " + f;
    }

    // ------------------------------------------------------------------ sprite

    /// <summary>A white speaker (body, cone and two sound waves) on a transparent 128 x 128 texture, saved as a sprite.</summary>
    private static Sprite EnsureSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null)
        {
            return existing;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Main/Sprites/6 Learn"))
        {
            AssetDatabase.CreateFolder("Assets/Main/Sprites", "6 Learn");
        }
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)(n - 1), v = y / (float)(n - 1);
                float a = Mathf.Max(Cover(u, v), 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SpritePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    /// <summary>Coverage (0..1) of the speaker shape at a point of the unit square (u right, v up).</summary>
    private static float Cover(float u, float v)
    {
        float edge = 0.012f;
        // body: a small box on the left
        float body = Box(u, v, 0.14f, 0.36f, 0.30f, 0.64f, edge);
        // cone: a trapezoid from the box to the right
        float cone = 0f;
        if (u >= 0.28f && u <= 0.52f)
        {
            float t = (u - 0.28f) / 0.24f;
            float half = Mathf.Lerp(0.14f, 0.32f, t);
            cone = Smooth((half - Mathf.Abs(v - 0.5f)) / edge);
        }
        // two waves: arcs of rings around the cone mouth
        float wave = Ring(u, v, 0.52f, 0.5f, 0.17f, 0.045f, edge) + Ring(u, v, 0.52f, 0.5f, 0.31f, 0.045f, edge);
        return Mathf.Clamp01(Mathf.Max(body, Mathf.Max(cone, wave)));
    }

    private static float Smooth(float t)
    {
        return Mathf.Clamp01(t + 0.5f);
    }

    private static float Box(float u, float v, float u0, float v0, float u1, float v1, float edge)
    {
        float du = Mathf.Min(u - u0, u1 - u), dv = Mathf.Min(v - v0, v1 - v);
        return Smooth(Mathf.Min(du, dv) / edge);
    }

    private static float Ring(float u, float v, float cx, float cy, float radius, float width, float edge)
    {
        float dx = u - cx, dy = v - cy;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        if (dx <= 0f || Mathf.Abs(angle) > 48f)
        {
            return 0f;
        }
        return Smooth((width / 2f - Mathf.Abs(d - radius)) / edge);
    }

    // ------------------------------------------------------------------ pieces

    /// <summary>A blue rounded pill with the speaker icon and "Tap to hear".</summary>
    private static Button AddPill(Transform parent, string name, Sprite icon, float x, float y, float w, float h, out Image pill)
    {
        Old(parent, name);
        TextMeshProUGUI label;
        pill = LearnSceneBuilder.Pill(parent, name, "Tap to hear", x, y, w, h, Blue, Color.white, h * 0.42f, true, out label);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        LibraryUiKit.Place(label.rectTransform, h * 1.05f, 0, w - h * 1.2f, h);
        Image iconImage = LibraryUiKit.Icon(pill.transform, "icon", icon, Color.white);
        LibraryUiKit.Place(iconImage.rectTransform, h * 0.22f, h * 0.18f, h * 0.64f, h * 0.64f);
        Button b = LearnSceneBuilder.MakeButton(pill);
        pill.raycastTarget = true;
        return b;
    }

    /// <summary>A blue round button with only the speaker icon.</summary>
    private static Button AddRound(Transform parent, string name, Sprite icon, float x, float y, float d)
    {
        Old(parent, name);
        Image circle = LibraryUiKit.Icon(parent, name, LibraryUiKit.Circle, Blue);
        LibraryUiKit.Place(circle.rectTransform, x, y, d, d);
        Image iconImage = LibraryUiKit.Icon(circle.transform, "icon", icon, Color.white);
        LibraryUiKit.Place(iconImage.rectTransform, d * 0.2f, d * 0.2f, d * 0.6f, d * 0.6f);
        Button b = LearnSceneBuilder.MakeButton(circle);
        circle.raycastTarget = true;
        return b;
    }

    private static void Old(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }
    }

    // ------------------------------------------------------------------ scenes

    private static string Learn(Sprite icon)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Main/Scenes/6 Learn/Learn - Cards.unity", OpenSceneMode.Single);
        var controller = Object.FindObjectOfType<LearnController>();
        Transform card = GameObject.Find("word_card").transform;
        Image pill;
        Button b = AddPill(card, "speak_button", icon, 950, 24, 270, 66, out pill);
        var so = new SerializedObject(controller);
        LearnSceneBuilder.SetRef(so, "speakButton", b);
        LearnSceneBuilder.SetRef(so, "speakImage", pill);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "added";
    }

    private static string PhraseCards(Sprite icon)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Main/Scenes/7 Phrasebook/Phrasebook - Cards.unity", OpenSceneMode.Single);
        var controller = Object.FindObjectOfType<PhraseCardsController>();
        Transform card = GameObject.Find("phrase_card").transform;
        Image pill;
        Button b = AddPill(card, "speak_button", icon, 950, 24, 270, 66, out pill);
        var so = new SerializedObject(controller);
        LearnSceneBuilder.SetRef(so, "speakButton", b);
        LearnSceneBuilder.SetRef(so, "speakImage", pill);
        so.ApplyModifiedPropertiesWithoutUndo();

        // the word tiles become buttons
        Transform template = card.Find("gloss_row/gloss_template");
        if (template != null)
        {
            var image = template.GetComponent<Image>();
            image.raycastTarget = true;
            if (template.GetComponent<Button>() == null)
            {
                LearnSceneBuilder.MakeButton(image);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "added";
    }

    private static string PhraseBuilder(Sprite icon)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Main/Scenes/7 Phrasebook/Phrasebook - Builder.unity", OpenSceneMode.Single);
        var controller = Object.FindObjectOfType<PhraseBuilderController>();
        Transform card = null;
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == "result_card" && t.gameObject.scene == scene)
            {
                card = t;
            }
        }
        if (card == null)
        {
            return "SKIPPED (no result card)";
        }
        // beside the English line, left of centre
        Button b = AddRound(card, "speak_button", icon, 70, 282, 70);
        var so = new SerializedObject(controller);
        LearnSceneBuilder.SetRef(so, "resultSpeakButton", b);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "added";
    }

    private static string WordleResult(Sprite icon)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Main/Scenes/2 Wordle/Wordle - Play.unity", OpenSceneMode.Single);
        var controller = Object.FindObjectOfType<WordleGameController>();
        var so = new SerializedObject(controller);
        var meaning = so.FindProperty("resultMeaning").objectReferenceValue as TMP_Text;
        if (meaning == null)
        {
            return "SKIPPED (no meaning label)";
        }
        Transform card = meaning.transform.parent;
        // make room: the meaning is centred between the speaker (left) and the star (right)
        LibraryUiKit.Place(meaning.rectTransform, 170, 408, 620, 62);
        Button b = AddRound(card, "speak_button", icon, 796, 410, 58);
        LearnSceneBuilder.SetRef(so, "resultSpeakButton", b);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "added";
    }

    private static string ResultRows(Sprite icon, string path)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Transform template = null;
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == "RowTemplate" && t.gameObject.scene == scene)
            {
                template = t;
            }
        }
        if (template == null)
        {
            return "SKIPPED (no RowTemplate)";
        }
        bool had = template.Find("Speaker") != null;
        Old(template, "Speaker");
        var meaning = template.Find("Meaning") as RectTransform;
        if (meaning != null && !had)
        {
            meaning.sizeDelta = new Vector2(meaning.sizeDelta.x - 70f, meaning.sizeDelta.y);
        }
        Button b = AddRound(template, "Speaker", icon, 868, 12, 48);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "added";
    }
}
