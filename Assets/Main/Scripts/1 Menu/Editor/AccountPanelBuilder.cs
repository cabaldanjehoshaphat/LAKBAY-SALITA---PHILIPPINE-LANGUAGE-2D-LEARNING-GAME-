using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static LearnSceneBuilder;

/// <summary>
/// AccountPanelBuilder
/// Editor tool that adds the account feature to the Profile screen (scene "2 Profile Icon"): sign in with a Gmail account. Menu: Tools > Profile >
/// "Add Account window". It adds
///  - a button on the profile card (between "Edit profile" and "Reset progress"): "Log in using Gmail", or "Account · online" when logged in,
///  - the Account window (a dialog like Edit profile): a status line; not logged in: ONE big button "Log in using Gmail"; logged in: who is signed in,
///    one button "Log out account" and a red outlined button "Delete my online data"; and a Close button,
///  - a confirmation dialog (OK / Cancel) that the window shows before logging out or deleting the online data,
///  - the AccountPanel component (the logic, see AccountPanel and CloudSave) with all references assigned.
/// There are no username, email or password fields. ProfileDesignBuilder calls AddToOpenScene at the end of a rebuild, so the Account window is never lost.
/// Safe to run again: an earlier one is replaced.
/// </summary>
public static class AccountPanelBuilder
{
    private const string ProfileScenePath = "Assets/Main/Scenes/1 Menu/2 Profile Icon.unity";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Navy = new Color32(27, 58, 140, 255);
    private static readonly Color Red = new Color32(163, 45, 45, 255);
    private static readonly Color GoogleBlue = new Color32(66, 133, 244, 255);

    [MenuItem("Tools/Profile/Add Account window")]
    public static void BuildMenu()
    {
        var scene = EditorSceneManager.OpenScene(ProfileScenePath, OpenSceneMode.Single);
        string result = AddToOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(result);
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

    public static string AddToOpenScene()
    {
        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            return "SKIPPED (no Canvas: not the Profile scene)";
        }
        Transform card = FindDeep(canvasGo.transform, "profile_card");
        Transform overlays = FindDeep(canvasGo.transform, "ProfileOverlays");
        var profileScreen = Object.FindObjectOfType<ProfileScreenController>();
        if (card == null || overlays == null || profileScreen == null)
        {
            return "SKIPPED (the redesigned Profile screen was not found)";
        }
        // an earlier one is replaced
        foreach (string old in new[] { "account_button", "account_panel", "AccountController" })
        {
            Transform t = FindDeep(canvasGo.transform, old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // ---- the button on the profile card
        TextMeshProUGUI openLabel;
        Image openImg = Pill(card, "account_button", "Log in using Gmail", 48, 722, 484, 58, Navy, Color.white, 32, true, out openLabel);
        Button openButton = MakeButton(openImg);

        // ---- the window
        Image dim = LibraryUiKit.Box(overlays, "account_panel", new Color(0.07f, 0.16f, 0.36f, 0.5f), false);
        LibraryUiKit.Fill(dim.rectTransform);
        dim.raycastTarget = true;
        Image win = LibraryUiKit.Box(dim.transform, "card", Cream, true);
        LibraryUiKit.Place(win.rectTransform, 560, 220, 800, 600);
        TextMeshProUGUI ribbonLabel;
        Pill(win.transform, "ribbon", "Account", 200, -44, 400, 100, Navy, Color.white, 48, true, out ribbonLabel);
        TextMeshProUGUI status = Lab(win.transform, "", 30, InkSoft, false, TextAlignmentOptions.Center, 40, 80, 720, 110);
        status.enableWordWrapping = true;

        // not logged in: ONE button
        RectTransform outGroup = LibraryUiKit.NewRect("signed_out", win.transform);
        LibraryUiKit.Place(outGroup, 0, 210, 800, 260);
        Image gOuter = LibraryUiKit.Box(outGroup, "google_button", Ink, true);
        LibraryUiKit.Place(gOuter.rectTransform, 80, 20, 640, 110);
        Image gInner = LibraryUiKit.Box(gOuter.transform, "inner", Color.white, true);
        LibraryUiKit.Place(gInner.rectTransform, 4, 4, 632, 102);
        Image gCircle = LibraryUiKit.Icon(gInner.transform, "g_circle", HdCircle(), GoogleBlue);
        LibraryUiKit.Place(gCircle.rectTransform, 28, 21, 60, 60);
        Lab(gCircle.transform, "G", 40, Color.white, true, TextAlignmentOptions.Center, 0, 0, 60, 60);
        TextMeshProUGUI googleLabel = Lab(gInner.transform, "Log in using Gmail", 38, Ink, true, TextAlignmentOptions.Center, 100, 0, 520, 102);
        googleLabel.enableWordWrapping = false;
        googleLabel.enableAutoSizing = true;
        googleLabel.fontSizeMin = 26f;
        googleLabel.fontSizeMax = 38f;
        Button google = MakeButton(gOuter);
        Lab(outGroup, "Only your Gmail address is saved with your progress.", 26, InkSoft, false, TextAlignmentOptions.Center, 60, 160, 680, 80).enableWordWrapping = true;

        // logged in: who, and ONE button
        RectTransform inGroup = LibraryUiKit.NewRect("signed_in", win.transform);
        LibraryUiKit.Place(inGroup, 0, 210, 800, 260);
        TextMeshProUGUI signedInLabel = Lab(inGroup, "", 36, Ink, true, TextAlignmentOptions.Center, 40, 0, 720, 100);
        signedInLabel.enableWordWrapping = true;
        signedInLabel.enableAutoSizing = true;
        signedInLabel.fontSizeMin = 24f;
        signedInLabel.fontSizeMax = 36f;
        Image outOuter = LibraryUiKit.Box(inGroup, "log_out_button", Ink, true);
        LibraryUiKit.Place(outOuter.rectTransform, 80, 104, 640, 84);
        Image outInner = LibraryUiKit.Box(outOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(outInner.rectTransform, 4, 4, 632, 76);
        Lab(outInner.transform, "Log out account", 40, Ink, true, TextAlignmentOptions.Center, 0, 0, 632, 76);
        Button logout = MakeButton(outOuter);
        Image delOuter = LibraryUiKit.Box(inGroup, "delete_button", Red, true);
        LibraryUiKit.Place(delOuter.rectTransform, 80, 204, 640, 76);
        Image delInner = LibraryUiKit.Box(delOuter.transform, "inner", new Color32(253, 236, 236, 255), true);
        LibraryUiKit.Place(delInner.rectTransform, 4, 4, 632, 68);
        Lab(delInner.transform, "Delete my online data", 34, Red, true, TextAlignmentOptions.Center, 0, 0, 632, 68);
        Button delete = MakeButton(delOuter);

        // close
        Image closeOuter = LibraryUiKit.Box(win.transform, "close_button", Ink, true);
        LibraryUiKit.Place(closeOuter.rectTransform, 250, 512, 300, 72);
        Image closeInner = LibraryUiKit.Box(closeOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(closeInner.rectTransform, 4, 4, 292, 64);
        Lab(closeInner.transform, "Close", 36, Ink, false, TextAlignmentOptions.Center, 0, 0, 292, 64);
        Button close = MakeButton(closeOuter);

        // the OK / Cancel question (logging out, deleting the online data); AccountPanel fills the texts
        Image askDim = LibraryUiKit.Box(dim.transform, "confirm", new Color(0.07f, 0.16f, 0.36f, 0.55f), false);
        LibraryUiKit.Fill(askDim.rectTransform);
        askDim.raycastTarget = true;
        Image askCard = LibraryUiKit.Box(askDim.transform, "card", Cream, true);
        LibraryUiKit.Place(askCard.rectTransform, 660, 320, 600, 440);
        TextMeshProUGUI askTitle = Lab(askCard.transform, "Log out?", 46, Ink, true, TextAlignmentOptions.Center, 0, 40, 600, 70);
        TextMeshProUGUI askBody = Lab(askCard.transform, "", 32, InkSoft, false, TextAlignmentOptions.Top, 48, 130, 504, 180);
        askBody.enableWordWrapping = true;
        Image askCancelOuter = LibraryUiKit.Box(askCard.transform, "cancel_button", Ink, true);
        LibraryUiKit.Place(askCancelOuter.rectTransform, 40, 332, 250, 84);
        Image askCancelInner = LibraryUiKit.Box(askCancelOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(askCancelInner.rectTransform, 4, 4, 242, 76);
        Lab(askCancelInner.transform, "Cancel", 38, Ink, false, TextAlignmentOptions.Center, 0, 0, 242, 76);
        Button askCancel = MakeButton(askCancelOuter);
        TextMeshProUGUI askOkLabel;
        Image askOkImg = Pill(askCard.transform, "ok_button", "OK", 310, 332, 250, 84, Navy, Color.white, 40, true, out askOkLabel);
        Button askOk = MakeButton(askOkImg);
        askDim.gameObject.SetActive(false);

        // ---- the logic
        RectTransform controllerRect = LibraryUiKit.NewRect("AccountController", canvasGo.transform);
        LibraryUiKit.Fill(controllerRect);
        var controller = controllerRect.gameObject.AddComponent<AccountPanel>();
        var so = new SerializedObject(controller);
        SetRef(so, "panel", dim.gameObject);
        SetRef(so, "openButton", openButton);
        SetRef(so, "openButtonLabel", openLabel);
        SetRef(so, "closeButton", close);
        SetRef(so, "statusLabel", status);
        SetRef(so, "signedInLabel", signedInLabel);
        SetRef(so, "signedOutGroup", outGroup.gameObject);
        SetRef(so, "googleButton", google);
        SetRef(so, "googleLabel", googleLabel);
        SetRef(so, "signedInGroup", inGroup.gameObject);
        SetRef(so, "logoutButton", logout);
        SetRef(so, "deleteButton", delete);
        SetRef(so, "confirmPanel", askDim.gameObject);
        SetRef(so, "confirmTitle", askTitle);
        SetRef(so, "confirmBody", askBody);
        SetRef(so, "confirmOkButton", askOk);
        SetRef(so, "confirmOkImage", askOkImg);
        SetRef(so, "confirmCancelButton", askCancel);
        SetRef(so, "profileScreen", profileScreen);
        so.ApplyModifiedPropertiesWithoutUndo();

        inGroup.gameObject.SetActive(false);   // the saved scene shows the logged-out window; it switches by itself when the game runs
        dim.gameObject.SetActive(false);
        overlays.SetAsLastSibling();
        controllerRect.SetAsLastSibling();
        EditorUtility.SetDirty(canvasGo);
        return "Account window added to the Profile screen";
    }

    /// <summary>A 256 px circle sprite (sharper than the 64 px one used for small dots), falling back to the small one.</summary>
    private static Sprite HdCircle()
    {
        Sprite s = Spr("main_menu_circle_hd.png");
        return s != null ? s : LibraryUiKit.Circle;
    }
}
