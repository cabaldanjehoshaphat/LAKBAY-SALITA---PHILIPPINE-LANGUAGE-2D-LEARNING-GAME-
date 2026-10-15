using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PhraseBuilderController
/// Runs the Sentence builder (scene "Phrasebook - Builder"): the player sees an English sentence and taps word tiles into the right order to
/// say it in the language. The tile bank holds the words of the phrase plus one wrong word taken from another phrase, in random order.
/// Tapping a tile puts it in the next empty slot; tapping a filled slot takes the word back; Clear empties every slot. Check compares the
/// sentence with the phrase: a right sentence marks the phrase as learned (PhraseProgressStore) and opens the result card, a wrong one costs one
/// of 4 tries. Hint puts the next correct word in place (2 per sentence). The result card has Next sentence (the next phrase, then the next
/// topic), See in Phrasebook (the phrase card) and Back to topics. The top-left back button and Esc return to the topics screen. Tiles and
/// slots are copies of the hidden "tile_template" and "slot_template" objects in the scene; references are assigned there by PhrasebookSceneBuilder.
/// </summary>
public class PhraseBuilderController : MonoBehaviour
{
    private const int MaxTries = 4;
    private const int HintsPerPhrase = 2;

    [Header("Top bar")]
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text languageLabel;
    [SerializeField] private TMP_Text topicLabel;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private Image[] triesDots;

    [Header("Prompt")]
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private TMP_Text englishLabel;

    [Header("Sentence")]
    [SerializeField] private RectTransform answerRow;
    [SerializeField] private RectTransform bankRow;
    [SerializeField] private GameObject slotTemplate;
    [SerializeField] private GameObject tileTemplate;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button hintButton;
    [SerializeField] private TMP_Text hintLabel;
    [SerializeField] private Button checkButton;
    [SerializeField] private TMP_Text messageLabel;

    [Header("Result card")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text resultSubtitle;
    [SerializeField] private TMP_Text resultSentence;
    [SerializeField] private TMP_Text resultEnglish;
    [SerializeField] private TMP_Text statTime;
    [SerializeField] private TMP_Text statTries;
    [SerializeField] private TMP_Text statStreak;
    [SerializeField] private TMP_Text resultNextLabel;
    [SerializeField] private Button resultSpeakButton;
    [SerializeField] private Button resultNextButton;
    [SerializeField] private Button resultPhrasebookButton;
    [SerializeField] private Button resultBackButton;

    [Header("Colours")]
    [SerializeField] private Color filledColor = new Color32(253, 248, 238, 255);
    [SerializeField] private Color emptyColor = new Color32(233, 242, 252, 255);
    [SerializeField] private Color dotOnColor = new Color32(93, 160, 42, 255);
    [SerializeField] private Color dotOffColor = new Color32(185, 183, 172, 255);

    private int language;
    private int topic;
    private int phraseIndex;
    private PhraseEntry entry;
    private string[] tokens = new string[0];
    private string[] bankTexts = new string[0];
    private readonly List<int> chosen = new List<int>();
    private readonly List<GameObject> slotObjects = new List<GameObject>();
    private readonly List<GameObject> tileObjects = new List<GameObject>();
    private int triesLeft;
    private int hintsLeft;
    private float startTime;
    private bool finished;
    private bool won;

    private void Awake()
    {
        Hook(backButton, Leave);
        Hook(clearButton, ClearAll);
        Hook(hintButton, UseHint);
        Hook(checkButton, CheckSentence);
        Hook(resultSpeakButton, SpeakPhrase);
        if (resultSpeakButton != null && Application.isPlaying) resultSpeakButton.gameObject.SetActive(WordAudio.Enabled);
        Hook(resultNextButton, ResultNext);
        Hook(resultPhrasebookButton, SeeInPhrasebook);
        Hook(resultBackButton, Leave);
    }

    private static void Hook(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private void Start()
    {
        StreakTracker.RecordPlay();
        LoadPhrase();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Leave();
        }
    }

    /// <summary>Reads PhraseSession and sets up the sentence (also used for the edit-mode preview).</summary>
    [ContextMenu("Refresh preview")]
    public void LoadPhrase()
    {
        language = Mathf.Clamp(PhraseSession.Language, 0, 2);
        topic = Mathf.Max(0, PhraseSession.Topic);
        phraseIndex = Mathf.Max(0, PhraseSession.Phrase);
        entry = PhraseData.Phrase(language, topic, phraseIndex);
        finished = false;
        triesLeft = MaxTries;
        hintsLeft = HintsPerPhrase;
        chosen.Clear();
        startTime = Time.unscaledTime;
        if (resultPanel != null) resultPanel.SetActive(false);
        if (messageLabel != null) messageLabel.text = string.Empty;

        PhraseTopicDef def = PhraseData.Topic(language, topic);
        int count = PhraseData.PhraseCount(language, topic);
        if (languageLabel != null) languageLabel.text = PhraseData.LanguageNames[language];
        if (topicLabel != null) topicLabel.text = def != null ? def.name : string.Empty;
        if (progressLabel != null) progressLabel.text = "Sentence " + (phraseIndex + 1) + " of " + count;
        if (promptLabel != null) promptLabel.text = "Say this in " + PhraseData.LanguageNames[language];
        if (englishLabel != null) englishLabel.text = entry != null ? entry.english : string.Empty;

        tokens = entry != null ? PhraseData.Tokens(entry.phrase) : new string[0];
        BuildBank();
        BuildSlots();
        RefreshBoard();
    }

    // ------------------------------------------------------------------ board

    private static string Norm(string s)
    {
        return (s ?? string.Empty).Trim().ToLowerInvariant();
    }

    /// <summary>The word tiles: the phrase's words plus one wrong word from another phrase, shuffled.</summary>
    private void BuildBank()
    {
        var words = new List<string>(tokens);
        var inPhrase = new HashSet<string>();
        foreach (string t in tokens) inPhrase.Add(Norm(t));
        var candidates = new List<string>();
        for (int t = 0; t < PhraseData.TopicCount(language); t++)
        {
            for (int p = 0; p < PhraseData.PhraseCount(language, t); p++)
            {
                foreach (string w in PhraseData.Tokens(PhraseData.Phrase(language, t, p).phrase))
                {
                    if (!inPhrase.Contains(Norm(w)) && !candidates.Contains(w) && w.Length <= 10 && !w.EndsWith("?") && !w.EndsWith("!"))
                    {
                        candidates.Add(w);
                    }
                }
            }
        }
        if (tokens.Length > 0 && candidates.Count > 0)
        {
            words.Add(candidates[Random.Range(0, candidates.Count)]);
        }
        for (int i = words.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string tmp = words[i];
            words[i] = words[j];
            words[j] = tmp;
        }
        bankTexts = words.ToArray();

        ClearClones(bankRow, tileTemplate);
        tileObjects.Clear();
        if (bankRow == null || tileTemplate == null)
        {
            return;
        }
        for (int i = 0; i < bankTexts.Length; i++)
        {
            int index = i;
            GameObject tile = Instantiate(tileTemplate, bankRow, false);
            tile.name = "tile_" + (i + 1);
            tile.SetActive(true);
            TMP_Text label = tile.GetComponentInChildren<TMP_Text>(true);
            label.text = bankTexts[i];
            var layout = tile.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = Mathf.Clamp(label.GetPreferredValues(bankTexts[i]).x + 70f, 140f, 320f);
            }
            tile.GetComponent<Button>().onClick.AddListener(delegate { OnTile(index); });
            tileObjects.Add(tile);
        }
    }

    private void BuildSlots()
    {
        ClearClones(answerRow, slotTemplate);
        slotObjects.Clear();
        if (answerRow == null || slotTemplate == null)
        {
            return;
        }
        int n = tokens.Length;
        float width = n > 0 ? Mathf.Min(280f, (1700f - 24f * (n - 1)) / n) : 280f;
        for (int i = 0; i < n; i++)
        {
            int index = i;
            GameObject slot = Instantiate(slotTemplate, answerRow, false);
            slot.name = "slot_" + (i + 1);
            slot.SetActive(true);
            var layout = slot.GetComponent<LayoutElement>();
            if (layout != null) layout.preferredWidth = width;
            slot.GetComponent<Button>().onClick.AddListener(delegate { OnSlot(index); });
            slotObjects.Add(slot);
        }
    }

    private static void ClearClones(RectTransform row, GameObject template)
    {
        if (row == null)
        {
            return;
        }
        for (int c = row.childCount - 1; c >= 0; c--)
        {
            Transform child = row.GetChild(c);
            if (template == null || child.gameObject != template)
            {
                LibraryUiKit.DestroyObject(child.gameObject);
            }
        }
    }

    private void RefreshBoard()
    {
        for (int i = 0; i < slotObjects.Count; i++)
        {
            bool filled = i < chosen.Count;
            Image img = slotObjects[i].GetComponent<Image>();
            if (img != null) img.color = filled ? filledColor : emptyColor;
            TMP_Text label = slotObjects[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = filled ? bankTexts[chosen[i]] : string.Empty;
            Button b = slotObjects[i].GetComponent<Button>();
            if (b != null) b.interactable = filled && !finished;
        }
        for (int i = 0; i < tileObjects.Count; i++)
        {
            bool used = chosen.Contains(i);
            Image img = tileObjects[i].GetComponent<Image>();
            if (img != null) img.color = used ? new Color(filledColor.r, filledColor.g, filledColor.b, 0.25f) : filledColor;
            TMP_Text label = tileObjects[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.alpha = used ? 0.25f : 1f;
            Button b = tileObjects[i].GetComponent<Button>();
            if (b != null) b.interactable = !used && !finished;
        }
        if (checkButton != null) checkButton.interactable = !finished && tokens.Length > 0 && chosen.Count == tokens.Length;
        if (hintButton != null) hintButton.interactable = !finished;
        if (clearButton != null) clearButton.interactable = !finished && chosen.Count > 0;
        if (hintLabel != null) hintLabel.text = hintsLeft > 0 ? "Hint · " + hintsLeft : "Hint · 0";
        for (int i = 0; triesDots != null && i < triesDots.Length; i++)
        {
            if (triesDots[i] != null) triesDots[i].color = i < triesLeft ? dotOnColor : dotOffColor;
        }
    }

    private void Say(string message)
    {
        if (messageLabel != null) messageLabel.text = message;
    }

    // ------------------------------------------------------------------ input

    private void OnTile(int bankIndex)
    {
        if (finished || chosen.Contains(bankIndex) || chosen.Count >= tokens.Length)
        {
            return;
        }
        chosen.Add(bankIndex);
        Say(string.Empty);
        RefreshBoard();
    }

    private void OnSlot(int slotIndex)
    {
        if (finished || slotIndex >= chosen.Count)
        {
            return;
        }
        chosen.RemoveAt(slotIndex);
        Say(string.Empty);
        RefreshBoard();
    }

    private void ClearAll()
    {
        if (finished)
        {
            return;
        }
        chosen.Clear();
        Say(string.Empty);
        RefreshBoard();
    }

    /// <summary>Hint: keeps the correct words from the start and puts the next correct word in place.</summary>
    private void UseHint()
    {
        if (finished || tokens.Length == 0)
        {
            return;
        }
        if (hintsLeft <= 0)
        {
            Say("No hints left");
            return;
        }
        int pos = 0;
        while (pos < chosen.Count && pos < tokens.Length && Norm(bankTexts[chosen[pos]]) == Norm(tokens[pos]))
        {
            pos++;
        }
        if (pos >= tokens.Length)
        {
            Say("Every word is in place. Press Check");
            return;
        }
        chosen.RemoveRange(pos, chosen.Count - pos);
        for (int i = 0; i < bankTexts.Length; i++)
        {
            if (!chosen.Contains(i) && Norm(bankTexts[i]) == Norm(tokens[pos]))
            {
                chosen.Add(i);
                break;
            }
        }
        hintsLeft--;
        Say("Hint: word " + (pos + 1) + " is placed");
        RefreshBoard();
    }

    private void CheckSentence()
    {
        if (finished || tokens.Length == 0)
        {
            return;
        }
        if (chosen.Count != tokens.Length)
        {
            Say("Fill every slot first");
            return;
        }
        bool correct = true;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (Norm(bankTexts[chosen[i]]) != Norm(tokens[i]))
            {
                correct = false;
                break;
            }
        }
        if (correct)
        {
            PhraseProgressStore.MarkLearned(language, topic, phraseIndex);
            Finish(true);
            return;
        }
        triesLeft--;
        RefreshBoard();
        if (triesLeft <= 0)
        {
            Finish(false);
        }
        else
        {
            Say("Not quite. Tap a word to change it");
        }
    }

    // ------------------------------------------------------------------ result

    private void Finish(bool win)
    {
        finished = true;
        won = win;
        RefreshBoard();
        if (resultPanel == null)
        {
            return;
        }
        bool tagalog = language == 2;
        bool cebuano = language == 0;
        if (resultTitle != null)
        {
            resultTitle.text = win ? (tagalog ? "Tama!" : "Husto!") : (cebuano ? "Hapit na!" : "Malapit na!");
        }
        if (resultSubtitle != null) resultSubtitle.text = win ? "You built the sentence" : "The sentence was";
        if (resultSentence != null) resultSentence.text = entry != null ? entry.phrase : string.Empty;
        if (resultEnglish != null) resultEnglish.text = entry != null ? entry.english : string.Empty;
        int seconds = Mathf.RoundToInt(Time.unscaledTime - startTime);
        if (statTime != null) statTime.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        if (statTries != null) statTries.text = (MaxTries - triesLeft + (win ? 1 : 0)) + " of " + MaxTries;
        if (statStreak != null)
        {
            int streak = Mathf.Max(1, StreakTracker.GetStreak());
            statStreak.text = streak == 1 ? "1 day" : streak + " days";
        }
        if (resultNextLabel != null)
        {
            int nextTopic, nextPhrase;
            bool hasNext = PhraseSession.TryGetNext(language, topic, phraseIndex, out nextTopic, out nextPhrase);
            resultNextLabel.text = win ? (hasNext ? "Next sentence" : "Back to topics") : "Try again";
        }
        resultPanel.SetActive(true);
    }

    private void ResultNext()
    {
        if (!finished)
        {
            return;
        }
        int nextTopic, nextPhrase;
        if (!won)
        {
            PhraseSession.OpenBuilder(language, topic, phraseIndex);
        }
        else if (PhraseSession.TryGetNext(language, topic, phraseIndex, out nextTopic, out nextPhrase))
        {
            PhraseSession.OpenBuilder(language, nextTopic, nextPhrase);
        }
        else
        {
            Leave();
        }
    }

    /// <summary>Tap to hear: plays the recording of the whole phrase.</summary>
    private void SpeakPhrase()
    {
        if (entry != null)
        {
            WordAudio.Play(language, entry.phrase);
        }
    }

    private void SeeInPhrasebook()
    {
        PhraseSession.OpenCards(language, topic, phraseIndex);
    }

    /// <summary>Back to the topics screen.</summary>
    public void Leave()
    {
        SceneManager.LoadScene(PhraseSession.TopicsScene);
    }
}
