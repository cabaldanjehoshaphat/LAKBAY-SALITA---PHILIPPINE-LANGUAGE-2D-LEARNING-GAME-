using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SceneFade
/// Changes scene through a black fade: the picture fades to black, the next scene loads, and the picture fades back in. It creates its own
/// overlay (a screen-space canvas above everything) the first time it is used and survives scene loads, so nothing has to be placed in a scene.
/// Used by the intro cutscene to enter the Main menu; call SceneFade.To("scene name") from anywhere else for the same effect.
/// </summary>
public class SceneFade : MonoBehaviour
{
    private static SceneFade _instance;
    private Image _overlay;
    private bool _busy;

    /// <summary>Fades to black in fadeOut seconds (0 = already black), loads the scene, then fades in over fadeIn seconds.</summary>
    public static void To(string scene, float fadeOut = 0.35f, float fadeIn = 0.45f)
    {
        Ensure();
        if (_instance._busy)
        {
            return;
        }
        _instance.StartCoroutine(_instance.Run(scene, fadeOut, fadeIn));
    }

    private static void Ensure()
    {
        if (_instance != null)
        {
            return;
        }
        var go = new GameObject("SceneFade");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var imgGo = new GameObject("Black", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var image = imgGo.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        _instance = go.AddComponent<SceneFade>();
        _instance._overlay = image;
    }

    private IEnumerator Run(string scene, float fadeOut, float fadeIn)
    {
        _busy = true;
        yield return Fade(_overlay.color.a, 1f, fadeOut);
        SceneManager.LoadScene(scene);
        yield return null;
        yield return null;
        yield return Fade(1f, 0f, fadeIn);
        _busy = false;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            SetAlpha(to);
            yield break;
        }
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        _overlay.color = new Color(0f, 0f, 0f, a);
    }
}
