using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static LearnSceneBuilder;

/// <summary>
/// IntroSceneBuilder
/// Editor tool for the intro cutscene. Menu: Tools > Intro > "Build Intro scene". It
///  - sets the import settings of the character pictures in Assets/Main/Resources/Intro/Characters (Sprite, no mip maps, not compressed),
///  - builds the scene "0 Intro" (Assets/Main/Scenes/1 Menu): the usual sky (MainMenuTheme, follows the clock) and an "IntroStage" that
///    holds the IntroCutscene component, with the map, boat, gull and wave pictures of the Main menu assigned,
///  - puts the scene first in Build Settings (index 0), so the game starts with the cutscene and then goes to "1 Main_menu".
/// Safe to run again: the scene is rebuilt.
/// </summary>
public static class IntroSceneBuilder
{
    private const string ScenePath = "Assets/Main/Scenes/1 Menu/0 Intro.unity";
    private const string WavePath = "Assets/Main/Sprites/1 Menu/Main Menu/";
    private const string CharactersPath = "Assets/Main/Resources/Intro/Characters";

    [MenuItem("Tools/Intro/Build Intro scene")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        int sprites = SetImportSettings();

        Image bg;
        Transform canvas = NewScene(out bg);
        RectTransform ui = LibraryUiKit.NewRect("IntroUI", canvas);
        LibraryUiKit.Fill(ui);
        BuildSky(canvas, ui, bg, "IntroTheme");

        RectTransform stage = LibraryUiKit.NewRect("IntroStage", ui);
        LibraryUiKit.Fill(stage);
        var cutscene = stage.gameObject.AddComponent<IntroCutscene>();
        var so = new SerializedObject(cutscene);
        SetRef(so, "stage", stage);
        SetRef(so, "mapSprite", Spr("main_menu_map.png"));
        SetRef(so, "boatSprite", Spr("main_menu_boat.png"));
        SetRef(so, "gullSprite", Spr("main_menu_gull.png"));
        SetRef(so, "waveFillTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(WavePath + "main_menu_wave_fill.png"));
        SetRef(so, "waveCrestTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(WavePath + "main_menu_wave_crest.png"));
        SetRef(so, "textMaterial", Mat());
        so.FindProperty("nextScene").stringValue = "1 Main_menu";
        so.FindProperty("allowSkip").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        FirstInBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        return "saved " + ScenePath + " (first in Build Settings); " + sprites + " character pictures set up";
    }

    private static int SetImportSettings()
    {
        int count = 0;
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { CharactersPath });
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (importer.maxTextureSize < 512) { importer.maxTextureSize = 512; changed = true; }
                if (changed)
                {
                    importer.SaveAndReimport();
                }
                count++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();
        return count;
    }

    private static void FirstInBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(delegate (EditorBuildSettingsScene s) { return s.path == path; });
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
