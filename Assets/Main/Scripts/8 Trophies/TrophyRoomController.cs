using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// TrophyRoomController
/// Runs the Trophy screen (scene "6 Trophies"), which has three tabs:
///  - Leaderboard: the online boards (This week, All time), filtered by game and language, with a podium for the top three and the player's own row always shown.
///    It needs the database functions of Documentation/Supabase/Leaderboard_Setup.sql; without them the screen explains what is missing. A logged-in player's results
///    are sent and can appear on the board. A guest sees the same board view-only: nothing is sent, and a "Log in" button leads to the Profile screen.
///    Every logged-in player appears automatically, because LeaderboardService publishes the name and avatar whenever results are sent. Only the name and a built-in
///    avatar are shown.
///    While the rows are on the screen they are asked for again every 30 seconds and redrawn only if something changed, keeping the player's place in the list.
///  - My records: works offline. Points this week against the best week, the last six weeks by the Monday each week starts, the best score and fastest time of each
///    game, and a "Beat your best" suggestion.
///  - Badges: the achievements, earned in colour and locked in gray with their progress.
/// The top bar is built in the scene by TrophyBuilder; the content of the tabs is built here, because it depends on the saved data. Open(tab, returnScene) opens the
/// screen on a tab and remembers where the back arrow returns to. The Survival tab, card and badges appear only when GameFeatures.SurvivalEnabled is true.
/// </summary>
public class TrophyRoomController : MonoBehaviour
{
    public const string SceneName = "6 Trophies";

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(122, 106, 88, 255);
    private static readonly Color Rule = new Color32(239, 231, 212, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Blue = new Color32(56, 138, 221, 255);
    private static readonly Color Red = new Color32(217, 96, 59, 255);
    private static readonly Color Purple = new Color32(138, 64, 199, 255);
    private static readonly Color PurpleSoft = new Color32(205, 184, 230, 255);
    private static readonly Color PurpleTint = new Color32(243, 232, 251, 255);
    private static readonly Color Orange = new Color32(232, 116, 28, 255);
    private static readonly Color Navy = new Color32(27, 58, 140, 255);
    private static readonly Color Gold = new Color32(232, 185, 34, 255);
    private static readonly Color Silver = new Color32(174, 180, 190, 255);
    private static readonly Color Bronze = new Color32(201, 128, 63, 255);
    private static readonly Color LockedRing = new Color32(216, 210, 198, 255);
    private static readonly Color LockedFace = new Color32(201, 195, 184, 255);
    private static readonly Color LockedCard = new Color32(244, 239, 229, 255);
    private static readonly Color Sand = new Color32(250, 242, 222, 255);
    private static readonly Color Peach = new Color32(251, 226, 196, 255);

    private static readonly string[] Titles = { "Leaderboard", "My records", "Achievements" };

    private static int _startTab;
    private static string _returnScene = "1 Main_menu";

    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Image statusDot;
    [SerializeField] private RectTransform[] tabRoots;
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private Image[] tabImages;
    [SerializeField] private TMP_Text[] tabLabels;

    private int _tab;
    private int _request;
    private int _game;
    private int _language = -1;
    private int _period;
    private int _badgeFilter;
    private bool _offline;
    private LeaderboardService.PlayerCard _card;
    private RectTransform _left, _right;

    // automatic refresh: while the online rows are on the screen they are asked for again every 30 seconds, and redrawn only if something changed
    private const float AutoRefreshSeconds = 30f;
    private float _nextRefresh;
    private bool _boardShowing;
    private bool _refreshing;
    private string _boardSignature = string.Empty;
    private ScrollRect _rowsScroll;

    /// <summary>Opens the screen. tab: 0 Leaderboard, 1 My records, 2 Badges.</summary>
    public static void Open(int tab, string returnScene)
    {
        _startTab = Mathf.Clamp(tab, 0, 2);
        _returnScene = string.IsNullOrEmpty(returnScene) ? "1 Main_menu" : returnScene;
        SceneManager.LoadScene(SceneName);
    }

    private void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(GoBack);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.AddListener(delegate { ShowTab(index); });
        }
        ShowTab(_startTab);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoBack();
        }
        if (_tab == 0 && _boardShowing && !_refreshing && Time.unscaledTime >= _nextRefresh)
        {
            RefreshBoardQuietly();
        }
    }

    private void OnDestroy()
    {
        _request++;   // an answer that arrives after the screen has closed is ignored
    }

    private void GoBack()
    {
        SceneManager.LoadScene(_returnScene);
    }

    // ------------------------------------------------------------------ small helpers

    private static void Clear(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, bool bold, TextAlignmentOptions align, float x, float y, float w, float h)
    {
        TextMeshProUGUI t = TrophyFont.Apply(LibraryUiKit.Label(parent, text, size, color, bold ? FontStyles.Bold : FontStyles.Normal, align));
        LibraryUiKit.Place(t.rectTransform, x, y, w, h);
        return t;
    }

    private static Image Card(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        Image card = LibraryUiKit.Box(parent, name, color, true);
        LibraryUiKit.Place(card.rectTransform, x, y, w, h);
        return card;
    }

    private static Image Chip(Transform parent, string text, float x, float y, float w, float h, bool selected, Color selectedColor, float size, UnityAction onClick)
    {
        Image chip = TrophyFont.Apply(LibraryUiKit.Pill(parent, text, x, y, w, h, selected ? selectedColor : Cream, selected ? Color.white : Ink, size, selected ? FontStyles.Bold : FontStyles.Normal));
        if (onClick != null)
        {
            LibraryUiKit.MakeButton(chip, onClick);
        }
        return chip;
    }

    private static void Avatar(Transform parent, int index, float x, float y, float size, Color ring)
    {
        Image frame = LibraryUiKit.Icon(parent, "avatar", LibraryUiKit.Circle, ring);
        LibraryUiKit.Place(frame.rectTransform, x, y, size, size);
        float border = ring == Cream ? 0f : Mathf.Round(size * 0.05f);
        Image mask = LibraryUiKit.Icon(frame.transform, "mask", LibraryUiKit.Circle, Color.white);
        LibraryUiKit.Place(mask.rectTransform, border, border, size - 2f * border, size - 2f * border);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Sprite sprite = AvatarLibrary.Get(index);
        if (sprite != null)
        {
            Image picture = LibraryUiKit.Icon(mask.transform, "picture", sprite, Color.white);
            LibraryUiKit.Fill(picture.rectTransform);
        }
    }

    private static void GameIcon(Transform parent, string game, float x, float y, float size)
    {
        Color color = game == ScoreRules.GameWordle ? Green : game == ScoreRules.GameCrossword ? Blue : game == ScoreRules.GameWordSearch ? Red : Navy;
        string letter = game == ScoreRules.GameWordle ? "W" : game == ScoreRules.GameCrossword ? "C" : game == ScoreRules.GameWordSearch ? "S" : "B";
        Image icon = Card(parent, "game_icon", x, y, size, size, color);
        Text(icon.transform, letter, size * 0.58f, Color.white, true, TextAlignmentOptions.Center, 0, 0, size, size);
    }

    private void SetStatus(string text, Color dot)
    {
        if (statusLabel != null) statusLabel.text = text;
        if (statusDot != null) statusDot.color = dot;
    }

    private void RefreshStatus()
    {
        if (_tab == 2)
        {
            return;
        }
        if (CloudSave.SignedIn && !_offline)
        {
            int days = ResultsStore.DaysLeftInWeek;
            SetStatus("<b>Online</b>  · Week ends in " + days + (days == 1 ? " day" : " days"), Green);
        }
        else if (_tab == 0 && !_offline)
        {
            SetStatus("<b>Guest</b>  · view only", Green);
        }
        else
        {
            SetStatus("<b>Offline</b>  · no internet needed", Orange);
        }
    }

    // ------------------------------------------------------------------ tabs

    private void ShowTab(int tab)
    {
        _tab = tab;
        _request++;
        if (titleLabel != null) titleLabel.text = Titles[tab];
        for (int i = 0; i < tabRoots.Length; i++)
        {
            tabRoots[i].gameObject.SetActive(i == tab);
            bool on = i == tab;
            tabImages[i].color = on ? Purple : Cream;
            tabLabels[i].color = on ? Color.white : Ink;
            tabLabels[i].fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
        }
        RefreshStatus();
        if (tab == 0) BuildLeaderboard();
        else if (tab == 1) BuildRecords();
        else BuildBadges();
    }

    // ------------------------------------------------------------------ leaderboard

    private string GameId
    {
        get
        {
            switch (_game)
            {
                case 1: return ScoreRules.GameWordle;
                case 2: return ScoreRules.GameCrossword;
                case 3: return ScoreRules.GameWordSearch;
                case 4: return ScoreRules.GameSurvival;
            }
            return "all";
        }
    }

    private void BuildLeaderboard()
    {
        Transform root = tabRoots[0];
        Clear(root);

        string[] games = { "All games", "Wordle", "Crossword", "Word Search", "Survival" };
        float[] gameWidths = { 180, 143, 190, 215, 160 };
        int gameCount = GameFeatures.SurvivalEnabled ? 5 : 4;
        float x = 80;
        for (int i = 0; i < gameCount; i++)
        {
            int index = i;
            Chip(root, games[i], x, 180, gameWidths[i], 64, _game == i, Purple, 28, delegate { _game = index; BuildLeaderboard(); });
            x += gameWidths[i] + 16;
        }

        string[] languages = { "All languages", "Cebuano", "Ilonggo", "Tagalog" };
        float[] languageWidths = { 200, 152, 136, 142 };
        x = 1040;
        for (int i = 0; i < 4; i++)
        {
            int value = i - 1;
            Chip(root, languages[i], x, 184, languageWidths[i], 56, _language == value, Ink, 25, delegate { _language = value; BuildLeaderboard(); });
            x += languageWidths[i] + 12;
        }

        Card(root, "period_back", 80, 268, 337, 64, Cream);
        string[] periods = { "This week", "All time" };
        float[] periodWidths = { 174, 143 };
        x = 86;
        for (int i = 0; i < periods.Length; i++)
        {
            int index = i;
            Chip(root, periods[i], x, 274, periodWidths[i], 52, _period == i, Green, 25, delegate { _period = index; BuildLeaderboard(); });
            x += periodWidths[i] + 8;
        }

        _left = Card(root, "podium_card", 80, 370, 620, 660, Cream).rectTransform;
        _right = Card(root, "list_card", 740, 370, 1100, 660, Cream).rectTransform;
        LoadBoard();
    }

    private void Message(string title, string body, string button, UnityAction action)
    {
        _boardShowing = false;
        Clear(_right);
        Clear(_left);
        Text(_left, "Top players", 30, Ink, true, TextAlignmentOptions.Center, 0, 26, 620, 44);
        var trophy = Resources.Load<Sprite>("Trophies/trophy");
        if (trophy != null)
        {
            Image icon = LibraryUiKit.Icon(_left, "trophy", trophy, Peach);
            LibraryUiKit.Place(icon.rectTransform, 190, 200, 240, 240);
        }
        Text(_right, title, 42, Ink, true, TextAlignmentOptions.Center, 60, 180, 980, 60);
        TextMeshProUGUI text = Text(_right, body, 30, InkSoft, false, TextAlignmentOptions.Top, 120, 254, 860, 130);
        text.enableWordWrapping = true;
        if (button != null)
        {
            Chip(_right, button, 350, 410, 400, 84, true, Purple, 34, action);
        }
    }

    private void Fail(LeaderboardService.BoardState state)
    {
        switch (state)
        {
            case LeaderboardService.BoardState.NoInternet:
                _offline = true;
                RefreshStatus();
                Message("No internet connection", "Your scores are saved on this computer and will be sent when you are back online. My records still works.", "Open My records", delegate { ShowTab(1); });
                break;
            case LeaderboardService.BoardState.NotSetUp:
                Message("Online boards are not switched on yet", "The online leaderboard has not been set up on the server. My records and Badges work now.", "Open My records", delegate { ShowTab(1); });
                break;
            case LeaderboardService.BoardState.NotSignedIn:
                ShowLoginMessage();
                break;
            default:
                Message("Something went wrong", "The board could not be loaded. Please try again.", "Try again", BuildLeaderboard);
                break;
        }
    }

    private void ShowLoginMessage()
    {
        Message("Log in to see the online boards", "Log in with Gmail on the Profile screen. Your scores are saved on this computer until then.", "Go to Profile", GoToProfile);
    }

    private static void GoToProfile()
    {
        SceneManager.LoadScene("2 Profile Icon");
    }

    private string Scope
    {
        get { return _period == 1 ? "all" : "week"; }
    }

    private void LoadBoard()
    {
        int request = ++_request;
        if (!CloudSave.SignedIn)
        {
            LoadGuestBoard(request);
            return;
        }
        Message("Loading...", "", null, null);
        LeaderboardService.SubmitAll(delegate (LeaderboardService.BoardState submitted)
        {
            if (request != _request) return;
            if (submitted != LeaderboardService.BoardState.Ok)
            {
                Fail(submitted);
                return;
            }
            _offline = false;
            RefreshStatus();
            LeaderboardService.GetMyCard(delegate (LeaderboardService.BoardState state, LeaderboardService.PlayerCard card)
            {
                if (request != _request) return;
                if (state != LeaderboardService.BoardState.Ok)
                {
                    Fail(state);
                    return;
                }
                _card = card;
                FetchRows(request);
            });
        });
    }

    /// <summary>A guest only reads the public board: no results are sent. Survival has no public board.</summary>
    private void LoadGuestBoard(int request)
    {
        _card = null;
        if (GameId == ScoreRules.GameSurvival)
        {
            ShowLoginMessage();
            return;
        }
        Message("Loading...", "", null, null);
        LeaderboardService.GetPublicBoard(Scope, GameId, _language, delegate (LeaderboardService.BoardState state, LeaderboardService.BoardRow[] rows)
        {
            if (request != _request) return;
            if (state != LeaderboardService.BoardState.Ok)
            {
                Fail(state);
                return;
            }
            _offline = false;
            RefreshStatus();
            ShowRows(rows, false);
        });
    }

    private void FetchRows(int request)
    {
        if (GameId == ScoreRules.GameSurvival)
        {
            LeaderboardService.GetSurvivalBoard("free", _language, delegate (LeaderboardService.BoardState state, LeaderboardService.BoardRow[] rows)
            {
                if (request != _request) return;
                if (state == LeaderboardService.BoardState.Ok) ShowRows(rows, true);
                else Fail(state);
            });
            return;
        }
        LeaderboardService.GetBoard(Scope, GameId, _language, delegate (LeaderboardService.BoardState state, LeaderboardService.BoardRow[] rows)
        {
            if (request != _request) return;
            if (state != LeaderboardService.BoardState.Ok)
            {
                Fail(state);
                return;
            }
            ShowRows(rows, false);
            CheckWeeklyTop10(rows);
        });
    }

    private void CheckWeeklyTop10(LeaderboardService.BoardRow[] rows)
    {
        if (_period != 0 || _game != 0 || _language >= 0)
        {
            return;
        }
        foreach (LeaderboardService.BoardRow row in rows)
        {
            if (row.is_me && row.rank <= 10)
            {
                AchievementTracker.AwardOnline(AchievementTracker.WeeklyTop10Id);
            }
        }
    }

    /// <summary>Asks for the rows again without the "Loading..." message. A failed attempt leaves the board as it is and tries again at the next interval.</summary>
    private void RefreshBoardQuietly()
    {
        _nextRefresh = Time.unscaledTime + AutoRefreshSeconds;
        int request = ++_request;
        bool survival = GameId == ScoreRules.GameSurvival;
        _refreshing = true;
        System.Action<LeaderboardService.BoardState, LeaderboardService.BoardRow[]> done = delegate (LeaderboardService.BoardState state, LeaderboardService.BoardRow[] rows)
        {
            _refreshing = false;
            if (request == _request && state == LeaderboardService.BoardState.Ok)
            {
                ApplyRefreshedRows(rows, survival);
            }
        };
        if (survival)
        {
            LeaderboardService.GetSurvivalBoard("free", _language, done);
        }
        else if (!CloudSave.SignedIn)
        {
            LeaderboardService.GetPublicBoard(Scope, GameId, _language, done);
        }
        else
        {
            LeaderboardService.GetBoard(Scope, GameId, _language, done);
        }
    }

    private static string Signature(LeaderboardService.BoardRow[] rows)
    {
        var text = new System.Text.StringBuilder();
        foreach (LeaderboardService.BoardRow row in rows)
        {
            text.Append(row.rank).Append('|').Append(row.nickname).Append('|').Append(row.avatar).Append('|').Append(row.puzzles).Append('|').Append(row.points).Append('|').Append(row.is_me).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>Redraws the board only when a row changed, and keeps the player's place in the list.</summary>
    private void ApplyRefreshedRows(LeaderboardService.BoardRow[] rows, bool survival)
    {
        if (Signature(rows) == _boardSignature)
        {
            return;
        }
        float scrolled = _rowsScroll != null ? _rowsScroll.content.anchoredPosition.y : 0f;
        ShowRows(rows, survival);
        if (_rowsScroll != null)
        {
            Vector2 position = _rowsScroll.content.anchoredPosition;
            position.y = scrolled;
            _rowsScroll.content.anchoredPosition = position;
        }
        CheckWeeklyTop10(rows);
    }

    private void ShowRows(LeaderboardService.BoardRow[] rows, bool survival)
    {
        _boardShowing = true;
        _boardSignature = Signature(rows);
        _nextRefresh = Time.unscaledTime + AutoRefreshSeconds;
        _rowsScroll = null;
        Clear(_left);
        Clear(_right);
        Text(_left, survival ? "Longest voyages" : _period == 1 ? "Top players of all time" : "Top players this week", 30, Ink, true, TextAlignmentOptions.Center, 0, 20, 620, 44);

        LeaderboardService.BoardRow me = null;
        var others = new List<LeaderboardService.BoardRow>();
        foreach (LeaderboardService.BoardRow row in rows)
        {
            if (row.is_me) me = row;
            if (row.rank > 3) others.Add(row);
        }
        BuildPodium(rows, survival);
        if (survival)
        {
            Text(_left, "Ranked by the best run", 23, InkSoft, false, TextAlignmentOptions.Center, 0, 608, 620, 40);
        }

        Text(_right, "Rank", 24, InkSoft, false, TextAlignmentOptions.MidlineLeft, 52, 18, 100, 36);
        Text(_right, "Player", 24, InkSoft, false, TextAlignmentOptions.MidlineLeft, 162, 18, 300, 36);
        Text(_right, survival ? "Rounds" : "Puzzles", 24, InkSoft, false, TextAlignmentOptions.MidlineRight, 660, 18, 170, 36);
        Text(_right, survival ? "Score" : "Points", 24, InkSoft, false, TextAlignmentOptions.MidlineRight, 860, 18, 190, 36);
        Image rule = LibraryUiKit.Box(_right, "rule", Rule, false);
        LibraryUiKit.Place(rule.rectTransform, 40, 58, 1020, 3);

        if (rows.Length == 0)
        {
            bool guest = !CloudSave.SignedIn;
            TextMeshProUGUI none = Text(_right, guest ? "No one is on this board yet.\nLog in and play a puzzle to be the first." : "No one is on this board yet.\nPlay a puzzle to be the first.", 30, InkSoft, false, TextAlignmentOptions.Center, 100, 200, 900, 150);
            none.enableWordWrapping = true;
            if (guest)
            {
                Chip(_right, "Log in", 400, 380, 300, 76, true, Purple, 32, GoToProfile);
            }
            return;
        }

        RectTransform content;
        _rowsScroll = LibraryUiKit.MakeScroll(_right, "rows", 40, 62, 1020, 442, out content);
        float y = 0;
        foreach (LeaderboardService.BoardRow row in others)
        {
            if (row.is_me) continue;
            BuildRow(content, row, y, false);
            y += 72;
        }
        if (others.Count == 0 || (others.Count == 1 && others[0].is_me))
        {
            Text(content, "Only the top three so far.", 26, InkSoft, false, TextAlignmentOptions.Center, 0, 40, 1020, 50);
        }
        content.sizeDelta = new Vector2(0, Mathf.Max(y, 10));

        if (me != null)
        {
            Image mine = Card(_right, "my_row", 40, 512, 1020, 84, Purple);
            Image inner = Card(mine.transform, "inner", 4, 4, 1012, 76, PurpleTint);
            BuildRow(inner.rectTransform, me, 3, true);
        }
        else if (!CloudSave.SignedIn)
        {
            Text(_right, "You are viewing as a guest. Log in to join the board.", 27, InkSoft, false, TextAlignmentOptions.MidlineLeft, 52, 524, 780, 60);
            Chip(_right, "Log in", 850, 522, 210, 64, true, Purple, 30, GoToProfile);
        }
        else
        {
            bool shown = _card != null && _card.show_on_board;
            Text(_right, shown ? "Play a puzzle to get on this board." : "Your name can not be shown. Please change it on the Profile screen.", 27, InkSoft, false, TextAlignmentOptions.Center, 40, 524, 1020, 60);
        }
        Text(_right, "Only names and avatars are shown.", 22, InkSoft, false, TextAlignmentOptions.Center, 40, 604, 1020, 36);
    }

    private static void BuildRow(Transform parent, LeaderboardService.BoardRow row, float y, bool me)
    {
        RectTransform r = LibraryUiKit.NewRect("row", parent);
        LibraryUiKit.Place(r, 0, y, 1012, 70);
        Text(r, "#" + row.rank, 34, me ? Purple : Ink, true, TextAlignmentOptions.MidlineLeft, 14, 0, 110, 70);
        Avatar(r, row.avatar, 124, 7, 56, Cream);
        TextMeshProUGUI name = Text(r, row.nickname + (me ? "  (you)" : string.Empty), 30, Ink, true, TextAlignmentOptions.MidlineLeft, 198, 0, 440, 70);
        name.enableAutoSizing = true;
        name.fontSizeMin = 20f;
        name.fontSizeMax = 30f;
        Text(r, row.puzzles.ToString(), 30, Ink, false, TextAlignmentOptions.MidlineRight, 640, 0, 150, 70);
        Text(r, row.points.ToString("N0", CultureInfo.InvariantCulture), 32, Ink, true, TextAlignmentOptions.MidlineRight, 810, 0, 190, 70);
    }

    private void BuildPodium(LeaderboardService.BoardRow[] rows, bool survival)
    {
        int[] order = { 1, 0, 2 };   // second place, first place, third place from left to right
        float[] heights = { 180, 240, 130 };
        Color[] colors = { Silver, Gold, Bronze };
        for (int i = 0; i < 3; i++)
        {
            int place = order[i];
            float x = 30 + i * 196;
            float blockTop = 590 - heights[i];
            Image block = Card(_left, "place", x, blockTop, 180, heights[i], colors[i]);
            Text(block.transform, (place + 1).ToString(), 72, Color.white, true, TextAlignmentOptions.Top, 0, 12, 180, 90);
            if (place >= rows.Length || rows[place].rank > 3)
            {
                continue;
            }
            LeaderboardService.BoardRow row = rows[place];
            Avatar(_left, row.avatar, x + 15, blockTop - 240, 150, colors[i]);
            TextMeshProUGUI name = Text(_left, row.nickname, 30, Ink, true, TextAlignmentOptions.Center, x - 6, blockTop - 86, 192, 40);
            name.enableAutoSizing = true;
            name.fontSizeMin = 18f;
            name.fontSizeMax = 30f;
            Text(_left, row.points.ToString("N0", CultureInfo.InvariantCulture) + (survival ? "" : " pts"), 26, InkSoft, false, TextAlignmentOptions.Center, x - 6, blockTop - 46, 192, 36);
        }
    }

    // ------------------------------------------------------------------ my records

    private void BuildRecords()
    {
        Transform root = tabRoots[1];
        Clear(root);

        Card(root, "points_card", 80, 190, 620, 840, Cream);
        int points = ResultsStore.PointsOfWeek(ResultsStore.ThisWeek);
        int best = ResultsStore.BestWeek();
        Text(root, "My points this week", 30, Ink, true, TextAlignmentOptions.MidlineLeft, 120, 220, 540, 44);
        Text(root, points.ToString("N0", CultureInfo.InvariantCulture), 120, Purple, true, TextAlignmentOptions.MidlineLeft, 120, 266, 540, 140);
        string toBeat = points >= best ? (best > 0 ? "This is your best week!" : "Finish a puzzle to earn points") : (best - points).ToString("N0", CultureInfo.InvariantCulture) + " to beat it";
        Text(root, "Best week: <b>" + best.ToString("N0", CultureInfo.InvariantCulture) + "</b>  ·  " + toBeat, 27, InkSoft, false, TextAlignmentOptions.MidlineLeft, 120, 414, 560, 40);
        Card(root, "bar_back", 120, 470, 540, 22, Rule);
        float fill = best > 0 ? Mathf.Clamp01((float)points / best) : 0f;
        if (fill > 0f)
        {
            Card(root, "bar_fill", 120, 470, Mathf.Max(22f, 540f * fill), 22, Purple);
        }

        Text(root, "Last 6 weeks  <size=21><color=#7A6A58>(week starting)</color></size>", 26, Ink, true, TextAlignmentOptions.MidlineLeft, 120, 540, 540, 40);
        List<ResultsStore.WeekPoints> weeks = ResultsStore.RecentWeeks(6);
        int highest = 1;
        foreach (ResultsStore.WeekPoints w in weeks)
        {
            highest = Mathf.Max(highest, w.Points);
        }
        for (int i = 0; i < weeks.Count; i++)
        {
            float barHeight = weeks[i].Points > 0 ? Mathf.Max(14f, 250f * weeks[i].Points / highest) : 6f;
            bool current = i == weeks.Count - 1;
            Card(root, "week_bar", 130 + i * 88, 900 - barHeight, 62, barHeight, current ? Purple : PurpleSoft);
            TextMeshProUGUI label = Text(root, weeks[i].Monday.ToString("MMM d", CultureInfo.InvariantCulture), 21, current ? Purple : InkSoft, current, TextAlignmentOptions.Center, 117 + i * 88, 908, 88, 32);
            label.overflowMode = TextOverflowModes.Overflow;
        }
        Image note = Card(root, "saved_note", 110, 952, 560, 62, Peach);
        TextMeshProUGUI noteText = Text(note.transform, CloudSave.SignedIn ? "Saved on this computer and sent to the online board." : "Saved on this computer. Sent to the online board when you log in.", 21, Ink, false, TextAlignmentOptions.MidlineLeft, 20, 0, 524, 62);
        noteText.enableWordWrapping = true;

        var games = new List<string> { ScoreRules.GameWordle, ScoreRules.GameCrossword, ScoreRules.GameWordSearch };
        bool survival = GameFeatures.SurvivalEnabled;
        float cardHeight = survival ? 138 : 180;
        float step = survival ? 150 : 200;
        float y = 190;
        foreach (string game in games)
        {
            BuildGameCard(root, game, y, cardHeight);
            y += step;
        }
        if (survival)
        {
            BuildSurvivalCard(root, y, cardHeight);
            y += step;
        }
        BuildBeatYourBest(root, y, survival);
    }

    private static void GameStat(Transform root, string title, string value, string note, float x, float y, float height)
    {
        float scale = height / 180f;
        Text(root, title, 22, InkSoft, false, TextAlignmentOptions.MidlineLeft, x, y + 28 * scale, 200, 32);
        Text(root, value, 46, Ink, true, TextAlignmentOptions.MidlineLeft, x, y + 60 * scale, 200, 56);
        TextMeshProUGUI noteLabel = Text(root, note, 19, InkSoft, false, TextAlignmentOptions.MidlineLeft, x, y + 120 * scale, 190, 28);
        noteLabel.enableAutoSizing = true;
        noteLabel.fontSizeMin = 13f;
        noteLabel.fontSizeMax = 19f;
    }

    private static PuzzleRecord RecordOf(string key)
    {
        foreach (PuzzleRecord r in ResultsStore.AllRecords())
        {
            if (r.Key == key)
            {
                return r;
            }
        }
        return null;
    }

    private static void BuildGameCard(Transform root, string game, float y, float height)
    {
        Card(root, game + "_card", 740, y, 1100, height, Cream);
        float icon = Mathf.Min(108, height - 60);
        GameIcon(root, game, 770, y + (height - icon) / 2f, icon);
        Text(root, ResultsStore.GameName(game), 40, Ink, true, TextAlignmentOptions.MidlineLeft, 910, y + height / 2f - 50, 320, 56);
        ResultsStore.GameSummary s = ResultsStore.Summary(game);
        int total = 0;
        for (int l = 0; l < 3; l++)
        {
            total += game == ScoreRules.GameWordle ? WordleData.TotalLevels(l) : game == ScoreRules.GameCrossword ? CrosswordCategoryData.TotalPuzzles(l) : TotalWordSearch(l);
        }
        Text(root, s.Puzzles + " / " + total + " scored", 24, InkSoft, false, TextAlignmentOptions.MidlineLeft, 910, y + height / 2f + 8, 320, 36);

        GameStat(root, "Best score", s.BestScore > 0 ? s.BestScore.ToString() : "-", s.BestKey != null ? ResultsStore.Describe(RecordOf(s.BestKey)) : string.Empty, 1240, y, height);
        GameStat(root, "Fastest", s.Fastest > 0 ? ResultsStore.Clock(s.Fastest) : "-", s.FastestKey != null ? ResultsStore.Describe(RecordOf(s.FastestKey)) : string.Empty, 1440, y, height);
        bool wordle = game == ScoreRules.GameWordle;
        GameStat(root, "Personal record", (wordle ? s.FirstTries : s.NoHintWins).ToString(), wordle ? "First try" : "No-hint wins", 1640, y, height);
    }

    private static int TotalWordSearch(int language)
    {
        int total = 0;
        for (int c = 0; c < WordSearchData.CategoryCount(language); c++)
        {
            WordSearchCategory category = WordSearchData.Category(language, c);
            total += category != null ? category.puzzles.Count : 0;
        }
        return total;
    }

    private static void BuildSurvivalCard(Transform root, float y, float height)
    {
        Card(root, "survival_card", 740, y, 1100, height, Cream);
        float icon = Mathf.Min(108, height - 60);
        GameIcon(root, ScoreRules.GameSurvival, 770, y + (height - icon) / 2f, icon);
        Text(root, "Survival", 40, Ink, true, TextAlignmentOptions.MidlineLeft, 910, y + height / 2f - 50, 320, 56);
        SurvivalRecords.Data d = SurvivalRecords.Load();
        Text(root, d.Runs + (d.Runs == 1 ? " run" : " runs"), 24, InkSoft, false, TextAlignmentOptions.MidlineLeft, 910, y + height / 2f + 8, 320, 36);
        GameStat(root, "Best rounds", d.BestRounds > 0 ? d.BestRounds.ToString() : "-", string.Empty, 1240, y, height);
        GameStat(root, "Best score", d.BestScore > 0 ? d.BestScore.ToString("N0", CultureInfo.InvariantCulture) : "-", string.Empty, 1440, y, height);
        GameStat(root, "Longest streak", d.LongestStreak > 0 ? d.LongestStreak.ToString() : "-", "Correct in a row", 1640, y, height);
    }

    private void BuildBeatYourBest(Transform root, float y, bool compact)
    {
        float height = 1030 - y;
        Card(root, "beat_card", 740, y, 1100, height, Sand);
        var trophy = Resources.Load<Sprite>("Trophies/trophy");
        float size = Mathf.Min(96, height - 40);
        if (trophy != null)
        {
            Image icon = LibraryUiKit.Icon(root, "trophy", trophy, Orange);
            LibraryUiKit.Place(icon.rectTransform, 780, y + (height - size) / 2f, size, size);
        }
        Text(root, "Beat your best", compact ? 30 : 36, Ink, true, TextAlignmentOptions.MidlineLeft, 900, y + (compact ? 12 : 34), 620, 50);
        PuzzleRecord pick = ResultsStore.BeatYourBest();
        string line;
        if (pick == null)
        {
            line = "Finish a puzzle to start your records.";
        }
        else if (pick.Game == ScoreRules.GameWordle)
        {
            line = "Your best score in <b><color=#3B2A1A>" + ResultsStore.Describe(pick) + "</color></b> is " + pick.Score + ". Can you score higher?";
        }
        else
        {
            line = "Your fastest " + ResultsStore.GameName(pick.Game) + " in <b><color=#3B2A1A>" + ResultsStore.Describe(pick) + "</color></b> is " + ResultsStore.Clock(pick.Fastest) + ". Can you finish it faster?";
        }
        TextMeshProUGUI text = Text(root, line, compact ? 23 : 27, InkSoft, false, TextAlignmentOptions.TopLeft, 900, y + (compact ? 58 : 90), 620, compact ? 56 : 90);
        text.enableWordWrapping = true;
        if (pick != null)
        {
            Chip(root, "Try again", 1560, y + (height - 84) / 2f, 240, 84, true, Green, 34, delegate { PlayAgain(pick); });
        }
    }

    private static void PlayAgain(PuzzleRecord record)
    {
        StreakTracker.RecordPlay();
        switch (record.Game)
        {
            case ScoreRules.GameWordle:
                WordleSession.Language = record.Language;
                WordleSession.Category = record.Category;
                WordleSession.Level = record.Level;
                SceneManager.LoadScene(WordleSession.PlayScene);
                break;
            case ScoreRules.GameCrossword:
                CrosswordSession.Language = record.Language;
                CrosswordSession.Category = record.Category;
                CrosswordSession.Size = record.Level;
                SceneManager.LoadScene(CrosswordSession.PlayScene);
                break;
            case ScoreRules.GameWordSearch:
                WordSearchSession.Language = record.Language;
                WordSearchSession.Category = record.Category;
                WordSearchSession.Puzzle = record.Level;
                SceneManager.LoadScene(WordSearchSession.PlayScene);
                break;
        }
    }

    // ------------------------------------------------------------------ badges

    private void BuildBadges()
    {
        Transform root = tabRoots[2];
        Clear(root);
        List<BadgeState> states = AchievementTracker.CheckAll(false);
        int earned = 0;
        foreach (BadgeState s in states)
        {
            if (s.Earned) earned++;
        }
        SetStatus("<b>" + earned + " of " + states.Count + "</b>  earned", Gold);

        string[] filters = { "All", "Games", "Words", "Streaks", "Online", "Survival" };
        float[] widths = { 90, 130, 124, 136, 124, 136 };
        int filterCount = GameFeatures.SurvivalEnabled ? 6 : 5;
        float x = 80;
        for (int i = 0; i < filterCount; i++)
        {
            int index = i;
            Chip(root, filters[i], x, 186, widths[i], 56, _badgeFilter == i, Ink, 25, delegate { _badgeFilter = index; BuildBadges(); });
            x += widths[i] + 12;
        }

        RectTransform content;
        LibraryUiKit.MakeScroll(root, "badges", 80, 262, 1760, 786, out content);
        int shown = 0;
        foreach (BadgeState s in states)
        {
            if (_badgeFilter != 0 && (int)s.Def.Group != _badgeFilter - 1)
            {
                continue;
            }
            BuildBadge(content, s, (shown % 4) * 440, (shown / 4) * 194);
            shown++;
        }
        content.sizeDelta = new Vector2(0, (shown + 3) / 4 * 194);
    }

    private static void BuildBadge(Transform parent, BadgeState s, float x, float y)
    {
        const float W = 420, H = 176;
        Card(parent, "badge_" + s.Def.Id, x, y, W, H, s.Earned ? Cream : LockedCard);

        Color ring = LockedRing;
        if (s.Earned)
        {
            ring = s.Def.Tiers.Length <= 1 ? Gold : (s.EarnedTier == 0 ? Bronze : s.EarnedTier == 1 ? Silver : Gold);
        }
        Image outer = LibraryUiKit.Icon(parent, "ring", LibraryUiKit.Circle, ring);
        LibraryUiKit.Place(outer.rectTransform, x + 22, y + (H - 116) / 2f, 116, 116);
        Image face = LibraryUiKit.Icon(outer.transform, "face", LibraryUiKit.Circle, s.Earned ? s.Def.Color : LockedFace);
        LibraryUiKit.Place(face.rectTransform, 12, 12, 92, 92);
        Text(face.transform, s.Def.Glyph, s.Def.Glyph.Length < 3 ? 44 : 32, Color.white, true, TextAlignmentOptions.Center, 0, 0, 92, 92);

        Color nameColor = s.Earned ? Ink : (Color)new Color32(143, 132, 116, 255);
        TextMeshProUGUI name = Text(parent, s.Def.Name, 28, nameColor, true, TextAlignmentOptions.MidlineLeft, x + 156, y + 18, W - 170, 40);
        name.enableAutoSizing = true;
        name.fontSizeMin = 20f;
        name.fontSizeMax = 28f;
        TextMeshProUGUI description = Text(parent, s.Def.Description, 20, InkSoft, false, TextAlignmentOptions.TopLeft, x + 156, y + 60, W - 172, 56);
        description.enableWordWrapping = true;

        float bottom = y + H - 44;
        if (s.Earned)
        {
            string tier = s.TierName.Length > 0 ? s.TierName : "Earned";
            Text(parent, tier, 20, new Color32(138, 100, 0, 255), true, TextAlignmentOptions.MidlineLeft, x + 156, bottom, 110, 32);
            if (s.HasNextTier)
            {
                TextMeshProUGUI next = Text(parent, "next tier " + s.Value + "/" + s.Target, 19, InkSoft, false, TextAlignmentOptions.MidlineRight, x + 256, bottom, 150, 32);
                next.overflowMode = TextOverflowModes.Overflow;
            }
        }
        else if (s.Target > 1)
        {
            Image back = Card(parent, "bar", x + 156, bottom + 9, W - 260, 14, new Color32(230, 221, 203, 255));
            float fill = Mathf.Clamp01((float)s.Value / s.Target);
            if (fill > 0f)
            {
                Card(back.transform, "fill", 0, 0, Mathf.Max(14f, (W - 260) * fill), 14, s.Def.Color);
            }
            TextMeshProUGUI count = Text(parent, s.Value + "/" + s.Target, 20, InkSoft, false, TextAlignmentOptions.MidlineRight, x + W - 100, bottom, 86, 32);
            count.overflowMode = TextOverflowModes.Overflow;
        }
        else
        {
            Text(parent, "Locked", 20, new Color32(167, 155, 136, 255), false, TextAlignmentOptions.MidlineLeft, x + 156, bottom, 140, 32);
        }
    }
}
