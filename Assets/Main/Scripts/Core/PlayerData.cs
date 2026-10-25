using System;
using System.Collections.Generic;

/// <summary>
/// PlayerData
/// Plain serializable data model for one player's save data: identity plus per-game
/// level-completion progress. This is the schema persisted to disk by PlayerDatabase.
/// To track a new game's progress later, add one more int field here (following the
/// existing "*Progress" fields) and a matching property in PlayerDatabase.
///
/// The fields below the original three were added for the redesigned Profile screen (avatar, join date, solved counts per
/// language, best crossword level, learned words). An older player_data.json without them still loads: the missing fields
/// keep their defaults.
/// Language index everywhere: 0 = Cebuano, 1 = Ilonggo / Hiligaynon, 2 = Tagalog.
/// </summary>
[Serializable]
public class PlayerData
{
    public string playerNameOrId = "Player";

    public int wordleProgress = 0;
    public int crosswordProgress = 0;
    public int wordSearchProgress = 0;

    public int avatarIndex = 0;

    /// <summary>True when the player chose a picture from their computer (saved by AvatarLibrary.ImportCustom) instead of one of the built-in avatars.</summary>
    public bool useCustomAvatar = false;

    /// <summary>Day the profile was created, as yyyy-MM-dd (filled in on first load).</summary>
    public string joinedDate = string.Empty;

    public int[] wordleByLanguage = new int[3];
    public int[] crosswordByLanguage = new int[3];
    public int[] wordSearchByLanguage = new int[3];

    /// <summary>Highest crossword level (1-10) the player has finished.</summary>
    public int bestCrosswordLevel = 0;

    /// <summary>Every different word the player solved or met in a finished puzzle, saved as "languageIndex:WORD".</summary>
    public List<string> learnedWords = new List<string>();
}
