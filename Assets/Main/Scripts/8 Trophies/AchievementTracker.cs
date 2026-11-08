using System;
using System.Collections.Generic;
using UnityEngine;

public enum BadgeGroup
{
    Games,
    Words,
    Streaks,
    Online,
    Survival
}

/// <summary>One badge of the Achievements screen. Tiers holds the counts for bronze, silver and gold; a badge with one goal has a single value.</summary>
public class BadgeDef
{
    public string Id;
    public string Name;
    public string Description;
    public string Glyph;
    public Color Color;
    public BadgeGroup Group;
    public int[] Tiers;
    public Func<int> Value;
    public bool Manual;
}

/// <summary>A badge with the player's state: EarnedTier is -1 while locked. Value and Target are the progress to show.</summary>
public class BadgeState
{
    public BadgeDef Def;
    public int EarnedTier = -1;
    public int Value;
    public int Target;

    public bool Earned { get { return EarnedTier >= 0; } }
    public bool HasNextTier { get { return Def.Tiers.Length > 1 && EarnedTier >= 0 && EarnedTier < Def.Tiers.Length - 1; } }
    public string TierName
    {
        get
        {
            if (Def.Tiers.Length <= 1 || EarnedTier < 0)
            {
                return string.Empty;
            }
            return new[] { "Bronze", "Silver", "Gold" }[Mathf.Clamp(EarnedTier, 0, 2)];
        }
    }
}

/// <summary>
/// AchievementTracker
/// The badges of Lakbay Salita and the check that awards them. The 15 badges are worked out from what the game saves (solved puzzles, words learned, phrases,
/// favorites, the best streak and ResultsStore), so they also work offline and after progress is merged at login. Weekly Top 10 is given by
/// the online boards (AwardOnline). The six Survival badges are ready, and stay hidden until GameFeatures.SurvivalEnabled is true.
/// Earned badges are saved in SaveStore ("achievements.earned.v1", entries "id:tier" joined with "|"); CloudMerge keeps everything from both copies. A badge once
/// earned is never taken away by the check. CheckAll announces new badges through TrophyOverlay; the very first check is silent, so that players who already have
/// progress are not flooded with messages when they update the game.
/// </summary>
public static class AchievementTracker
{
    public const string SaveKey = "achievements.earned.v1";
    private const string ReadyKey = "achievements.ready";

    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Blue = new Color32(56, 138, 221, 255);
    private static readonly Color Red = new Color32(217, 96, 59, 255);
    private static readonly Color Teal = new Color32(28, 158, 116, 255);
    private static readonly Color Purple = new Color32(138, 64, 199, 255);
    private static readonly Color Orange = new Color32(232, 116, 28, 255);
    private static readonly Color Yellow = new Color32(232, 185, 34, 255);
    private static readonly Color Navy = new Color32(27, 58, 140, 255);

    private const string MasterId = "lakbay_master";
    public const string WeeklyTop10Id = "weekly_top10";

    private static List<BadgeDef> _all;

    private static BadgeDef Def(string id, string name, string desc, string glyph, Color color, BadgeGroup group, int[] tiers, Func<int> value)
    {
        return new BadgeDef { Id = id, Name = name, Description = desc, Glyph = glyph, Color = color, Group = group, Tiers = tiers, Value = value };
    }

    private static int Sum(Func<int, int> perLanguage)
    {
        return perLanguage(0) + perLanguage(1) + perLanguage(2);
    }

    private static int WordleTotal() { return Sum(WordleProgressStore.SolvedTotal); }
    private static int CrosswordTotal() { return Sum(CrosswordProgressStore.SolvedTotal); }
    private static int WordSearchTotal() { return Sum(WordSearchProgressStore.SolvedTotal); }

    private static int LanguagesPlayed()
    {
        int count = 0;
        for (int l = 0; l < 3; l++)
        {
            if (WordleProgressStore.SolvedTotal(l) + CrosswordProgressStore.SolvedTotal(l) + WordSearchProgressStore.SolvedTotal(l) > 0)
            {
                count++;
            }
        }
        return count;
    }

    private static int BestTopicProgress()
    {
        int best = 0;
        for (int l = 0; l < 3; l++)
        {
            for (int c = 0; c < WordleData.CategoryCount(l); c++)
            {
                best = Mathf.Max(best, WordleProgressStore.SolvedCount(l, c, WordleData.LevelCount(l, c)));
            }
        }
        return best;
    }

    private static int AnyRecord(Predicate<PuzzleRecord> test)
    {
        foreach (PuzzleRecord r in ResultsStore.AllRecords())
        {
            if (test(r))
            {
                return 1;
            }
        }
        return 0;
    }

    private static int WordsLearned()
    {
        return PlayerDatabase.Instance != null ? PlayerDatabase.Instance.WordsLearned : 0;
    }

    /// <summary>Every badge, Survival included (use Visible for the ones to show).</summary>
    public static List<BadgeDef> All
    {
        get
        {
            if (_all != null)
            {
                return _all;
            }
            int[] once = { 1 };
            _all = new List<BadgeDef>
            {
                Def("first_steps", "First Steps", "Finish your first puzzle", "1", Green, BadgeGroup.Games, once, () => Mathf.Min(1, WordleTotal() + CrosswordTotal() + WordSearchTotal())),
                Def("wordle_star", "Wordle Star", "Solve 5 · 42 · 126 Wordle levels", "W", Green, BadgeGroup.Games, new[] { 5, 42, 126 }, WordleTotal),
                Def("crossword_builder", "Crossword Builder", "Finish 5 · 21 · 63 crosswords", "C", Blue, BadgeGroup.Games, new[] { 5, 21, 63 }, CrosswordTotal),
                Def("word_hunter", "Word Hunter", "Finish 5 · 21 · 63 word searches", "S", Red, BadgeGroup.Games, new[] { 5, 21, 63 }, WordSearchTotal),
                Def("three_languages", "Three Languages", "Solve a puzzle in Cebuano, Ilonggo and Tagalog", "3", Teal, BadgeGroup.Games, new[] { 3 }, LanguagesPlayed),
                Def("word_collector", "Word Collector", "Learn 25 · 150 · 400 words", "Aa", Purple, BadgeGroup.Words, new[] { 25, 150, 400 }, WordsLearned),
                Def("phrase_speaker", "Phrase Speaker", "Learn 10 · 40 · 83 phrases", "Hi", Teal, BadgeGroup.Words, new[] { 10, 40, 83 }, () => Sum(PhraseProgressStore.LearnedTotal)),
                Def("bookworm", "Bookworm", "Save 10 words to your favorites", "10", Purple, BadgeGroup.Words, new[] { 10 }, LibraryFavoritesStore.Count),
                Def("daily_traveler", "Daily Traveler", "Keep a 3 · 7 · 30 day streak", "D", Orange, BadgeGroup.Streaks, new[] { 3, 7, 30 }, StreakTracker.GetBestStreak),
                Def("topic_champion", "Topic Champion", "Finish all 6 levels of one Wordle topic", "6", Green, BadgeGroup.Games, new[] { 6 }, BestTopicProgress),
                Def("no_help_needed", "No Help Needed", "Solve a puzzle without a hint", "!", Yellow, BadgeGroup.Games, once, () => AnyRecord(r => r.Hints == 0 && r.Reveals == 0)),
                Def("first_try", "First Try!", "Solve a Wordle on the first guess", "1st", Green, BadgeGroup.Games, once, () => AnyRecord(r => r.FirstTry)),
                Def("speed_runner", "Speed Runner", "Finish a small crossword in under 1 minute", "GO", Blue, BadgeGroup.Games, once,
                    () => AnyRecord(r => r.Game == ScoreRules.GameCrossword && r.Level == 0 && r.Fastest > 0 && r.Fastest < 60)),
                new BadgeDef { Id = WeeklyTop10Id, Name = "Weekly Top 10", Description = "Reach the top 10 of the online weekly board", Glyph = "T10", Color = Purple, Group = BadgeGroup.Online, Tiers = once, Manual = true },
                Def(MasterId, "Lakbay Master", "Earn every other badge", "L", Yellow, BadgeGroup.Games, new[] { 1 }, () => 0),

                // prepared for the Survival mode: hidden until GameFeatures.SurvivalEnabled is true
                Def("first_voyage", "First Voyage", "Finish a Survival run", "V", Navy, BadgeGroup.Survival, once, () => Mathf.Min(1, SurvivalRecords.Load().Runs)),
                Def("reached_visayas", "Reached the Visayas", "Reach round 6 in Survival", "VS", Teal, BadgeGroup.Survival, once, () => SurvivalRecords.Load().BestStage >= 1 ? 1 : 0),
                Def("reached_mindanao", "Reached Mindanao", "Reach round 16 in Survival", "MN", Orange, BadgeGroup.Survival, once, () => SurvivalRecords.Load().BestStage >= 2 ? 1 : 0),
                Def("open_sea", "Open Sea", "Reach round 31 in Survival", "31", Blue, BadgeGroup.Survival, once, () => SurvivalRecords.Load().BestStage >= 3 ? 1 : 0),
                Def("second_chance", "Second Chance", "Answer a missed word correctly when it returns", "2x", Red, BadgeGroup.Survival, once, () => SurvivalRecords.Load().SecondChance ? 1 : 0),
                Def("three_language_sailor", "Three-Language Sailor", "Finish a Journey run past Mindanao", "3L", Purple, BadgeGroup.Survival, once, () => SurvivalRecords.Load().JourneyPastMindanao ? 1 : 0),
            };
            return _all;
        }
    }

    private static bool IsVisible(BadgeDef def)
    {
        return def.Group != BadgeGroup.Survival || GameFeatures.SurvivalEnabled;
    }

    private static Dictionary<string, int> LoadEarned()
    {
        var map = new Dictionary<string, int>();
        foreach (string part in SaveStore.GetString(SaveKey, string.Empty).Split('|'))
        {
            int colon = part.LastIndexOf(':');
            int tier;
            if (colon > 0 && int.TryParse(part.Substring(colon + 1), out tier))
            {
                string id = part.Substring(0, colon);
                int old;
                if (!map.TryGetValue(id, out old) || tier > old)
                {
                    map[id] = tier;
                }
            }
        }
        return map;
    }

    private static void SaveEarned(Dictionary<string, int> map)
    {
        var parts = new List<string>();
        foreach (KeyValuePair<string, int> pair in map)
        {
            parts.Add(pair.Key + ":" + pair.Value);
        }
        parts.Sort(StringComparer.Ordinal);
        SaveStore.SetString(SaveKey, string.Join("|", parts.ToArray()));
        SaveStore.Save();
    }

    private static int TierFor(BadgeDef def, int value)
    {
        int tier = -1;
        for (int i = 0; i < def.Tiers.Length; i++)
        {
            if (value >= def.Tiers[i])
            {
                tier = i;
            }
        }
        return tier;
    }

    /// <summary>Works out the badges and awards the new ones. New badges are announced unless this is the first check ever or announce is false.</summary>
    public static List<BadgeState> CheckAll(bool announce)
    {
        bool first = !SaveStore.HasKey(ReadyKey);
        Dictionary<string, int> earned = LoadEarned();
        var fresh = new List<BadgeState>();
        var states = new List<BadgeState>();
        BadgeState master = null;

        foreach (BadgeDef def in All)
        {
            if (!IsVisible(def))
            {
                continue;
            }
            var state = new BadgeState { Def = def };
            int saved;
            if (earned.TryGetValue(def.Id, out saved))
            {
                state.EarnedTier = saved;
            }
            if (def.Id == MasterId)
            {
                master = state;
                states.Add(state);
                continue;
            }
            state.Value = def.Manual ? (state.Earned ? 1 : 0) : def.Value();
            int tier = def.Manual ? state.EarnedTier : TierFor(def, state.Value);
            if (tier > state.EarnedTier)
            {
                state.EarnedTier = tier;
                earned[def.Id] = tier;
                fresh.Add(state);
            }
            int next = Mathf.Clamp(state.EarnedTier + 1, 0, def.Tiers.Length - 1);
            state.Target = def.Tiers[next];
            states.Add(state);
        }

        if (master != null)
        {
            int others = states.Count - 1;
            int done = 0;
            foreach (BadgeState s in states)
            {
                if (s != master && s.Earned)
                {
                    done++;
                }
            }
            master.Value = done;
            master.Target = others;
            if (done >= others && !master.Earned)
            {
                master.EarnedTier = 0;
                earned[MasterId] = 0;
                fresh.Add(master);
            }
        }

        if (fresh.Count > 0)
        {
            SaveEarned(earned);
        }
        if (first)
        {
            SaveStore.SetInt(ReadyKey, 1);
            SaveStore.Save();
        }
        else if (announce && fresh.Count > 0)
        {
            TrophyOverlay.Announce(fresh);
        }
        return states;
    }

    /// <summary>Gives a badge that only the online boards can decide (Weekly Top 10).</summary>
    public static void AwardOnline(string id)
    {
        Dictionary<string, int> earned = LoadEarned();
        if (earned.ContainsKey(id))
        {
            return;
        }
        earned[id] = 0;
        SaveEarned(earned);
        foreach (BadgeState state in CheckAll(false))
        {
            if (state.Def.Id == id)
            {
                TrophyOverlay.Announce(new List<BadgeState> { state });
            }
        }
    }

    public static void Reset()
    {
        SaveStore.DeleteKey(SaveKey);
        SaveStore.Save();
    }
}
