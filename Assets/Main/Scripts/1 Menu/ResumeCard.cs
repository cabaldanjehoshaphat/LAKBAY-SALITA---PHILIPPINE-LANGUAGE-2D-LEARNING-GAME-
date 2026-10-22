using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ResumeCard
/// The "Continue" card at the top of the Main menu's cards (above Phrasebook, Play and Library). It shows the last puzzle the player opened,
/// for example "Wordle · Cebuano · Food and drink · Level 4", and pressing it opens that puzzle again (ResumeStore.Open).
///  - Puzzle not finished (the puzzle that was opened last has not been won yet, even if it was solved on an earlier day): the card is full
///    (warm cream with an orange icon) and can be pressed.
///  - Puzzle finished (it was won): nothing is left to resume, so the card is hollow: only an orange outline, a faint orange icon and muted text, with
///    " · Finished" at the end of the line, and it can not be pressed. It is full again as soon as another puzzle is opened.
///  - Nothing played yet (a new profile, or after "Reset progress"): the card is hidden while the game runs and the three cards below move
///    back up, so there is no gap. The scene is saved with the card showing and those cards moved down by "shift" pixels.
/// The card is added to the scene by ResumeCardBuilder (Tools > Menu > "Add Continue card"), which also assigns the references.
/// </summary>
public class ResumeCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image cardImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text lineLabel;
    [Tooltip("The thin outline of a rounded card (sliced), used for the hollow look.")]
    [SerializeField] private Sprite outlineSprite;
    [Tooltip("The cards under the Continue card (Phrasebook, Play, Library). They are saved moved down by Shift and move back up while the Continue card is hidden.")]
    [SerializeField] private RectTransform[] cardsBelow;
    [Tooltip("How far the cards below are moved down, in canvas pixels (the card height 140 plus the gap 16).")]
    [SerializeField] private float shift = 156f;

    private static readonly Color Orange = new Color32(232, 117, 26, 255);
    private static readonly Color FullCard = new Color32(255, 244, 220, 236);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);

    private bool _shifted = true;
    private Image _hollowFill;
    private Sprite _fullSprite;
    private Color _titleColor, _lineColor;

    private void Awake()
    {
        if (cardImage != null) _fullSprite = cardImage.sprite;
        if (titleLabel != null) _titleColor = titleLabel.color;
        if (lineLabel != null) _lineColor = lineLabel.color;
        if (button != null)
        {
            button.onClick.AddListener(ResumeStore.Open);
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>Reads what was played last and shows the card full, hollow or hidden.</summary>
    public void Refresh()
    {
        if (!Application.isPlaying)
        {
            return;   // in the editor the scene keeps its saved design
        }
        string title = null, line = null;
        bool finished = false;
        bool show = ResumeStore.Has && ResumeStore.Describe(out title, out line, out finished);
        if (show)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (lineLabel != null) lineLabel.text = line;
            SetHollow(finished);
        }
        SetShown(show);
    }

    private void SetHollow(bool hollow)
    {
        if (cardImage != null)
        {
            cardImage.sprite = hollow && outlineSprite != null ? outlineSprite : _fullSprite;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = hollow ? Orange : FullCard;
        }
        if (iconImage != null)
        {
            iconImage.color = hollow ? new Color(Orange.r, Orange.g, Orange.b, 0.35f) : Orange;
        }
        if (titleLabel != null) titleLabel.color = hollow ? Orange : _titleColor;
        if (lineLabel != null) lineLabel.color = hollow ? InkSoft : _lineColor;
        EnsureHollowFill();
        if (_hollowFill != null) _hollowFill.gameObject.SetActive(hollow);
        if (button != null) button.enabled = !hollow;   // a disabled Button is not pressed and does not tint the card
    }

    /// <summary>A faint white veil inside the outline, so the text can still be read over the map behind the card.</summary>
    private void EnsureHollowFill()
    {
        if (_hollowFill != null || cardImage == null)
        {
            return;
        }
        var go = new GameObject("hollow_fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = cardImage.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(cardImage.transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 5f);
        rect.offsetMax = new Vector2(-5f, -5f);
        _hollowFill = go.GetComponent<Image>();
        _hollowFill.sprite = _fullSprite;
        _hollowFill.type = Image.Type.Sliced;
        _hollowFill.color = new Color(1f, 1f, 1f, 0.55f);
        _hollowFill.raycastTarget = false;
    }

    private void SetShown(bool show)
    {
        transform.localScale = show ? Vector3.one : Vector3.zero;   // a hidden card is scaled to nothing, so it can not be seen or pressed
        if (show == _shifted || cardsBelow == null)
        {
            return;
        }
        foreach (RectTransform card in cardsBelow)
        {
            if (card != null)
            {
                card.anchoredPosition += new Vector2(0f, show ? -shift : shift);
            }
        }
        _shifted = show;
    }
}
