using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// TrophyOverlay
/// A canvas that stays above every scene and is created by itself when the game starts. It shows
///  - the "Achievement unlocked" message, 4 seconds per badge (when many badges arrive at once, one message says how many),
///  - the login question "You have guest progress" with Don't add / Add to account (asked by CloudSave).
/// It also runs AchievementTracker.CheckAll whenever a scene opens, so badges earned in any screen (Library favorites, Phrasebook, streak) are noticed.
/// Coordinates are in the usual 1920x1080 layout.
/// </summary>
public class TrophyOverlay : MonoBehaviour
{
    private const float ToastSeconds = 4f;

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color InkSoft = new Color32(107, 82, 55, 255);
    private static readonly Color Gold = new Color32(232, 185, 34, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Purple = new Color32(138, 64, 199, 255);
    private static readonly Color Sand = new Color32(250, 242, 222, 255);

    private static TrophyOverlay _instance;
    private RectTransform _stage;
    private readonly Queue<BadgeState> _queue = new Queue<BadgeState>();
    private Coroutine _toastRoutine;
    private GameObject _guestPrompt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance == null)
        {
            var go = new GameObject("TrophyOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrophyOverlay>();
        }
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        _stage = LibraryUiKit.NewRect("Stage", transform);
        _stage.anchorMin = _stage.anchorMax = new Vector2(0.5f, 0.5f);
        _stage.pivot = new Vector2(0.5f, 0.5f);
        _stage.anchoredPosition = Vector2.zero;
        _stage.sizeDelta = new Vector2(1920, 1080);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AchievementTracker.CheckAll(true);
    }

    private static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, bool bold, TextAlignmentOptions align, float x, float y, float w, float h)
    {
        TextMeshProUGUI t = TrophyFont.Apply(LibraryUiKit.Label(parent, text, size, color, bold ? FontStyles.Bold : FontStyles.Normal, align));
        LibraryUiKit.Place(t.rectTransform, x, y, w, h);
        return t;
    }

    // ------------------------------------------------------------------ unlock message

    /// <summary>Shows the new badges one after the other.</summary>
    public static void Announce(List<BadgeState> badges)
    {
        if (_instance == null || badges == null || badges.Count == 0)
        {
            return;
        }
        if (badges.Count > 3)
        {
            _instance.ShowSummary(badges.Count);
            return;
        }
        foreach (BadgeState badge in badges)
        {
            _instance._queue.Enqueue(badge);
        }
        if (_instance._toastRoutine == null)
        {
            _instance._toastRoutine = _instance.StartCoroutine(_instance.PlayQueue());
        }
    }

    private void ShowSummary(int count)
    {
        _queue.Clear();
        _queue.Enqueue(new BadgeState { Def = new BadgeDef { Name = count + " new badges", Description = "Open the trophies to see them", Tiers = new[] { 1 } } });
        if (_toastRoutine == null)
        {
            _toastRoutine = StartCoroutine(PlayQueue());
        }
    }

    private IEnumerator PlayQueue()
    {
        while (_queue.Count > 0)
        {
            BadgeState badge = _queue.Dequeue();
            GameObject toast = BuildToast(badge);
            yield return new WaitForSecondsRealtime(ToastSeconds);
            if (toast != null)
            {
                Destroy(toast);
            }
        }
        _toastRoutine = null;
    }

    private GameObject BuildToast(BadgeState badge)
    {
        Image border = LibraryUiKit.Box(_stage, "achievement_toast", Gold, true);
        LibraryUiKit.Place(border.rectTransform, 560, 28, 800, 150);
        Image card = LibraryUiKit.Box(border.transform, "card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 5, 5, 790, 140);
        var medal = Resources.Load<Sprite>("Trophies/medal");
        if (medal != null)
        {
            Image icon = LibraryUiKit.Icon(card.transform, "medal", medal, Color.white);
            LibraryUiKit.Place(icon.rectTransform, 24, 14, 112, 112);
        }
        Text(card.transform, "ACHIEVEMENT UNLOCKED", 25, InkSoft, true, TextAlignmentOptions.MidlineLeft, 160, 14, 480, 34);
        string tier = badge.TierName.Length > 0 ? " · " + badge.TierName : string.Empty;
        TextMeshProUGUI name = Text(card.transform, badge.Def.Name + tier, 44, Ink, true, TextAlignmentOptions.MidlineLeft, 160, 46, 480, 56);
        name.enableAutoSizing = true;
        name.fontSizeMin = 28f;
        name.fontSizeMax = 44f;
        Text(card.transform, badge.Def.Description, 24, InkSoft, false, TextAlignmentOptions.MidlineLeft, 160, 100, 480, 32);
        Image view = TrophyFont.Apply(LibraryUiKit.Pill(card.transform, "View", 650, 44, 118, 56, Purple, Color.white, 28, FontStyles.Bold));
        LibraryUiKit.MakeButton(view, delegate { TrophyRoomController.Open(2, "1 Main_menu"); });
        return border.gameObject;
    }

    // ------------------------------------------------------------------ guest progress question

    /// <summary>Asks whether the guest progress joins the account. answer(true) = Add to account.</summary>
    public static void AskAboutGuestProgress(int puzzles, int words, int bestStreak, Action<bool> answer)
    {
        if (_instance == null)
        {
            answer(true);
            return;
        }
        _instance.ShowGuestPrompt(puzzles, words, bestStreak, answer);
    }

    private void ShowGuestPrompt(int puzzles, int words, int bestStreak, Action<bool> answer)
    {
        if (_guestPrompt != null)
        {
            Destroy(_guestPrompt);
        }
        Image dim = LibraryUiKit.Box(_stage, "guest_progress_prompt", new Color(0.1f, 0.14f, 0.22f, 0.6f), false);
        LibraryUiKit.Fill(dim.rectTransform);
        dim.raycastTarget = true;
        _guestPrompt = dim.gameObject;

        Image card = LibraryUiKit.Box(dim.transform, "card", Cream, true);
        LibraryUiKit.Place(card.rectTransform, 410, 205, 1100, 670);
        Text(card.transform, "You have guest progress", 54, Ink, true, TextAlignmentOptions.Center, 0, 44, 1100, 70);
        Text(card.transform, "This computer has progress that is not saved to any account.", 30, InkSoft, false, TextAlignmentOptions.Center, 40, 122, 1020, 44);

        string[] values = { puzzles.ToString(), words.ToString(), bestStreak == 1 ? "1 day" : bestStreak + " days" };
        string[] names = { "puzzles solved", "words learned", "best streak" };
        for (int i = 0; i < 3; i++)
        {
            Image box = LibraryUiKit.Box(card.transform, "stat", Sand, true);
            LibraryUiKit.Place(box.rectTransform, 70 + i * 326, 200, 306, 130);
            Text(box.transform, values[i], 50, Ink, true, TextAlignmentOptions.Center, 0, 18, 306, 60);
            Text(box.transform, names[i], 25, InkSoft, false, TextAlignmentOptions.Center, 0, 80, 306, 36);
        }

        Text(card.transform, "Add it to this account?", 36, Ink, true, TextAlignmentOptions.Center, 0, 366, 1100, 50);
        Text(card.transform, "If this is not your progress, choose Don't add. It stays on this computer.", 26, InkSoft, false, TextAlignmentOptions.Center, 40, 420, 1020, 40);

        Image dontOuter = LibraryUiKit.Box(card.transform, "dont_add", new Color32(207, 197, 178, 255), true);
        LibraryUiKit.Place(dontOuter.rectTransform, 156, 500, 380, 100);
        Image dontInner = LibraryUiKit.Box(dontOuter.transform, "inner", Cream, true);
        LibraryUiKit.Place(dontInner.rectTransform, 4, 4, 372, 92);
        Text(dontInner.transform, "Don't add", 36, Ink, true, TextAlignmentOptions.Center, 0, 0, 372, 92);
        LibraryUiKit.MakeButton(dontOuter, delegate { CloseGuestPrompt(); answer(false); });

        Image add = TrophyFont.Apply(LibraryUiKit.Pill(card.transform, "Add to account", 564, 500, 380, 100, Green, Color.white, 36, FontStyles.Bold));
        LibraryUiKit.MakeButton(add, delegate { CloseGuestPrompt(); answer(true); });
    }

    private void CloseGuestPrompt()
    {
        if (_guestPrompt != null)
        {
            Destroy(_guestPrompt);
            _guestPrompt = null;
        }
    }
}
