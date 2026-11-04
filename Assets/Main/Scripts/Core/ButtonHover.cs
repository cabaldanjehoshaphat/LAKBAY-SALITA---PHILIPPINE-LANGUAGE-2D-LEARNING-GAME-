using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ButtonHover
/// Gives every button of the menu screens a hover effect: when the mouse is over a usable Button (or Toggle) it grows a little and gets a soft
/// white glow, and it goes back to normal when the mouse leaves. It creates itself before the first scene loads (RuntimeInitializeOnLoadMethod),
/// survives scene loads and needs nothing placed in any scene, like SoundEffects. Like SoundEffects it asks the EventSystem what is under the
/// pointer, so buttons that a screen creates while it runs (Library rows, tabs, cards) get the effect without any setup.
/// It only works in these scenes (HoverScenes): the Main menu, the Profile, the Play screen (language hub), the Library, the three Phrasebook
/// scenes and the start screens of Wordle, Crossword and Word Search (topic cards, level arrows, Learn, Play, Random). The glow is a separate white image behind the button's text and icons, with the same sprite as the button, so no colour of the button
/// is ever changed (screens that colour their own buttons, like the language tabs, keep working). Size and look are the constants below.
/// </summary>
public class ButtonHover : MonoBehaviour
{
    private const float HoverScale = 1.04f;
    private const float GlowAlpha = 0.26f;
    private const float Speed = 14f;
    private const string GlowName = "HoverGlow";

    /// <summary>Scenes (part of the scene name) that get the hover effect.</summary>
    private static readonly string[] HoverScenes = { "Main_menu", "Profile", "Language_Selection", "Library", "Phrasebook", "Wordle - Start", "Crossword - Start", "WordSearch - Start" };

    private static ButtonHover _instance;
    private readonly List<RaycastResult> _hits = new List<RaycastResult>();
    private readonly Dictionary<Transform, Vector3> _baseScale = new Dictionary<Transform, Vector3>();
    private readonly List<Transform> _active = new List<Transform>();
    private Selectable _hovered;
    private Image _glow;
    private PointerEventData _pointer;
    private EventSystem _pointerSystem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }
        var go = new GameObject("ButtonHover");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ButtonHover>();
        SceneManager.sceneLoaded += delegate { _instance.Release(); };
    }

    private static bool SceneHasHover()
    {
        string name = SceneManager.GetActiveScene().name;
        foreach (string part in HoverScenes)
        {
            if (name.Contains(part))
            {
                return true;
            }
        }
        return false;
    }

    private void Update()
    {
        Selectable target = null;
        if (SceneHasHover() && EventSystem.current != null && Input.mousePresent)
        {
            target = FindUnderPointer();
        }
        if (target != _hovered)
        {
            Release();
            _hovered = target;
            if (_hovered != null)
            {
                Attach(_hovered);
            }
        }
        Animate();
    }

    /// <summary>The topmost usable Button or Toggle under the mouse (a button whose label or icon is hit counts too).</summary>
    private Selectable FindUnderPointer()
    {
        if (_pointer == null || _pointerSystem != EventSystem.current)
        {
            _pointerSystem = EventSystem.current;     // every scene may have its own EventSystem
            _pointer = new PointerEventData(_pointerSystem);
        }
        _pointer.position = Input.mousePosition;
        _hits.Clear();
        EventSystem.current.RaycastAll(_pointer, _hits);
        foreach (RaycastResult hit in _hits)
        {
            if (hit.gameObject == null || hit.gameObject.name == GlowName)
            {
                continue;
            }
            Selectable s = hit.gameObject.GetComponentInParent<Button>();
            if (s == null)
            {
                s = hit.gameObject.GetComponentInParent<Toggle>();
            }
            if (s != null && s.IsInteractable())
            {
                return s;
            }
            // anything else that is drawn on top (a panel, a dialog) blocks the buttons behind it
            if (hit.gameObject.GetComponentInParent<Selectable>() == null)
            {
                Graphic g = hit.gameObject.GetComponent<Graphic>();
                if (g != null && g.color.a > 0.5f && !(g is Text) && hit.gameObject.GetComponent<TMPro.TMP_Text>() == null)
                {
                    return null;
                }
            }
        }
        return null;
    }

    private void Attach(Selectable s)
    {
        Transform t = s.transform;
        if (!_baseScale.ContainsKey(t))
        {
            _baseScale[t] = t.localScale;
        }
        if (!_active.Contains(t))
        {
            _active.Add(t);
        }
        var target = s.targetGraphic as Image;
        if (target == null)
        {
            target = s.GetComponent<Image>();
        }
        if (target == null || target.sprite == null && target.color.a < 0.05f)
        {
            _glow = null;
            return;
        }
        Transform existing = t.Find(GlowName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(GlowName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(t, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetSiblingIndex(0);
        _glow = go.GetComponent<Image>();
        _glow.sprite = target.sprite;
        _glow.type = target.type;
        _glow.pixelsPerUnitMultiplier = target.pixelsPerUnitMultiplier;
        _glow.raycastTarget = false;
        _glow.color = new Color(1f, 1f, 1f, 0f);
        var ignore = go.GetComponent<LayoutElement>();
        if (ignore == null)
        {
            ignore = go.AddComponent<LayoutElement>();
        }
        ignore.ignoreLayout = true;
        _glowFade = 0f;
    }

    private float _glowFade;

    private void Animate()
    {
        float step = Time.unscaledDeltaTime * Speed;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Transform t = _active[i];
            if (t == null)
            {
                _active.RemoveAt(i);      // the button was destroyed (scene change)
                continue;
            }
            bool on = _hovered != null && _hovered.transform == t;
            Vector3 baseScale = _baseScale.ContainsKey(t) ? _baseScale[t] : Vector3.one;
            Vector3 goal = on ? baseScale * HoverScale : baseScale;
            t.localScale = Vector3.Lerp(t.localScale, goal, Mathf.Clamp01(step));
            if (!on && (t.localScale - baseScale).sqrMagnitude < 0.00001f)
            {
                t.localScale = baseScale;
                _active.RemoveAt(i);
                _baseScale.Remove(t);
            }
        }
        if (_glow != null)
        {
            _glowFade = Mathf.MoveTowards(_glowFade, 1f, step);
            _glow.color = new Color(1f, 1f, 1f, GlowAlpha * _glowFade);
        }
    }

    /// <summary>Takes the glow away from the button that was hovered; its size eases back in Animate.</summary>
    private void Release()
    {
        if (_glow != null)
        {
            _glow.color = new Color(1f, 1f, 1f, 0f);
            _glow = null;
        }
        _hovered = null;
    }
}
