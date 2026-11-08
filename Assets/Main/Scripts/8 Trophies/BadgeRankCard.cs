using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BadgeRankCard
/// The two trophy parts of the Profile screen: the Badges card beside "Words learned" ("5/16", opens the Badges tab) and the chip at the top right ("#12 this week"
/// when the player is logged in and shown on the weekly board, otherwise "Leaderboard"; opens the Leaderboard tab). Added to the scene by TrophyBuilder.
/// </summary>
public class BadgeRankCard : MonoBehaviour
{
    [SerializeField] private TMP_Text badgesValue;
    [SerializeField] private Button badgesButton;
    [SerializeField] private TMP_Text rankLabel;
    [SerializeField] private Button rankButton;

    private const string ProfileScene = "2 Profile Icon";

    private void Start()
    {
        if (badgesButton != null) badgesButton.onClick.AddListener(delegate { TrophyRoomController.Open(2, ProfileScene); });
        if (rankButton != null) rankButton.onClick.AddListener(delegate { TrophyRoomController.Open(0, ProfileScene); });

        int earned = 0, total = 0;
        foreach (BadgeState state in AchievementTracker.CheckAll(false))
        {
            total++;
            if (state.Earned) earned++;
        }
        if (badgesValue != null) badgesValue.text = earned + "<size=40>/" + total + "</size>";
        if (rankLabel != null) rankLabel.text = "Leaderboard";

        if (CloudSave.SignedIn)
        {
            LeaderboardService.GetBoard("week", "all", -1, delegate (LeaderboardService.BoardState state, LeaderboardService.BoardRow[] rows)
            {
                if (this == null || rankLabel == null || state != LeaderboardService.BoardState.Ok)
                {
                    return;
                }
                foreach (LeaderboardService.BoardRow row in rows)
                {
                    if (row.is_me)
                    {
                        rankLabel.text = "#" + row.rank + " this week";
                    }
                }
            });
        }
    }
}
