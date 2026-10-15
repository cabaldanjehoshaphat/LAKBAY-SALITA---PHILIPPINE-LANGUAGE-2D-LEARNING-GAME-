using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WordAudioPlayer
/// The object behind WordAudio: one AudioSource that survives scene changes plays the word recordings, and a small message that appears on
/// top of any screen says "No recording for this word yet" when a tap-to-hear button has no recording. It creates itself the first time
/// WordAudio needs it, so nothing has to be added to a scene.
/// </summary>
public class WordAudioPlayer : MonoBehaviour
{
    private static WordAudioPlayer instance;

    private AudioSource source;
    private GameObject toastRoot;
    private TMP_Text toastLabel;
    private Coroutine hideRoutine;

    /// <summary>The player (created on first use).</summary>
    public static WordAudioPlayer Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("WordAudioPlayer");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<WordAudioPlayer>();
                instance.Build();
            }
            return instance;
        }
    }

    private void Build()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 1f;

        var canvasGo = new GameObject("Toast", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var box = new GameObject("Box", typeof(RectTransform));
        box.transform.SetParent(canvasGo.transform, false);
        var rect = (RectTransform)box.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 70f);
        rect.sizeDelta = new Vector2(760f, 84f);
        var image = box.AddComponent<Image>();
        image.color = new Color(0.14f, 0.1f, 0.06f, 0.88f);
        image.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(box.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        toastLabel = textGo.AddComponent<TextMeshProUGUI>();
        toastLabel.fontSize = 34f;
        toastLabel.color = Color.white;
        toastLabel.alignment = TextAlignmentOptions.Center;
        toastLabel.raycastTarget = false;

        toastRoot = box;
        toastRoot.SetActive(false);
    }

    /// <summary>Plays a recording (a new tap cuts off the one still playing).</summary>
    public void PlayClip(AudioClip clip)
    {
        source.Stop();
        source.clip = clip;
        source.Play();
    }

    /// <summary>Shows a short message at the bottom of the screen for about two seconds.</summary>
    public void ShowMessage(string message)
    {
        toastLabel.text = message;
        toastRoot.SetActive(true);
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }
        hideRoutine = StartCoroutine(HideLater());
    }

    private IEnumerator HideLater()
    {
        yield return new WaitForSecondsRealtime(2f);
        toastRoot.SetActive(false);
        hideRoutine = null;
    }
}
