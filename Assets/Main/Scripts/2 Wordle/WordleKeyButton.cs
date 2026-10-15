using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>What a key on the on-screen Wordle keyboard does.</summary>
public enum WordleKeyKind
{
    Letter,
    Enter,
    Backspace
}

/// <summary>
/// WordleKeyButton
/// One key of the on-screen Wordle keyboard (put on each key object together with a Button and an Image).
/// WordleGameController listens to Pressed and types the letter, submits the row or deletes a letter. Keys stay plain
/// after a guess (so the keypad does not give away right or wrong letters); if WordleGameController.colorKeyboardKeys is
/// turned on, the controller colours letter keys green / yellow / gray through SetState instead. Letter keys remember
/// the best state they ever had (green beats yellow beats gray).
/// </summary>
public class WordleKeyButton : MonoBehaviour
{
    public WordleKeyKind kind;
    public char letter;
    public Image background;
    public TMP_Text label;

    [SerializeField] private Color idleColor = new Color32(253, 248, 238, 255);
    [SerializeField] private Color idleText = new Color32(59, 42, 26, 255);

    /// <summary>Raised when the player taps the key.</summary>
    public event System.Action<WordleKeyButton> Pressed;

    private int stateRank;

    private void Awake()
    {
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (Pressed != null)
        {
            Pressed(this);
        }
    }

    /// <summary>Colours the key. rank: 1 = not in the word, 2 = wrong position, 3 = correct. Lower ranks never overwrite higher ones.</summary>
    public void SetState(int rank, Color color)
    {
        if (kind != WordleKeyKind.Letter || rank <= stateRank)
        {
            return;
        }
        stateRank = rank;
        if (background != null) background.color = color;
        if (label != null) label.color = Color.white;
    }

    /// <summary>Returns the key to its plain look.</summary>
    public void ResetState()
    {
        stateRank = 0;
        if (background != null) background.color = idleColor;
        if (label != null) label.color = idleText;
    }
}
