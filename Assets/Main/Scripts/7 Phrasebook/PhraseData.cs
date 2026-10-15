using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PhraseData
/// The Phrasebook's data model and loader. The phrases live in one JSON file per language (Assets/Main/Data/Phrasebook/phrases_&lt;language&gt;.json),
/// found through LevelDataRegistry (Phrasebook Files): 3 languages (0 Cebuano, 1 Ilonggo, 2 Tagalog) x 7 topics x a few phrases. A phrase has the
/// English line, the phrase in the language and optional word-by-word meanings (gloss: empty, or one entry per word). The Phrasebook screens and
/// the Sentence builder split a phrase into words at its spaces (Tokens). The file is read once and cached. The phrases are placeholders until
/// native speakers check them.
/// </summary>
[Serializable]
public class PhraseEntry
{
    public string english;
    public string phrase;
    public List<string> gloss = new List<string>();
}

[Serializable]
public class PhraseTopicDef
{
    public string name;
    public List<PhraseEntry> phrases = new List<PhraseEntry>();
}

[Serializable]
public class PhraseLanguageDef
{
    public string name;
    public List<PhraseTopicDef> topics = new List<PhraseTopicDef>();
}

public static class PhraseData
{
    private static PhraseLanguageDef[] _languages;

    /// <summary>Language names in the order of the language index (0 Cebuano, 1 Ilonggo, 2 Tagalog).</summary>
    public static readonly string[] LanguageNames = { "Cebuano", "Ilonggo", "Tagalog" };

    /// <summary>Forgets the cached file so the next call reads the JSON again.</summary>
    public static void Reload()
    {
        _languages = null;
    }

    public static PhraseLanguageDef Language(int language)
    {
        if (_languages == null)
        {
            LevelDataRegistry registry = LevelDataRegistry.Instance;
            if (registry == null)
            {
                return null;
            }
            _languages = LevelDataRegistry.Parse<PhraseLanguageDef>(registry.phrasebookFiles, "Phrasebook");
        }
        return language >= 0 && language < _languages.Length ? _languages[language] : null;
    }

    public static int TopicCount(int language)
    {
        PhraseLanguageDef l = Language(language);
        return l != null ? l.topics.Count : 0;
    }

    public static PhraseTopicDef Topic(int language, int topic)
    {
        PhraseLanguageDef l = Language(language);
        return l != null && topic >= 0 && topic < l.topics.Count ? l.topics[topic] : null;
    }

    public static int PhraseCount(int language, int topic)
    {
        PhraseTopicDef t = Topic(language, topic);
        return t != null ? t.phrases.Count : 0;
    }

    public static PhraseEntry Phrase(int language, int topic, int phrase)
    {
        PhraseTopicDef t = Topic(language, topic);
        return t != null && phrase >= 0 && phrase < t.phrases.Count ? t.phrases[phrase] : null;
    }

    /// <summary>How many phrases a language has in total (all topics).</summary>
    public static int TotalPhrases(int language)
    {
        int total = 0;
        for (int t = 0; t < TopicCount(language); t++)
        {
            total += PhraseCount(language, t);
        }
        return total;
    }

    /// <summary>The words of a phrase, split at the spaces; punctuation stays on the word it follows.</summary>
    public static string[] Tokens(string phrase)
    {
        if (string.IsNullOrEmpty(phrase))
        {
            return new string[0];
        }
        return phrase.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
