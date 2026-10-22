using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ResumeCardBuilder
/// Editor tool that adds the "Continue" card to the Main menu (scene 1 Main_menu). Menu: Tools > Menu > "Add Continue card". It
///  - copies the look of the Play card (same size, rounded card, icon square, title and line), with an orange icon (a ring with a play triangle,
///    Assets/Main/Sprites/1 Menu/Main Menu/main_menu_resume_icon.png), the title "Continue" and a sample line,
///  - puts it on top of the card list (y = 352, where the Phrasebook card was) and moves the Phrasebook, Play and Library cards down by 156
///    (card height 140 + gap 16), once,
///  - adds the ResumeCard component (shows the last played puzzle and opens it when pressed; turns hollow, an outline only, once that puzzle is
///    finished; hides itself and moves the cards back up while there is nothing to continue) and assigns its references.
/// Safe to run again: the card is rebuilt and the other cards are only moved the first time.
/// </summary>
public static class ResumeCardBuilder
{
    private const string MenuPath = "Assets/Main/Scenes/1 Menu/1 Main_menu.unity";
    private const string IconPath = "Assets/Main/Sprites/1 Menu/Main Menu/main_menu_resume_icon.png";
    private const string OutlinePath = "Assets/Main/Sprites/1 Menu/Main Menu/main_menu_card_outline.png";
    private const float Shift = 156f;

    [MenuItem("Tools/Menu/Add Continue card")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var outlineImporter = AssetImporter.GetAtPath(OutlinePath) as TextureImporter;
        if (outlineImporter != null && (outlineImporter.textureType != TextureImporterType.Sprite || outlineImporter.spriteBorder == Vector4.zero))
        {
            outlineImporter.textureType = TextureImporterType.Sprite;
            outlineImporter.spriteImportMode = SpriteImportMode.Single;
            outlineImporter.mipmapEnabled = false;
            outlineImporter.alphaIsTransparency = true;
            outlineImporter.spriteBorder = new Vector4(36f, 36f, 36f, 36f);   // the corners stay round when the card is stretched
            outlineImporter.SaveAndReimport();
        }
        Sprite outline = AssetDatabase.LoadAssetAtPath<Sprite>(OutlinePath);

        var scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        GameObject play = GameObject.Find("play_card");
        GameObject phrasebook = GameObject.Find("phrasebook_card");
        GameObject library = GameObject.Find("library_card");
        if (play == null || phrasebook == null || library == null)
        {
            return "SKIPPED (the Main menu cards were not found)";
        }

        // move the cards down, only the first time
        GameObject old = GameObject.Find("continue_card");
        bool firstTime = old == null;
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }
        if (firstTime)
        {
            foreach (GameObject card in new[] { phrasebook, play, library })
            {
                var rt = (RectTransform)card.transform;
                rt.anchoredPosition += new Vector2(0f, -Shift);
            }
        }

        // the new card is a copy of the Play card
        GameObject copy = Object.Instantiate(play, play.transform.parent);
        copy.name = "continue_card";
        var copyRect = (RectTransform)copy.transform;
        copyRect.anchoredPosition = new Vector2(580f, -352f);
        copy.transform.SetSiblingIndex(phrasebook.transform.GetSiblingIndex());

        var button = copy.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        var cardImage = copy.GetComponent<Image>();
        cardImage.color = new Color32(255, 244, 220, 236);   // a little warmer than the other cards

        Transform iconBox = copy.transform.Find("icon");
        iconBox.GetComponent<Image>().color = new Color32(232, 117, 26, 255);
        Transform triangle = iconBox.Find("triangle");
        var triangleImage = triangle.GetComponent<Image>();
        triangleImage.sprite = icon;
        triangleImage.preserveAspect = true;
        var triangleRect = (RectTransform)triangle;
        triangleRect.sizeDelta = new Vector2(76f, 76f);
        triangleRect.anchoredPosition = new Vector2(14f, -14f);

        TMP_Text title = null, line = null;
        foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
        {
            if (title == null) title = text; else line = text;
        }
        title.text = "Continue";
        line.text = "Wordle · Cebuano · Food and drink · Level 4";
        line.enableWordWrapping = false;
        line.enableAutoSizing = true;
        line.fontSizeMin = 22f;
        line.fontSizeMax = 36f;
        line.overflowMode = TextOverflowModes.Ellipsis;

        var component = copy.AddComponent<ResumeCard>();
        var so = new SerializedObject(component);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("cardImage").objectReferenceValue = cardImage;
        so.FindProperty("iconImage").objectReferenceValue = iconBox.GetComponent<Image>();
        so.FindProperty("outlineSprite").objectReferenceValue = outline;
        so.FindProperty("titleLabel").objectReferenceValue = title;
        so.FindProperty("lineLabel").objectReferenceValue = line;
        SerializedProperty below = so.FindProperty("cardsBelow");
        below.arraySize = 3;
        below.GetArrayElementAtIndex(0).objectReferenceValue = phrasebook.transform;
        below.GetArrayElementAtIndex(1).objectReferenceValue = play.transform;
        below.GetArrayElementAtIndex(2).objectReferenceValue = library.transform;
        so.FindProperty("shift").floatValue = Shift;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Continue card " + (firstTime ? "added, the other cards moved down by " + Shift : "rebuilt");
    }
}
