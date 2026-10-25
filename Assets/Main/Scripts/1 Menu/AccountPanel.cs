using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AccountPanel
/// The "Account" window of the Profile screen: sign in with a Gmail account to keep the progress online and use it on another device (see CloudSave).
///  - Not logged in: one button, "Log in using Gmail". It opens the Google sign in; while the player is choosing an account the button reads "Waiting for
///    Google... (tap to cancel)". After a successful sign in the progress is synced (merged with what this device has).
///  - Logged in: who is signed in (the Gmail address), the status line ("Saved online at 14:32"), one button "Log out account" and a red outlined button
///    "Delete my online data". Both ask first with an OK / Cancel question. Logging out saves the progress online, forgets the login and clears the progress
///    on this device (so another account can log in here without mixing); deleting removes the online copy, logs out and clears the device too.
/// The window opens from the button on the profile card; that button reads "Log in using Gmail" or "Account · online". After a sign in or a logout the Profile
/// screen is refreshed (ProfileScreenController.Refresh), because the profile may have changed. Built into the Profile scene by AccountPanelBuilder
/// (Tools > Profile > "Add Account window"). The game works without an account; nothing is sent to the server unless the player signs in.
/// </summary>
public class AccountPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private TMP_Text openButtonLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text signedInLabel;
    [Header("Not logged in")]
    [SerializeField] private GameObject signedOutGroup;
    [SerializeField] private Button googleButton;
    [SerializeField] private TMP_Text googleLabel;
    [Header("Logged in")]
    [SerializeField] private GameObject signedInGroup;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button deleteButton;
    [Tooltip("Refreshed after a sign in or a logout, because the profile may have changed.")]
    [SerializeField] private ProfileScreenController profileScreen;
    [Header("OK / Cancel question")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text confirmTitle;
    [SerializeField] private TMP_Text confirmBody;
    [SerializeField] private Button confirmOkButton;
    [SerializeField] private Image confirmOkImage;
    [SerializeField] private Button confirmCancelButton;

    private static readonly Color Good = new Color32(93, 160, 42, 255);
    private static readonly Color Bad = new Color32(163, 45, 45, 255);
    private static readonly Color Neutral = new Color32(107, 82, 55, 255);
    private static readonly Color Navy = new Color32(27, 58, 140, 255);
    private Action _onConfirm;

    private void Awake()
    {
        Hook(openButton, Open);
        Hook(closeButton, Close);
        Hook(googleButton, SignInWithGoogle);
        Hook(logoutButton, AskLogOut);
        Hook(deleteButton, AskDelete);
        Hook(confirmOkButton, ConfirmOk);
        Hook(confirmCancelButton, CloseQuestion);
        if (panel != null) panel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    private void OnEnable()
    {
        CloudSave.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        CloudSave.Changed -= Refresh;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && panel != null && panel.activeSelf)
        {
            if (confirmPanel != null && confirmPanel.activeSelf)
            {
                CloseQuestion();
            }
            else
            {
                Close();
            }
        }
    }

    private static void Hook(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        CloseQuestion();
        Refresh();
    }

    public void Close()
    {
        CloseQuestion();
        if (panel != null) panel.SetActive(false);
    }

    // ------------------------------------------------------------------ OK / Cancel question

    private void Ask(string title, string body, Color okColor, Action onOk)
    {
        if (confirmPanel == null)
        {
            onOk();   // no question window in the scene: do it
            return;
        }
        _onConfirm = onOk;
        if (confirmTitle != null) confirmTitle.text = title;
        if (confirmBody != null) confirmBody.text = body;
        if (confirmOkImage != null) confirmOkImage.color = okColor;
        confirmPanel.SetActive(true);
        confirmPanel.transform.SetAsLastSibling();
    }

    private void ConfirmOk()
    {
        Action action = _onConfirm;
        CloseQuestion();
        if (action != null) action();
    }

    private void CloseQuestion()
    {
        _onConfirm = null;
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ------------------------------------------------------------------ actions

    private void SignInWithGoogle()
    {
        if (CloudSave.GoogleWaiting)
        {
            CloudSave.CancelGoogle();   // the same button cancels while the player is choosing an account
            return;
        }
        if (CloudSave.Busy)
        {
            return;
        }
        SetStatus("Opening Google...", Neutral);
        CloudSave.SignInWithGoogle(delegate (bool ok, string text)
        {
            SetStatus(text, ok ? Good : Bad);
            if (profileScreen != null) profileScreen.Refresh();
            Refresh();
        });
        Refresh();
    }

    private void AskLogOut()
    {
        if (CloudSave.Busy)
        {
            return;
        }
        Ask("Log out?", "Your progress is saved online first, then this device is cleared. Log in with Gmail again to get it back.", Navy, LogOut);
    }

    private void LogOut()
    {
        SetStatus("Logging out...", Neutral);
        CloudSave.SignOut(delegate
        {
            SetStatus(CloudSave.Status, Neutral);
            if (profileScreen != null) profileScreen.Refresh();   // the progress on the device was cleared
            Refresh();
        });
    }

    private void AskDelete()
    {
        if (CloudSave.Busy)
        {
            return;
        }
        Ask("Delete online data?", "This deletes your progress stored online for this Gmail account, logs you out and clears this device. It can not be undone.", Bad, DeleteOnline);
    }

    private void DeleteOnline()
    {
        if (CloudSave.Busy)
        {
            return;
        }
        SetStatus("Please wait...", Neutral);
        CloudSave.DeleteOnlineData(delegate (bool ok, string text)
        {
            if (!ok)
            {
                SetStatus(text, Bad);
                Refresh();
                return;
            }
            CloudSave.SignOut(delegate
            {
                SetStatus(CloudSave.Status, Good);
                if (profileScreen != null) profileScreen.Refresh();
                Refresh();
            }, true);
        });
    }

    private void SetStatus(string text, Color color)
    {
        if (statusLabel != null)
        {
            statusLabel.text = text;
            statusLabel.color = color;
        }
    }

    /// <summary>Shows the logged-in or the logged-out part, and the button labels.</summary>
    public void Refresh()
    {
        bool signedIn = CloudSave.SignedIn;
        if (signedOutGroup != null) signedOutGroup.SetActive(!signedIn);
        if (signedInGroup != null) signedInGroup.SetActive(signedIn);
        if (signedInLabel != null) signedInLabel.text = signedIn ? "Signed in as\n" + CloudSave.DisplayName : string.Empty;
        if (openButtonLabel != null) openButtonLabel.text = signedIn ? "Account · online" : "Log in using Gmail";
        if (googleLabel != null) googleLabel.text = CloudSave.GoogleWaiting ? "Waiting for Google...  (tap to cancel)" : "Log in using Gmail";
        if (statusLabel != null && string.IsNullOrEmpty(statusLabel.text))
        {
            SetStatus(signedIn ? "Your progress is saved online." : "Log in with your Gmail account to keep your progress and use it on another device.", Neutral);
        }
        bool busy = CloudSave.Busy;
        if (statusLabel != null && !string.IsNullOrEmpty(CloudSave.Status) && statusLabel.text != CloudSave.Status)
        {
            SetStatus(CloudSave.Status, Neutral);   // the latest message of the sign in or of an automatic sync ("Saved online at 14:32.")
        }
        if (googleButton != null) googleButton.interactable = !busy;
        if (logoutButton != null) logoutButton.interactable = !busy;
        if (deleteButton != null) deleteButton.interactable = !busy;
    }
}
