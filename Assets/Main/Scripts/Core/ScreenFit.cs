using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ScreenFit
/// Keeps the game the right size on any window or screen, and owns the display settings. It creates itself before the first scene loads
/// (RuntimeInitializeOnLoadMethod) and survives scene loads, so nothing has to be placed in a scene. It does three things:
///  1. Standard sizes: the window can be 800 x 600 (smallest), 1024 x 768 (the start size) or 1920 x 1080 (largest), see Presets. The
///     Settings window (gear on the Main menu) lets the player pick one of the three sizes and switch between Windowed and Fullscreen
///     through SetResolution and SetFullscreen; the choice is saved (SaveStore "Display.Resolution" and "Display.Fullscreen") and is
///     applied again the next time the game starts. A size that is bigger than the player's screen can not be chosen (PresetFits).
///  2. Window limits (Windows build, windowed mode): a window that is dragged outside 800 x 600 .. 1920 x 1080 snaps back to the nearest
///     allowed size after a moment. Pressing the maximize button of the window switches to Fullscreen (a full screen window at 1920 x 1080
///     on a 1080p screen); leaving full screen (Alt+Enter) returns to the window size that was chosen last.
///  3. Picture shape: every screen was designed for 16:9. In any other window shape (4:3 for example) all cameras draw into the biggest 16:9
///     rectangle that fits and the rest of the window is filled with a dark colour (bars on top and bottom, or left and right), so buttons and
///     cards never overlap or fall out of the picture.
/// The limits are the constants below. ClampSize and FitRect are public so the numbers can be checked without a window.
/// </summary>
public class ScreenFit : MonoBehaviour
{
    public const int MinWidth = 800;
    public const int MinHeight = 600;
    public const int MaxWidth = 1920;
    public const int MaxHeight = 1080;

    /// <summary>The standard window sizes, smallest to largest.</summary>
    public static readonly Vector2Int[] Presets =
    {
        new Vector2Int(800, 600), new Vector2Int(1024, 768), new Vector2Int(1920, 1080)
    };

    /// <summary>The size the game starts in: 1024 x 768 (index 1 of Presets).</summary>
    public const int DefaultPreset = 1;

    private const string ResolutionKey = "Display.Resolution";
    private const string FullscreenKey = "Display.Fullscreen";
    private const float ContentAspect = 16f / 9f;
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    private const float SnapDelaySeconds = 0.35f;
    private const float FullscreenRequestSeconds = 2f;

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr window);

    private enum WindowState { Normal, FullscreenRequested, InFullscreen }
#endif
    private static readonly Color BarColor = new Color32(16, 26, 50, 255);

    private static ScreenFit _instance;
    private Camera _barCamera;
    private int _lastWidth;
    private int _lastHeight;
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    private float _outsideSince = -1f;
    private WindowState _state = WindowState.Normal;
    private float _requestedAt;
#endif

    // ------------------------------------------------------------------ display settings (used by the Settings window)

    /// <summary>The window size chosen last: an index into Presets (1024 x 768 until the player chooses another).</summary>
    public static int ResolutionIndex
    {
        get { return Mathf.Clamp(SaveStore.GetInt(ResolutionKey, DefaultPreset), 0, Presets.Length - 1); }
    }

    /// <summary>True when the player chose Fullscreen (or maximized the window); false for Windowed.</summary>
    public static bool Fullscreen
    {
        get { return SaveStore.GetInt(FullscreenKey, 0) == 1; }
    }

    /// <summary>Whether a preset fits on the player's screen (a size bigger than the screen can not be chosen).</summary>
    public static bool PresetFits(int index)
    {
        if (index < 0 || index >= Presets.Length)
        {
            return false;
        }
        Resolution screen = Screen.currentResolution;
        return Presets[index].x <= screen.width && Presets[index].y <= screen.height;
    }

    /// <summary>Switches to Windowed mode at one of the standard sizes (index into Presets) and saves the choice.</summary>
    public static void SetResolution(int index)
    {
        SaveStore.SetInt(ResolutionKey, Mathf.Clamp(index, 0, Presets.Length - 1));
        SaveStore.SetInt(FullscreenKey, 0);
        SaveStore.Save();
        if (_instance != null)
        {
            _instance.ApplyDisplay();
        }
    }

    /// <summary>Switches between Fullscreen (true) and Windowed at the size chosen last (false) and saves the choice.</summary>
    public static void SetFullscreen(bool fullscreen)
    {
        SaveStore.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        SaveStore.Save();
        if (_instance != null)
        {
            _instance.ApplyDisplay();
        }
    }

    // ------------------------------------------------------------------ life cycle

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }
        var go = new GameObject("ScreenFit");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ScreenFit>();
    }

    private void Awake()
    {
        // a camera that only paints the bars: it draws first, clears the whole window and renders nothing
        var barGo = new GameObject("BarCamera");
        barGo.transform.SetParent(transform, false);
        _barCamera = barGo.AddComponent<Camera>();
        _barCamera.depth = -100f;
        _barCamera.clearFlags = CameraClearFlags.SolidColor;
        _barCamera.backgroundColor = BarColor;
        _barCamera.cullingMask = 0;
        _barCamera.allowHDR = false;
        _barCamera.allowMSAA = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SaveStore.HasKey(ResolutionKey) || SaveStore.HasKey(FullscreenKey))
        {
            ApplyDisplay();   // the display settings of the last session
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _lastWidth = -1;   // fit the cameras of the new scene on the next frame
    }

    private void Update()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight || _lastWidth == -1)
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            ApplyFit();
        }
        LimitWindow();
    }

    private void LateUpdate()
    {
        // cameras that a scene creates in its own Start get the same rectangle
        if (Time.frameCount % 30 == 0)
        {
            ApplyFit();
        }
    }

    // ------------------------------------------------------------------ picture shape

    /// <summary>The largest 16:9 rectangle that fits a window of this shape, as a camera viewport (0 to 1).</summary>
    public static Rect FitRect(float windowWidth, float windowHeight)
    {
        if (windowWidth <= 0f || windowHeight <= 0f)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }
        float windowAspect = windowWidth / windowHeight;
        if (windowAspect > ContentAspect)
        {
            float width = ContentAspect / windowAspect;      // window wider than 16:9: bars left and right
            return new Rect((1f - width) / 2f, 0f, width, 1f);
        }
        float height = windowAspect / ContentAspect;          // window taller than 16:9: bars on top and bottom
        return new Rect(0f, (1f - height) / 2f, 1f, height);
    }

    private void ApplyFit()
    {
        Rect rect = FitRect(Screen.width, Screen.height);
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == _barCamera || cam.targetTexture != null)
            {
                continue;
            }
            if (cam.rect != rect)
            {
                cam.rect = rect;
            }
        }
    }

    // ------------------------------------------------------------------ window size

    /// <summary>The window size limited to 800 x 600 .. 1920 x 1080.</summary>
    public static Vector2Int ClampSize(int width, int height)
    {
        return new Vector2Int(Mathf.Clamp(width, MinWidth, MaxWidth), Mathf.Clamp(height, MinHeight, MaxHeight));
    }

    /// <summary>
    /// Puts the window into the saved display mode: Fullscreen (a full screen window at 1920 x 1080) or Windowed at the saved standard size.
    /// Only a Windows build changes the window; in the editor the saved choice is just remembered.
    /// </summary>
    private void ApplyDisplay()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        _outsideSince = -1f;
        if (Fullscreen)
        {
            _state = WindowState.FullscreenRequested;
            _requestedAt = Time.unscaledTime;
            Screen.SetResolution(MaxWidth, MaxHeight, FullScreenMode.FullScreenWindow);
        }
        else
        {
            _state = WindowState.Normal;
            Vector2Int size = Presets[ResolutionIndex];
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }
#endif
    }

    /// <summary>
    /// Windows build only. Maximize: switches to Fullscreen (and saves it) and, when full screen is left again, returns to the saved
    /// window size. Otherwise puts a windowed window back inside the allowed sizes once it has been outside them for a moment.
    /// </summary>
    private void LimitWindow()
    {
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        bool windowed = Screen.fullScreenMode == FullScreenMode.Windowed;
        switch (_state)
        {
            case WindowState.FullscreenRequested:
                if (!windowed)
                {
                    _state = WindowState.InFullscreen;
                }
                else if (Time.unscaledTime - _requestedAt > FullscreenRequestSeconds)
                {
                    _state = WindowState.Normal;
                }
                return;
            case WindowState.InFullscreen:
                if (windowed)
                {
                    // full screen was left (Alt+Enter): back to the saved window size
                    SaveStore.SetInt(FullscreenKey, 0);
                    SaveStore.Save();
                    ApplyDisplay();
                }
                return;
        }

        if (!windowed)
        {
            _outsideSince = -1f;
            return;
        }
        if (IsZoomed(GetActiveWindow()))
        {
            // the maximize button was pressed
            SaveStore.SetInt(FullscreenKey, 1);
            SaveStore.Save();
            ApplyDisplay();
            return;
        }
        Vector2Int allowed = ClampSize(Screen.width, Screen.height);
        if (allowed.x == Screen.width && allowed.y == Screen.height)
        {
            _outsideSince = -1f;
            return;
        }
        if (_outsideSince < 0f)
        {
            _outsideSince = Time.unscaledTime;
        }
        else if (Time.unscaledTime - _outsideSince > SnapDelaySeconds)
        {
            _outsideSince = -1f;
            Screen.SetResolution(allowed.x, allowed.y, FullScreenMode.Windowed);
        }
#endif
    }
}
