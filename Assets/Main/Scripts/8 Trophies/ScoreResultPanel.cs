using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ScoreResultPanel
/// The small score card shown to the right of the result card of Wordle, Crossword and Word Search after a win. Report saves the result (ResultsStore), checks the
/// badges, and builds the card at run time inside the result window, so it needs no change in the 3 play scenes and survives a "Rebuild design". It lists how the
/// score was earned, says "New personal best!", and when the player is logged in it uploads the results and adds the weekly rank. Placement in the 1920x1080
/// layout: x 1532, y 230, 360 wide (the result cards end at x 1520 at most).
/// </summary>
public static class ScoreResultPanel
{
    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Line = new Color32(239, 231, 212, 255);
    private static readonly Color Red = new Color32(217, 96, 59, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Purple = new Color32(138, 64, 199, 255);

    private static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, bool bold, TextAlignmentOptions align, float x, float y, float w, float h)
    {
        TextMeshProUGUI t = TrophyFont.Apply(LibraryUiKit.Label(parent, text, size, color, bold ? FontStyles.Bold : FontStyles.Normal, align));
        LibraryUiKit.Place(t.rectTransform, x, y, w, h);
        return t;
    }

    /// <summary>Saves the win and shows its score. window is the full-screen result window of the play scene.</summary>
    public static void Report(PuzzleResult result, ScoreRules.Score score, GameObject window)
    {
        result.Score = score.Total;
        ResultsStore.Outcome outcome = ResultsStore.Record(result);
        AchievementTracker.CheckAll(true);
        if (window != null)
        {
            Build(score, outcome, window.transform);
        }
    }

    private static void Build(ScoreRules.Score score, ResultsStore.Outcome outcome, Transform window)
    {
        int rows = score.Lines.Count;
        float height = 150 + rows * 50 + (CloudSave.SignedIn ? 230 : 172);
        Image card = LibraryUiKit.Box(window, "score_card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 1532, 230, 360, height);

        Text(card.transform, "Score", 34, Ink, true, TextAlignmentOptions.Center, 0, 20, 360, 46);
        float y = 76;
        foreach (ScoreRules.Line line in score.Lines)
        {
            TextMeshProUGUI label = Text(card.transform, line.Label, 23, Ink, false, TextAlignmentOptions.MidlineLeft, 22, y, 240, 46);
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 23f;
            Text(card.transform, (line.Points >= 0 ? "+" : "-") + Mathf.Abs(line.Points), 25, line.Points >= 0 ? Ink : Red, true, TextAlignmentOptions.MidlineRight, 258, y, 80, 46);
            Image rule = LibraryUiKit.Box(card.transform, "rule", Line, false);
            LibraryUiKit.Place(rule.rectTransform, 22, y + 47, 316, 2);
            y += 50;
        }
        y += 8;
        Text(card.transform, "Total", 34, Ink, true, TextAlignmentOptions.MidlineLeft, 22, y, 160, 56);
        Text(card.transform, score.Total.ToString(), 44, Green, true, TextAlignmentOptions.MidlineRight, 178, y, 160, 56);
        y += 66;

        if (outcome.IsNewBest)
        {
            TrophyFont.Apply(LibraryUiKit.Pill(card.transform, "New personal best!", 22, y, 316, 50, new Color32(253, 240, 200, 255), new Color32(138, 100, 0, 255), 24, FontStyles.Bold));
        }
        else
        {
            Text(card.transform, "Your best: " + outcome.PreviousBest, 25, InkSoft, false, TextAlignmentOptions.Center, 22, y, 316, 50);
        }
        y += 58;

        Image rankPill = TrophyFont.Apply(LibraryUiKit.Pill(card.transform, "Saving...", 22, y, 316, 50, new Color32(243, 232, 251, 255), Purple, 24, FontStyles.Bold));
        TextMeshProUGUI rankLabel = rankPill.GetComponentInChildren<TextMeshProUGUI>();
        rankPill.gameObject.SetActive(CloudSave.SignedIn);
        if (CloudSave.SignedIn)
        {
            y += 58;
            LeaderboardService.RankAfterResult(delegate (int rank, int moved)
            {
                if (rankLabel == null)
                {
                    return;
                }
                if (rank <= 0)
                {
                    rankLabel.text = "Saved online";
                    return;
                }
                rankLabel.text = "#" + rank + " this week" + (moved > 0 ? " (up " + moved + ")" : string.Empty);
            });
        }

        Image open = TrophyFont.Apply(LibraryUiKit.Pill(card.transform, "Leaderboard", 22, y, 316, 62, Purple, Color.white, 28, FontStyles.Bold));
        LibraryUiKit.MakeButton(open, delegate { TrophyRoomController.Open(0, StartSceneOf(SceneManager.GetActiveScene().name)); });
    }

    /// <summary>The Back button of the Trophy screen returns to the start screen of the game just played.</summary>
    private static string StartSceneOf(string playScene)
    {
        if (playScene.StartsWith("Wordle"))
        {
            return WordleSession.StartScene;
        }
        return playScene.StartsWith("Crossword") ? CrosswordSession.StartScene : WordSearchSession.StartScene;
    }
}
