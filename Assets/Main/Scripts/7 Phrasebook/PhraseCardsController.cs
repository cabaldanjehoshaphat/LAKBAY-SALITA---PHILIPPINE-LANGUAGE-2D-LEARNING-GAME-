using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PhraseCardsController
/// Runs the phrase cards (scene "Phrasebook - Cards"): one card per phrase of a topic. A card shows the English line, the phrase in the language
/// and, when the phrase has word-by-word meanings, a tile per word with its meaning, so the player sees how the phrase is made. Back and Next
/// move through the phrases, "Build this topic" opens the Sentence builder on the shown phrase. The top-left back button and Esc return to the
/// topics screen. What to show is read from PhraseSession. The gloss tiles are copies of the hidden "gloss_template" object in the scene;
/// references are assigned there by PhrasebookSceneBuilder.
/// </summary>
public class PhraseCardsController : MonoBehaviour
{
    [Header("Top bar")]
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text languageLabel;
    [SerializeField] private TMP_Text topicLabel;

    [Header("Card")]
    [SerializeField] private TMP_Text counterLabel;
    [SerializeField] private TMP_Text englishLabel;
    [SerializeField] private TMP_Text phraseLabel;
    [SerializeField] private RectTransform glossRow;
    [SerializeField] private GameObject glossTemplate;

    [Header("Tap to hear")]
    [SerializeField] private Button speakButton;
    [SerializeField] private Image speakImage;

    [Header("Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button buildButton;

    private int language;
    private int topic;
    private int index;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(Leave);
        if (previousButton != null) previousButton.onClick.AddListener(delegate { Step(-1); });
        if (nextButton != null) nextButton.onClick.AddListener(delegate { Step(1); });
        if (buildButton != null) buildButton.onClick.AddListener(BuildThis);
        if (speakButton != null)
        {
            speakButton.onClick.AddListener(Speak);
            if (Application.isPlaying) speakButton.gameObject.SetActive(WordAudio.Enabled);
        }
    }

    /// <summary>Tap to hear: plays the recording of the whole phrase.</summary>
    private void Speak()
    {
        PhraseEntry entry = PhraseData.Phrase(language, topic, index);
        if (entry != null)
        {
            WordAudio.Play(language, entry.phrase);
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Leave();
        }
    }

    /// <summary>Re-reads PhraseSession and shows the chosen phrase (also used for the edit-mode preview).</summary>
    [ContextMenu("Refresh preview")]
    public void Refresh()
    {
        language = Mathf.Clamp(PhraseSession.Language, 0, 2);
        topic = Mathf.Max(0, PhraseSession.Topic);
        index = Mathf.Max(0, PhraseSession.Phrase);
        PhraseTopicDef def = PhraseData.Topic(language, topic);
        if (languageLabel != null) languageLabel.text = PhraseData.LanguageNames[language];
        if (topicLabel != null) topicLabel.text = def != null ? def.name : string.Empty;
        ShowPhrase();
    }

    private void ShowPhrase()
    {
        int count = PhraseData.PhraseCount(language, topic);
        index = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        PhraseSession.Phrase = index;
        PhraseEntry entry = PhraseData.Phrase(language, topic, index);

        if (counterLabel != null) counterLabel.text = count > 0 ? "Phrase " + (index + 1) + " of " + count : string.Empty;
        if (englishLabel != null) englishLabel.text = entry != null ? entry.english : string.Empty;
        if (phraseLabel != null) phraseLabel.text = entry != null ? entry.phrase : string.Empty;
        BuildGloss(entry);
        if (previousButton != null) previousButton.interactable = index > 0;
        if (nextButton != null) nextButton.interactable = index < count - 1;
        if (speakImage != null && Application.isPlaying)
        {
            Color c = speakImage.color;
            c.a = entry != null && WordAudio.Has(language, entry.phrase) ? 1f : 0.5f;
            speakImage.color = c;
        }
    }

    /// <summary>One tile per word with its meaning (only when the phrase has a meaning for every word).</summary>
    private void BuildGloss(PhraseEntry entry)
    {
        if (glossRow == null || glossTemplate == null)
        {
            return;
        }
        for (int c = glossRow.childCount - 1; c >= 0; c--)
        {
            Transform child = glossRow.GetChild(c);
            if (child.gameObject != glossTemplate)
            {
                LibraryUiKit.DestroyObject(child.gameObject);
            }
        }
        if (entry == null)
        {
            return;
        }
        string[] words = PhraseData.Tokens(entry.phrase);
        if (words.Length == 0 || entry.gloss == null || entry.gloss.Count != words.Length)
        {
            return;
        }
        for (int i = 0; i < words.Length; i++)
        {
            GameObject tile = Instantiate(glossTemplate, glossRow, false);
            tile.name = "gloss_" + (i + 1);
            tile.SetActive(true);
            TMP_Text word = tile.transform.Find("Word").GetComponent<TMP_Text>();
            TMP_Text meaning = tile.transform.Find("Meaning").GetComponent<TMP_Text>();
            word.text = words[i];
            meaning.text = entry.gloss[i];
            // tap a word tile to hear that word on its own
            Button tileButton = tile.GetComponent<Button>();
            if (tileButton != null)
            {
                if (WordAudio.Enabled)
                {
                    string spoken = words[i];
                    tileButton.onClick.AddListener(delegate { WordAudio.Play(language, spoken); });
                }
                else
                {
                    Destroy(tileButton);   // Tap to hear is off: the tile is plain text again
                }
            }
            var layout = tile.GetComponent<LayoutElement>();
            if (layout != null)
            {
                float w = Mathf.Max(word.GetPreferredValues(words[i]).x, meaning.GetPreferredValues(entry.gloss[i]).x);
                layout.preferredWidth = Mathf.Clamp(w + 60f, 200f, 420f);
            }
        }
    }

    private void Step(int direction)
    {
        index += direction;
        ShowPhrase();
    }

    private void BuildThis()
    {
        PhraseSession.OpenBuilder(language, topic, index);
    }

    /// <summary>Back to the topics screen.</summary>
    public void Leave()
    {
        SceneManager.LoadScene(PhraseSession.TopicsScene);
    }
}
