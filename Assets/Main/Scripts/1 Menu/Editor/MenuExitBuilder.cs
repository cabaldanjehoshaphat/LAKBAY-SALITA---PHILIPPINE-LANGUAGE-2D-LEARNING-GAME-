using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static LearnSceneBuilder;

/// <summary>
/// MenuExitBuilder
/// Editor tool that adds the Exit button to the Main menu. Menu: Tools > Menu > "Add Exit button". It builds, under MenuUI,
///  - "exit_button": a round red X button with a white border and a white X, in the bottom-left corner of the 1920 x 1080 canvas,
///  - "exit_panel": the hidden "Exit game?" window (a dimmed background, a card with Cancel and Exit buttons),
///  - "ExitController": the ExitButton script that drives them.
/// Safe to run again: the three objects are rebuilt.
/// </summary>
public static class MenuExitBuilder
{
    private const string MenuPath = "Assets/Main/Scenes/1 Menu/1 Main_menu.unity";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Sand = new Color32(239, 231, 212, 255);
    private static readonly Color Coral = new Color32(217, 96, 59, 255);
    private static readonly Color Red = new Color32(228, 36, 36, 255);
    private static readonly Color Navy = new Color32(30, 58, 138, 255);

    [MenuItem("Tools/Menu/Add Exit button")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        GameObject ui = GameObject.Find("MenuUI");
        if (ui == null)
        {
            return "SKIPPED (no MenuUI)";
        }
        foreach (string old in new[] { "exit_button", "exit_panel", "ExitController" })
        {
            Transform t = ui.transform.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // ---- the X button, bottom-left (x 40, y 936 of 1080): a white ring around a red disc with a white X
        Image exitRing = LibraryUiKit.Icon(ui.transform, "exit_button", LibraryUiKit.Circle, Color.white);
        LibraryUiKit.Place(exitRing.rectTransform, 40, 936, 104, 104);
        Image exitDisc = LibraryUiKit.Icon(exitRing.transform, "fill", LibraryUiKit.Circle, Red);
        LibraryUiKit.Place(exitDisc.rectTransform, 8, 8, 88, 88);
        TextMeshProUGUI exitX = Lab(exitDisc.transform, "X", 62, Color.white, true, TextAlignmentOptions.Center, 0, 0, 88, 88);
        exitX.raycastTarget = false;
        Button exitButton = MakeButton(exitRing);

        // ---- the confirmation window
        Image dim = LibraryUiKit.Box(ui.transform, "exit_panel", new Color(0.08f, 0.13f, 0.24f, 0.5f), false);
        LibraryUiKit.Fill(dim.rectTransform);
        dim.raycastTarget = true;
        Image card = LibraryUiKit.Box(dim.transform, "exit_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 660, 340, 600, 400);
        TextMeshProUGUI ribbonLabel;
        Pill(card.transform, "ribbon", "Exit game?", 100, -48, 400, 100, Navy, Color.white, 50, true, out ribbonLabel);
        Lab(card.transform, "Your progress is saved.", 32, InkSoft, false, TextAlignmentOptions.Center, 40, 120, 520, 80);
        TextMeshProUGUI cancelLabel, confirmLabel;
        Image cancelImg = Pill(card.transform, "cancel_button", "Cancel", 50, 250, 230, 84, Sand, Ink, 40, true, out cancelLabel);
        Button cancel = MakeButton(cancelImg);
        Image confirmImg = Pill(card.transform, "confirm_button", "Exit", 320, 250, 230, 84, Coral, Color.white, 40, true, out confirmLabel);
        Button confirm = MakeButton(confirmImg);
        dim.gameObject.SetActive(false);

        // ---- controller
        RectTransform controllerRect = LibraryUiKit.NewRect("ExitController", ui.transform);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<ExitButton>();
        var so = new SerializedObject(controller);
        SetRef(so, "panel", dim.gameObject);
        SetRef(so, "openButton", exitButton);
        SetRef(so, "confirmButton", confirm);
        SetRef(so, "cancelButton", cancel);
        so.ApplyModifiedPropertiesWithoutUndo();
        controllerRect.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Exit button and Exit game? window built";
    }
}
