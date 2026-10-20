using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ExitButton
/// The Exit button in the bottom-left corner of the Main menu. Pressing it opens a small "Exit game?" window; "Exit" closes the game
/// (Application.Quit in a build; in the editor it stops Play mode) and "Cancel" or Esc closes the window again. Progress is saved after
/// every puzzle, so nothing is lost; the save files are written once more just before quitting. The button and the window are built into the
/// main menu scene by MenuExitBuilder (Tools > Menu); the references are assigned there.
/// </summary>
public class ExitButton : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (confirmButton != null) confirmButton.onClick.AddListener(Quit);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        if (panel != null && Application.isPlaying) panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && panel != null && panel.activeSelf)
        {
            Close();
        }
    }

    /// <summary>Shows the "Exit game?" window.</summary>
    public void Open()
    {
        if (panel != null) panel.SetActive(true);
    }

    /// <summary>Hides the window without leaving the game.</summary>
    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Closes the game (stops Play mode when running in the editor).</summary>
    public void Quit()
    {
        SaveStore.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
