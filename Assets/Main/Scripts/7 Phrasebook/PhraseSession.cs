using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PhraseSession
/// Static hand-over between the Phrasebook scenes ("Phrasebook - Topics", "Phrasebook - Cards", "Phrasebook - Builder"): which language, topic and
/// phrase (all 0-based) to show. The main menu's Phrasebook card (PhraseMenuButton) opens the topics screen, where the player picks the language
/// (remembered); the topics screen also sets topic and phrase, the cards and the
/// Sentence builder read them. Language index everywhere: 0 Cebuano, 1 Ilonggo, 2 Tagalog. Also holds the scene names and the helper that
/// finds the phrase after the current one.
/// </summary>
public static class PhraseSession
{
    public const string TopicsScene = "Phrasebook - Topics";
    public const string CardsScene = "Phrasebook - Cards";
    public const string BuilderScene = "Phrasebook - Builder";
    public const string MenuScene = "1 Main_menu";
    private const string LanguageKey = "phrasebook.language";

    public static int Language;
    public static int Topic;
    public static int Phrase;

    /// <summary>The language the player last picked in the Phrasebook (saved), used when the Phrasebook opens from the main menu.</summary>
    public static int SavedLanguage()
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey, 0), 0, 2);
    }

    /// <summary>Sets the Phrasebook language and remembers it.</summary>
    public static void SetLanguage(int language)
    {
        Language = Mathf.Clamp(language, 0, 2);
        PlayerPrefs.SetInt(LanguageKey, Language);
        PlayerPrefs.Save();
    }

    /// <summary>Opens the phrase cards of a topic, starting at one phrase.</summary>
    public static void OpenCards(int language, int topic, int phrase)
    {
        Language = language;
        Topic = topic;
        Phrase = phrase;
        SceneManager.LoadScene(CardsScene);
    }

    /// <summary>Opens the Sentence builder on one phrase.</summary>
    public static void OpenBuilder(int language, int topic, int phrase)
    {
        Language = language;
        Topic = topic;
        Phrase = phrase;
        SceneManager.LoadScene(BuilderScene);
    }

    /// <summary>The phrase after (topic, phrase): the next phrase of the topic, then the first phrase of the next topic. False after the last one.</summary>
    public static bool TryGetNext(int language, int topic, int phrase, out int nextTopic, out int nextPhrase)
    {
        nextTopic = topic;
        nextPhrase = phrase + 1;
        if (nextPhrase < PhraseData.PhraseCount(language, topic))
        {
            return true;
        }
        nextTopic = topic + 1;
        nextPhrase = 0;
        while (nextTopic < PhraseData.TopicCount(language))
        {
            if (PhraseData.PhraseCount(language, nextTopic) > 0)
            {
                return true;
            }
            nextTopic++;
        }
        return false;
    }
}

/// <summary>
/// PhraseProgressStore
/// Remembers which phrases the player has built correctly in the Sentence builder, in PlayerPrefs ("phrasebook.learned.v1", entries
/// "language:topic:phrase" joined with "|"). The topics screen shows "N of M learned" per topic from it and the hub shows the total.
/// </summary>
public static class PhraseProgressStore
{
    private const string LearnedKey = "phrasebook.learned.v1";

    private static string KeyFor(int language, int topic, int phrase)
    {
        return language + ":" + topic + ":" + phrase;
    }

    private static List<string> Load()
    {
        var list = new List<string>();
        string saved = PlayerPrefs.GetString(LearnedKey, string.Empty);
        foreach (string part in saved.Split('|'))
        {
            if (part.Length > 0)
            {
                list.Add(part);
            }
        }
        return list;
    }

    public static bool IsLearned(int language, int topic, int phrase)
    {
        return Load().Contains(KeyFor(language, topic, phrase));
    }

    /// <summary>How many of a topic's phrases are learned.</summary>
    public static int LearnedCount(int language, int topic, int phraseCount)
    {
        List<string> list = Load();
        int count = 0;
        for (int p = 0; p < phraseCount; p++)
        {
            if (list.Contains(KeyFor(language, topic, p)))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>How many phrases are learned in one language (all topics).</summary>
    public static int LearnedTotal(int language)
    {
        int total = 0;
        for (int t = 0; t < PhraseData.TopicCount(language); t++)
        {
            total += LearnedCount(language, t, PhraseData.PhraseCount(language, t));
        }
        return total;
    }

    public static void MarkLearned(int language, int topic, int phrase)
    {
        List<string> list = Load();
        string key = KeyFor(language, topic, phrase);
        if (!list.Contains(key))
        {
            list.Add(key);
            PlayerPrefs.SetString(LearnedKey, string.Join("|", list.ToArray()));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Forgets every learned phrase (used when the profile is reset).</summary>
    public static void Reset()
    {
        PlayerPrefs.DeleteKey(LearnedKey);
        PlayerPrefs.Save();
    }
}
