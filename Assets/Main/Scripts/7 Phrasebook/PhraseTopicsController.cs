using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PhraseTopicsController
/// Runs the Phrasebook topics screen (scene "Phrasebook - Topics"): a language selection at the top, like the tabs of the Library (Cebuano, Ilonggo,
/// Tagalog; the pick is remembered), and one card per everyday topic (Greetings,
/// Thanks and sorry, About me, Food and market, Directions, Numbers and time, Help and safety). Each card shows how many of its phrases are
/// learned ("2 of 5 learned", gold once one is learned), an example phrase, a Learn button (opens the phrase cards, PhraseCardsController) and a
/// Build button (opens the Sentence builder on the first phrase not learned yet). "Random phrase" builds a random phrase that is not learned yet.
/// The back button and Esc return to the main menu. References are assigned in the scene by PhrasebookSceneBuilder.
/// </summary>
public class PhraseTopicsController : MonoBehaviour
{
    [Header("Top bar")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button randomButton;
    [Header("Language selection (one chip per language)")]
    [SerializeField] private Button[] languageButtons;
    [SerializeField] private Image[] languageImages;
    [SerializeField] private TMP_Text[] languageLabels;

    [Header("Topic cards (one entry per topic)")]
    [SerializeField] private TMP_Text[] topicNames;
    [SerializeField] private TMP_Text[] learnedLabels;
    [SerializeField] private Image[] learnedPills;
    [SerializeField] private TMP_Text[] exampleLabels;
    [SerializeField] private Button[] learnButtons;
    [SerializeField] private Button[] buildButtons;

    [Header("Colours")]
    [SerializeField] private Color learnedTagColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color openTagColor = new Color32(239, 231, 212, 255);
    [SerializeField] private Color chipOnColor = new Color32(59, 42, 26, 255);
    [SerializeField] private Color chipOffColor = new Color32(253, 248, 238, 255);
    [SerializeField] private Color chipOnTextColor = new Color32(253, 248, 238, 255);
    [SerializeField] private Color chipOffTextColor = new Color32(59, 42, 26, 255);

    private int language;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(BackToMenu);
        if (randomButton != null) randomButton.onClick.AddListener(PlayRandom);
        for (int i = 0; languageButtons != null && i < languageButtons.Length; i++)
        {
            int chosen = i;
            if (languageButtons[i] != null) languageButtons[i].onClick.AddListener(delegate { ChooseLanguage(chosen); });
        }
        int count = learnButtons != null ? learnButtons.Length : 0;
        for (int i = 0; i < count; i++)
        {
            int topic = i;
            if (learnButtons[i] != null) learnButtons[i].onClick.AddListener(delegate { Learn(topic); });
            if (buildButtons != null && i < buildButtons.Length && buildButtons[i] != null) buildButtons[i].onClick.AddListener(delegate { Build(topic); });
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
            BackToMenu();
        }
    }

    /// <summary>Re-reads the saved progress: tags, example phrases and the language chip.</summary>
    [ContextMenu("Refresh preview")]
    public void Refresh()
    {
        language = Mathf.Clamp(PhraseSession.Language, 0, 2);
        for (int i = 0; languageImages != null && i < languageImages.Length; i++)
        {
            bool on = i == language;
            if (languageImages[i] != null) languageImages[i].color = on ? chipOnColor : chipOffColor;
            if (languageLabels != null && i < languageLabels.Length && languageLabels[i] != null) languageLabels[i].color = on ? chipOnTextColor : chipOffTextColor;
        }

        int count = learnButtons != null ? learnButtons.Length : 0;
        for (int t = 0; t < count; t++)
        {
            PhraseTopicDef topic = PhraseData.Topic(language, t);
            int phrases = topic != null ? topic.phrases.Count : 0;
            int learned = Application.isPlaying ? PhraseProgressStore.LearnedCount(language, t, phrases) : 0;
            if (topicNames != null && t < topicNames.Length && topicNames[t] != null && topic != null) topicNames[t].text = topic.name;
            if (learnedLabels != null && t < learnedLabels.Length && learnedLabels[t] != null) learnedLabels[t].text = learned + " of " + phrases + " learned";
            if (learnedPills != null && t < learnedPills.Length && learnedPills[t] != null) learnedPills[t].color = learned > 0 ? learnedTagColor : openTagColor;
            if (exampleLabels != null && t < exampleLabels.Length && exampleLabels[t] != null)
            {
                exampleLabels[t].text = phrases > 0 ? topic.phrases[0].english : string.Empty;
            }
        }
    }

    /// <summary>A language chip was pressed: shows that language's topics and remembers the choice.</summary>
    private void ChooseLanguage(int chosen)
    {
        PhraseSession.SetLanguage(chosen);
        Refresh();
    }

    private void Learn(int topic)
    {
        PhraseSession.OpenCards(language, topic, 0);
    }

    private void Build(int topic)
    {
        int phrases = PhraseData.PhraseCount(language, topic);
        int start = 0;
        for (int p = 0; p < phrases; p++)
        {
            if (!PhraseProgressStore.IsLearned(language, topic, p))
            {
                start = p;
                break;
            }
        }
        PhraseSession.OpenBuilder(language, topic, start);
    }

    /// <summary>Builds a random phrase that is not learned yet (any phrase once everything is learned).</summary>
    private void PlayRandom()
    {
        var open = new System.Collections.Generic.List<int[]>();
        var all = new System.Collections.Generic.List<int[]>();
        for (int t = 0; t < PhraseData.TopicCount(language); t++)
        {
            for (int p = 0; p < PhraseData.PhraseCount(language, t); p++)
            {
                all.Add(new[] { t, p });
                if (!PhraseProgressStore.IsLearned(language, t, p))
                {
                    open.Add(new[] { t, p });
                }
            }
        }
        System.Collections.Generic.List<int[]> pool = open.Count > 0 ? open : all;
        if (pool.Count == 0)
        {
            return;
        }
        int[] pick = pool[Random.Range(0, pool.Count)];
        PhraseSession.OpenBuilder(language, pick[0], pick[1]);
    }

    /// <summary>Returns to the main menu.</summary>
    public void BackToMenu()
    {
        SceneManager.LoadScene(PhraseSession.MenuScene);
    }
}
