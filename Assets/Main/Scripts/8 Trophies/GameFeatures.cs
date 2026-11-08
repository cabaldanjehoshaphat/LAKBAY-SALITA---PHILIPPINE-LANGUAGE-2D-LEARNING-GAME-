/// <summary>
/// GameFeatures
/// Switches for parts of the game that are prepared but not released yet. SurvivalEnabled stays false until the Survival mode exists: while it is false the
/// Survival leaderboard tab, the Survival card on My records and the six Survival badges are hidden, and Survival data can never appear to players.
/// To release Survival: build the mode, call SurvivalRecords.RecordRun at the end of a run, and set SurvivalEnabled to true.
/// See "Survival materials" in the document Leaderboard and Achievement.docx.
/// </summary>
public static class GameFeatures
{
    public static bool SurvivalEnabled = false;
}
