using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LeaderboardService
/// The game's connection to the online leaderboards in Supabase, through the database functions of Documentation/Supabase/Leaderboard_Setup.sql. Every call goes
/// through CloudSave.CallApi and needs a login, except GetPublicBoard, the view-only board for guests. The game never sends a finished score: it sends what happened (time, tries, hints) and the server works
/// out the score itself. SubmitAll is safe to repeat, because each puzzle only keeps its best result; that is also what uploads results earned offline.
/// SubmitAll also publishes the player's name and avatar, so every logged-in player appears on the boards automatically.
/// When the SQL file has not been run yet, the calls answer BoardState.NotSetUp and the screens say so.
/// </summary>
public static class LeaderboardService
{
    public enum BoardState
    {
        Ok,
        NoInternet,
        NotSetUp,
        NotSignedIn,
        Error
    }

    [Serializable]
    public class BoardRow
    {
        public int rank;
        public string nickname;
        public int avatar;
        public int puzzles;
        public int points;
        public bool is_me;
    }

    [Serializable]
    public class PlayerCard
    {
        public string nickname;
        public int avatar;
        public bool show_on_board;
    }

    [Serializable] private class BoardRows { public BoardRow[] rows; }
    [Serializable] private class CardRows { public PlayerCard[] rows; }

    [Serializable]
    private class UploadRow
    {
        public string game;
        public string puzzle;
        public int language;
        public string week;
        public int seconds;
        public int seconds_left;
        public int tries_left;
        public int hints;
        public int checks;
        public int reveals;
        public int words;
        public bool meaning;
    }

    [Serializable] private class SubmitBody { public UploadRow[] p_rows; }
    [Serializable] private class BoardBody { public string p_scope; public string p_game; public int p_language; public string p_class; }
    [Serializable] private class PublicBoardBody { public string p_scope; public string p_game; public int p_language; }
    [Serializable] private class SurvivalBoardBody { public string p_scope; public int p_language; }
    [Serializable] private class CardBody { public string p_nickname; public int p_avatar; public bool p_show; }
    [Serializable] private class EmptyBody { }
    [Serializable]
    private class SurvivalBody
    {
        public string p_mode;
        public int p_language;
        public int p_rounds;
        public int p_score;
        public int p_streak;
    }

    private static BoardState StateOf(long code)
    {
        if (code >= 200 && code < 300) return BoardState.Ok;
        if (code == 0) return BoardState.NoInternet;
        if (code == 404) return BoardState.NotSetUp;
        if (code == 401 || code == 403) return BoardState.NotSignedIn;
        return BoardState.Error;
    }

    private static void Rpc(string function, object body, Action<long, string> done)
    {
        CloudSave.CallApi("POST", "/rest/v1/rpc/" + function, JsonUtility.ToJson(body), done);
    }

    private static T[] Rows<T, TList>(string text, Func<TList, T[]> read)
    {
        try
        {
            TList list = JsonUtility.FromJson<TList>("{\"rows\":" + text + "}");
            return list != null ? read(list) ?? new T[0] : new T[0];
        }
        catch (Exception)
        {
            return new T[0];
        }
    }

    /// <summary>Publishes the player's name and avatar, then uploads every saved result. A name the server refuses only keeps the player off the board; the results are still sent.</summary>
    public static void SubmitAll(Action<BoardState> done)
    {
        PlayerDatabase db = PlayerDatabase.Instance;
        string name = db != null ? db.PlayerNameOrId : "Player";
        int avatar = db != null ? db.AvatarIndex : 0;
        SetMyCard(name, avatar, true, delegate (BoardState state, string refused) { UploadResults(done); });
    }

    private static void UploadResults(Action<BoardState> done)
    {
        var rows = new List<UploadRow>();
        foreach (PuzzleRecord r in ResultsStore.ForUpload())
        {
            rows.Add(new UploadRow
            {
                game = r.Game, puzzle = r.Key, language = r.Language, week = r.Week, seconds = r.Seconds, seconds_left = r.SecondsLeft, tries_left = r.TriesLeft,
                hints = r.Hints, checks = r.Checks, reveals = r.Reveals, words = r.Words, meaning = r.Meaning
            });
        }
        if (rows.Count == 0)
        {
            if (done != null) done(CloudSave.SignedIn ? BoardState.Ok : BoardState.NotSignedIn);
            return;
        }
        Rpc("submit_scores", new SubmitBody { p_rows = rows.ToArray() }, delegate (long code, string text) { if (done != null) done(StateOf(code)); });
    }

    /// <summary>scope: "week", "last_week" or "all". game: "all" or a game id. language: -1 for all. p_class stays empty: the database function still takes it.</summary>
    public static void GetBoard(string scope, string game, int language, Action<BoardState, BoardRow[]> done)
    {
        Rpc("get_board", new BoardBody { p_scope = scope, p_game = game, p_language = language, p_class = string.Empty }, delegate (long code, string text)
        {
            BoardState state = StateOf(code);
            done(state, state == BoardState.Ok ? Rows<BoardRow, BoardRows>(text, l => l.rows) : new BoardRow[0]);
        });
    }

    /// <summary>The view-only board for guests; works without a login. scope: "week" or "all". game: "all" or a game id. language: -1 for all.</summary>
    public static void GetPublicBoard(string scope, string game, int language, Action<BoardState, BoardRow[]> done)
    {
        string body = JsonUtility.ToJson(new PublicBoardBody { p_scope = scope, p_game = game, p_language = language });
        CloudSave.CallPublicApi("POST", "/rest/v1/rpc/get_public_board", body, delegate (long code, string text)
        {
            BoardState state = StateOf(code);
            done(state, state == BoardState.Ok ? Rows<BoardRow, BoardRows>(text, l => l.rows) : new BoardRow[0]);
        });
    }

    public static void GetMyCard(Action<BoardState, PlayerCard> done)
    {
        Rpc("get_my_card", new EmptyBody(), delegate (long code, string text)
        {
            BoardState state = StateOf(code);
            PlayerCard[] rows = state == BoardState.Ok ? Rows<PlayerCard, CardRows>(text, l => l.rows) : new PlayerCard[0];
            done(state, rows.Length > 0 ? rows[0] : null);
        });
    }

    /// <summary>done(state, errorText): errorText is a sentence for the player when the nickname was refused.</summary>
    public static void SetMyCard(string nickname, int avatar, bool show, Action<BoardState, string> done)
    {
        Rpc("set_my_card", new CardBody { p_nickname = nickname, p_avatar = avatar, p_show = show }, delegate (long code, string text)
        {
            bool refused = code >= 400 && text != null && text.Contains("nickname_not_allowed");
            done(StateOf(code), refused ? "That name can not be shown. Please change your name on the Profile screen." : null);
        });
    }

    /// <summary>Prepared for the Survival mode. mode: "free" or "daily".</summary>
    public static void SubmitSurvivalRun(string mode, int language, SurvivalRun run, Action<BoardState> done)
    {
        Rpc("submit_survival_run", new SurvivalBody { p_mode = mode, p_language = language, p_rounds = run.Rounds, p_score = run.Score, p_streak = run.LongestStreak },
            delegate (long code, string text) { if (done != null) done(StateOf(code)); });
    }

    /// <summary>Prepared for the Survival mode. scope: "free" (best run) or "daily" (today's Daily Voyage). In the rows, puzzles holds the rounds and points the score.</summary>
    public static void GetSurvivalBoard(string scope, int language, Action<BoardState, BoardRow[]> done)
    {
        Rpc("get_survival_board", new SurvivalBoardBody { p_scope = scope, p_language = language }, delegate (long code, string text)
        {
            BoardState state = StateOf(code);
            done(state, state == BoardState.Ok ? Rows<BoardRow, BoardRows>(text, l => l.rows) : new BoardRow[0]);
        });
    }

    /// <summary>Removes the player's scores and public card. Called when the player deletes the online data.</summary>
    public static void DeleteMyData()
    {
        Rpc("delete_my_leaderboard_data", new EmptyBody(), null);
    }

    private const string LastRankKey = "leaderboard.lastRank";

    /// <summary>After a puzzle: uploads the results and answers with the player's weekly rank (0 when not on the board) and how much it moved.</summary>
    public static void RankAfterResult(Action<int, int> done)
    {
        if (!CloudSave.SignedIn)
        {
            return;
        }
        SubmitAll(delegate (BoardState state)
        {
            if (state != BoardState.Ok)
            {
                return;
            }
            GetBoard("week", "all", -1, delegate (BoardState boardState, BoardRow[] rows)
            {
                foreach (BoardRow row in rows)
                {
                    if (row.is_me)
                    {
                        int last = SaveStore.GetInt(LastRankKey, 0);
                        SaveStore.SetInt(LastRankKey, row.rank);
                        done(row.rank, last > 0 ? last - row.rank : 0);
                        if (row.rank <= 10)
                        {
                            AchievementTracker.AwardOnline(AchievementTracker.WeeklyTop10Id);
                        }
                        return;
                    }
                }
                done(0, 0);
            });
        });
    }
}
