using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// IntroCutscene
/// The 30 second intro that plays when the game starts, before the Main menu (scene "0 Intro", the first scene of the build). Everything is
/// built in code at start and animated by time, like the preview video:
///   0 - 3 s    the LAKBAY tiles flip in, the SALITA ribbon and the tag line appear, the boat sails across the sea;
///   3 - 24 s   Cebuano, Ilonggo and Tagalog, 7 s each: the map of the Philippines lights up the region, the name tiles flip, and three chibi
///              characters of that language greet each other (the lines are phrases of the Phrasebook: Good morning, How are you?, I am fine);
///   24 - 30 s  all nine characters dance, "Three languages. One journey.", then a fade into the Main menu.
/// Click or any key skips it at once. The sky comes from the scene (MainMenuTheme: it follows the clock like the other screens), the map, boat
/// and wave sprites are assigned in the scene by IntroSceneBuilder (Tools > Intro), and the chibi characters are puppets made of picture parts in
/// Resources/Intro/Characters/&lt;Name&gt;/ (back, body, body_front, arm_l, arm_r, head, eyes_open, eyes_closed, mouth_closed, mouth_open, front):
/// the head bobs, the eyes blink, the mouth moves, the arms wave, and the whole body hops and squashes. The music is the Main menu track
/// (BackgroundMusic), the sounds come from SoundEffects and a small pitched blip voice for each character.
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    [Header("Art (assigned by IntroSceneBuilder)")]
    [SerializeField] private RectTransform stage;
    [SerializeField] private Sprite mapSprite;
    [SerializeField] private Sprite boatSprite;
    [SerializeField] private Sprite gullSprite;
    [SerializeField] private Texture2D waveFillTexture;
    [SerializeField] private Texture2D waveCrestTexture;
    [Tooltip("The colourable text material of the menus (LiberationSans SDF Colorable), so text colours show as set.")]
    [SerializeField] private Material textMaterial;

    [Header("Flow")]
    [SerializeField] private string nextScene = "1 Main_menu";
    [SerializeField] private bool allowSkip = true;

    // ------------------------------------------------------------------ timeline
    private const float Duration = 30f;
    private const float LangLen = 7f;
    private const float FirstLang = 3f;
    private const float FinalStart = 24f;
    private const float LineStart = 1.8f;
    private const float LineStep = 1.6f;
    private const float LineLen = 1.45f;
    private const float MapScale = 0.651f;
    private static readonly Vector2 MapPos = new Vector2(40f, 36f);

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Cream2 = new Color32(239, 231, 212, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);
    private static readonly Color Soft = new Color32(107, 82, 55, 255);
    private static readonly Color Green = new Color32(93, 160, 42, 255);
    private static readonly Color Gold = new Color32(232, 185, 35, 255);
    private static readonly Color Navy = new Color32(30, 58, 138, 255);
    private static readonly Color TileGray = new Color32(185, 183, 172, 255);
    private static readonly Color[] TitleColors = { Green, Gold, Green, Green, Gold, Green };
    private static readonly Vector2[] Route = { new Vector2(705, 885), new Vector2(585, 810), new Vector2(410, 440) };   // Cebu, Panay, Manila (map picture pixels)

    private class Cast
    {
        public string name, folder, phrase, english;
        public float voice;
    }

    private class LangInfo
    {
        public string name, also, spoken, words;
        public Color color, letter;
        public Vector2[] marks;
        public Cast[] cast;
    }

    private class Tile
    {
        public RectTransform rt;
        public Image image;
        public TextMeshProUGUI label;
    }

    private class Puppet
    {
        public RectTransform root, headBack, headFront, armL, armR;
        public Image eyesOpen, eyesClosed, mouthClosed, mouthOpen;
    }

    private class TimedEvent
    {
        public float time;
        public Action act;
    }

    private LangInfo[] _langs;
    private float _start;
    private float _lastTime = -1f;
    private float _manualTime = -1f;
    private bool _finished;
    private AudioSource _voice;
    private AudioClip[] _taps;
    private readonly List<TimedEvent> _events = new List<TimedEvent>();

    // title
    private CanvasGroup _titleGroup, _ribbonGroup, _tagGroup;
    private RectTransform _boat, _gullA, _gullB;
    private RawImage _waveFillA, _waveFillB, _waveCrestA, _waveCrestB;
    private readonly Tile[] _titleTiles = new Tile[6];
    // map
    private CanvasGroup _mapGroup;
    private RectTransform _mapBoat;
    private Image _mapBoatImage;
    private readonly List<Image> _dashes = new List<Image>();      // 16 per leg, 2 legs
    private readonly List<Image> _markGlow = new List<Image>();
    private readonly List<Image> _markDot = new List<Image>();
    private readonly List<Image> _markFill = new List<Image>();
    private readonly List<int> _markLang = new List<int>();
    private readonly List<Vector2> _markPos = new List<Vector2>();
    // card
    private CanvasGroup _cardGroup, _infoGroup, _overGroup, _charsGroup;
    private RectTransform _card;
    private readonly Tile[] _langTiles = new Tile[7];
    private TextMeshProUGUI _alsoLabel, _spokenLabel, _chipWords, _chipSub, _subtitle;
    private Image _chip;
    private readonly RectTransform[] _tagRect = new RectTransform[3];
    private readonly TextMeshProUGUI[] _tagLabel = new TextMeshProUGUI[3];
    private readonly Image[] _tagImage = new Image[3];
    private RectTransform _bubble, _bubbleBody;
    private Image _bubbleOutline, _bubbleTailOutline;
    private TextMeshProUGUI _bubbleLabel;
    private readonly Puppet[] _puppets = new Puppet[9];
    private int _shownLang = -1;
    private int _shownLine = -1;
    // finale
    private CanvasGroup _finaleGroup;
    private readonly CanvasGroup[] _finalePills = new CanvasGroup[2];
    private readonly RectTransform[] _finalePillRect = new RectTransform[2];
    private readonly CanvasGroup[] _finaleChips = new CanvasGroup[3];
    // hint and fade
    private CanvasGroup _hintGroup;
    private Image _fade;

    // ------------------------------------------------------------------ helpers
    private static float Clamp01(float x) { return x < 0f ? 0f : (x > 1f ? 1f : x); }
    private static float Prog(float t, float a, float b) { return Clamp01((t - a) / (b - a)); }
    private static float EaseOut(float t) { return 1f - Mathf.Pow(1f - t, 3f); }
    private static float EaseIO(float t) { return 3f * t * t - 2f * t * t * t; }
    private static float EaseBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private RectTransform Mk(Transform parent, string name, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static void At(RectTransform rt, float cx, float cy)
    {
        rt.anchoredPosition = new Vector2(cx, -cy);
    }

    private Image Img(Transform parent, string name, float w, float h, Color color, Sprite sprite)
    {
        RectTransform rt = Mk(parent, name, w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Image Box(Transform parent, string name, float w, float h, Color color)
    {
        Image img = Img(parent, name, w, h, color, LibraryUiKit.Rounded);
        img.type = Image.Type.Sliced;
        return img;
    }

    private TextMeshProUGUI Txt(Transform parent, string text, float size, Color color, bool bold, TextAlignmentOptions align = TextAlignmentOptions.Center,
        float w = 900f, float h = 120f)
    {
        RectTransform rt = Mk(parent, "Text", w, h);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (textMaterial != null)
        {
            t.fontSharedMaterial = textMaterial;
        }
        return t;
    }

    /// <summary>Anchors a child in the middle of its parent (the other helpers anchor at the top-left corner).</summary>
    private static void CenterInParent(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private CanvasGroup Grp(RectTransform rt)
    {
        var cg = rt.gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
        return cg;
    }

    private RectTransform Layer(string name)
    {
        RectTransform rt = Mk(stage, name, 1920f, 1080f);
        At(rt, 960f, 540f);
        return rt;
    }

    private Tile MakeTile(Transform parent)
    {
        var tile = new Tile();
        tile.image = Box(parent, "Tile", 100f, 100f, TileGray);
        tile.rt = tile.image.rectTransform;
        tile.label = Txt(tile.rt, "", 60f, Color.white, true, TextAlignmentOptions.Center, 100f, 100f);
        CenterInParent(tile.label.rectTransform);
        return tile;
    }

    /// <summary>A Wordle style tile: gray at p = 0, flips over and shows its colour and letter at p = 1.</summary>
    private void SetTile(Tile tile, float cx, float cy, float size, string letter, float p, Color color, Color letterColor)
    {
        float sx = Mathf.Abs(Mathf.Cos(Mathf.PI * p));
        bool on = p >= 0.5f;
        tile.rt.sizeDelta = new Vector2(size, size);
        At(tile.rt, cx, cy);
        tile.rt.localScale = new Vector3(Mathf.Max(0.04f, sx), 1f, 1f);
        tile.image.color = on ? color : TileGray;
        tile.label.rectTransform.sizeDelta = new Vector2(size, size);
        tile.label.fontSize = size * 0.62f;
        tile.label.text = letter;
        tile.label.color = letterColor;
        tile.label.enabled = on;
    }

    // ------------------------------------------------------------------ data
    private static string PhraseOf(int lang, int topic, int index, string fallback, out string english, string fallbackEnglish)
    {
        PhraseEntry e = PhraseData.Phrase(lang, topic, index);
        if (e != null && !string.IsNullOrEmpty(e.phrase))
        {
            english = e.english;
            return e.phrase;
        }
        english = fallbackEnglish;
        return fallback;
    }

    private void BuildData()
    {
        string[][] names =
        {
            new[] { "Ana", "Jun", "Lola Nena" }, new[] { "Mara", "Bong", "Lolo Ben" }, new[] { "Maya", "Miguel", "Mang Tomas" }
        };
        float[][] voices = { new[] { 1.35f, 1.1f, 0.8f }, new[] { 1.45f, 1.15f, 0.7f }, new[] { 1.4f, 1.05f, 0.75f } };
        string[][] fallback =
        {
            new[] { "Maayong buntag", "Kumusta ka?", "Maayo ra ko" }, new[] { "Maayong aga", "Kamusta ka?", "Maayo man ako" },
            new[] { "Magandang umaga", "Kumusta ka?", "Mabuti ako" }
        };
        // the three lines of the chat are phrases 1, 4 and 5 of the Phrasebook topic Greetings
        int[] lineIndex = { 0, 3, 4 };
        string[] fallbackEnglish = { "Good morning", "How are you?", "I am fine" };
        _langs = new[]
        {
            new LangInfo { name = "Cebuano", also = "Also called Bisaya", spoken = "Spoken in Cebu, Bohol, Negros Oriental and much of Mindanao", words = "22,005 words",
                           color = new Color32(225, 98, 60, 255), letter = Color.white, marks = new[] { new Vector2(705, 885), new Vector2(830, 1130) } },
            new LangInfo { name = "Ilonggo", also = "Also called Hiligaynon", spoken = "Spoken in Iloilo, Negros Occidental and Guimaras", words = "15,223 words",
                           color = Gold, letter = Ink, marks = new[] { new Vector2(585, 810), new Vector2(620, 905) } },
            new LangInfo { name = "Tagalog", also = "The base of Filipino", spoken = "Spoken in Metro Manila and much of Luzon", words = "26,312 words",
                           color = new Color32(138, 68, 200, 255), letter = Color.white, marks = new[] { new Vector2(410, 440) } }
        };
        for (int l = 0; l < 3; l++)
        {
            _langs[l].cast = new Cast[3];
            for (int j = 0; j < 3; j++)
            {
                string english;
                string phrase = PhraseOf(l, 0, lineIndex[j], fallback[l][j], out english, fallbackEnglish[j]);
                if (j == 0)
                {
                    phrase += "!";
                    english += "!";
                }
                _langs[l].cast[j] = new Cast { name = names[l][j], folder = names[l][j].Replace(" ", "_"), phrase = phrase, english = english, voice = voices[l][j] };
            }
        }
    }

    // ------------------------------------------------------------------ build
    private void Start()
    {
        BuildData();
        if (stage == null)
        {
            Debug.LogError("IntroCutscene: no stage assigned.");
            return;
        }
        BuildTitle();
        BuildMap();
        BuildCard();
        BuildFinale();
        BuildHintAndFade();
        BuildAudio();
        _start = Time.unscaledTime;
        Tick(0f);
    }

    private void BuildTitle()
    {
        RectTransform root = Layer("Title");
        _titleGroup = Grp(root);
        // sea: two scrolling wave layers (the same wave pictures as the Main menu)
        _waveFillA = Wave(root, "wave_a_fill", waveFillTexture, new Color32(30, 98, 172, 255), 997f, 165f);
        _waveCrestA = Wave(root, "wave_a_crest", waveCrestTexture, new Color(0.92f, 0.96f, 1f, 0.6f), 997f, 165f);
        _waveFillB = Wave(root, "wave_b_fill", waveFillTexture, new Color32(22, 78, 146, 255), 1020f, 120f);
        _waveCrestB = Wave(root, "wave_b_crest", waveCrestTexture, new Color(0.92f, 0.96f, 1f, 0.45f), 1020f, 120f);
        _boat = Img(root, "boat", 180f, 180f, Color.white, boatSprite).rectTransform;
        _gullA = Img(root, "gull_a", 115f, 58f, Color.white, gullSprite).rectTransform;
        _gullB = Img(root, "gull_b", 115f, 58f, Color.white, gullSprite).rectTransform;
        for (int i = 0; i < 6; i++)
        {
            _titleTiles[i] = MakeTile(root);
        }
        RectTransform ribbon = Mk(root, "ribbon", 380f, 80f);
        At(ribbon, 960f, 600f);
        _ribbonGroup = Grp(ribbon);
        Image rb = Box(ribbon, "bg", 380f, 80f, Navy);
        At(rb.rectTransform, 190f, 40f);
        TextMeshProUGUI rl = Txt(ribbon, "SALITA", 52f, Color.white, true, TextAlignmentOptions.Center, 380f, 80f);
        At(rl.rectTransform, 190f, 40f);
        RectTransform tag = Mk(root, "tag", 560f, 70f);
        At(tag, 960f, 707f);
        _tagGroup = Grp(tag);
        Image tb = Box(tag, "bg", 560f, 70f, Cream);
        At(tb.rectTransform, 280f, 35f);
        TextMeshProUGUI tl = Txt(tag, "Journey through words", 40f, Ink, false, TextAlignmentOptions.Center, 560f, 70f);
        At(tl.rectTransform, 280f, 35f);
    }

    private RawImage Wave(Transform parent, string name, Texture2D texture, Color color, float cy, float h)
    {
        RectTransform rt = Mk(parent, name, 1920f, h);
        At(rt, 960f, cy);
        var raw = rt.gameObject.AddComponent<RawImage>();
        if (texture != null)
        {
            texture.wrapMode = TextureWrapMode.Repeat;
            raw.texture = texture;
        }
        raw.color = color;
        raw.uvRect = new Rect(0f, 0f, 3.75f, 1f);
        raw.raycastTarget = false;
        return raw;
    }

    private void BuildMap()
    {
        RectTransform root = Layer("Map");
        _mapGroup = Grp(root);
        Image map = Img(root, "map", 1024f * MapScale, 1536f * MapScale, Color.white, mapSprite);
        At(map.rectTransform, MapPos.x + 1024f * MapScale / 2f, MapPos.y + 1536f * MapScale / 2f);
        for (int i = 0; i < 32; i++)
        {
            _dashes.Add(Img(root, "dash", 20f, 6f, new Color(1f, 1f, 1f, 0.9f), LibraryUiKit.Rounded));
        }
        for (int l = 0; l < 3; l++)
        {
            foreach (Vector2 m in _langs[l].marks)
            {
                _markGlow.Add(Img(root, "glow", 60f, 60f, new Color(_langs[l].color.r, _langs[l].color.g, _langs[l].color.b, 0.43f), LibraryUiKit.Circle));
                _markDot.Add(Img(root, "ring", 38f, 38f, Color.white, LibraryUiKit.Circle));
                _markFill.Add(Img(root, "dot", 26f, 26f, _langs[l].color, LibraryUiKit.Circle));
                _markLang.Add(l);
                _markPos.Add(m);
            }
        }
        _mapBoatImage = Img(root, "boat", 52f, 52f, Color.white, boatSprite);
        _mapBoat = _mapBoatImage.rectTransform;
    }

    private static Vector2 Mp(Vector2 p)
    {
        return new Vector2(MapPos.x + p.x * MapScale, MapPos.y + p.y * MapScale);
    }

    private void BuildCard()
    {
        RectTransform cardRoot = Layer("Card");
        _cardGroup = Grp(cardRoot);
        _card = Box(cardRoot, "card", 1070f, 850f, Cream).rectTransform;
        for (int i = 0; i < 7; i++)
        {
            _langTiles[i] = MakeTile(cardRoot);
        }
        _alsoLabel = Txt(cardRoot, "", 32f, Soft, false, TextAlignmentOptions.Left, 600f, 50f);
        RectTransform infoRoot = Mk(cardRoot, "info", 1920f, 1080f);
        At(infoRoot, 960f, 540f);
        _infoGroup = Grp(infoRoot);
        _chip = Box(infoRoot, "chip", 250f, 90f, Gold);
        _chipWords = Txt(infoRoot, "", 34f, Ink, true, TextAlignmentOptions.Center, 250f, 44f);
        _chipSub = Txt(infoRoot, "in the Library", 22f, Ink, false, TextAlignmentOptions.Center, 250f, 30f);
        _spokenLabel = Txt(infoRoot, "", 32f, Ink, false, TextAlignmentOptions.Center, 970f, 50f);

        // the chibi characters stand on the card, above it
        RectTransform charsRoot = Layer("Characters");
        _charsGroup = Grp(charsRoot);
        string[] folders = { "Ana", "Jun", "Lola_Nena", "Mara", "Bong", "Lolo_Ben", "Maya", "Miguel", "Mang_Tomas" };
        for (int i = 0; i < 9; i++)
        {
            _puppets[i] = BuildPuppet(charsRoot, folders[i]);
        }

        // name tags, speech bubble and subtitle above the characters
        RectTransform overRoot = Layer("Over");
        _overGroup = Grp(overRoot);
        for (int j = 0; j < 3; j++)
        {
            _tagImage[j] = Box(overRoot, "tag", 160f, 44f, Gold);
            _tagRect[j] = _tagImage[j].rectTransform;
            _tagLabel[j] = Txt(_tagRect[j], "", 30f, Ink, true, TextAlignmentOptions.Center, 300f, 44f);
            _tagLabel[j].rectTransform.anchoredPosition = new Vector2(80f, -22f);
        }
        BuildBubble(overRoot);
        _subtitle = Txt(overRoot, "", 44f, Green, false, TextAlignmentOptions.Center, 1000f, 60f);
    }

    private void BuildBubble(Transform parent)
    {
        _bubble = Mk(parent, "bubble", 0f, 0f);         // a point: the tail tip, so the bubble pops out from the speaker
        _bubbleOutline = Box(_bubble, "outline", 400f, 96f, Gold);
        _bubbleTailOutline = Img(_bubble, "tail_outline", 40f, 40f, Gold, LibraryUiKit.Rounded);
        _bubbleTailOutline.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        _bubbleTailOutline.rectTransform.anchoredPosition = new Vector2(0f, -(-26f));
        Image inner = Box(_bubble, "inner", 388f, 84f, Color.white);
        Image tailInner = Img(_bubble, "tail_inner", 28f, 28f, Color.white, LibraryUiKit.Rounded);
        tailInner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        _bubbleBody = _bubbleOutline.rectTransform;
        _bubbleLabel = Txt(inner.rectTransform, "", 46f, Ink, true, TextAlignmentOptions.Center, 900f, 84f);
        CenterInParent(_bubbleLabel.rectTransform);
        // keep the references needed to resize
        _bubbleInner = inner.rectTransform;
        _bubbleTailInner = tailInner.rectTransform;
    }

    private RectTransform _bubbleInner, _bubbleTailInner;

    private Puppet BuildPuppet(Transform parent, string folder)
    {
        var p = new Puppet();
        p.root = Mk(parent, folder, 400f, 520f);
        p.root.pivot = new Vector2(0.5f, 0.035f);       // the feet
        p.headBack = StretchGroup(p.root, "head_back");
        Part(p.headBack, folder, "back");
        Part(p.root, folder, "body");
        Part(p.root, folder, "body_front");
        p.armL = Part(p.root, folder, "arm_l").rectTransform;
        p.armR = Part(p.root, folder, "arm_r").rectTransform;
        p.armL.pivot = new Vector2(134f / 400f, 1f - 298f / 520f);     // the shoulders
        p.armR.pivot = new Vector2(266f / 400f, 1f - 298f / 520f);
        p.headFront = StretchGroup(p.root, "head_front");
        Part(p.headFront, folder, "head");
        p.eyesOpen = Part(p.headFront, folder, "eyes_open");
        p.eyesClosed = Part(p.headFront, folder, "eyes_closed");
        p.mouthClosed = Part(p.headFront, folder, "mouth_closed");
        p.mouthOpen = Part(p.headFront, folder, "mouth_open");
        Part(p.headFront, folder, "front");
        p.root.gameObject.SetActive(false);
        return p;
    }

    private RectTransform StretchGroup(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private Image Part(Transform parent, string folder, string part)
    {
        var go = new GameObject(part, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = Resources.Load<Sprite>("Intro/Characters/" + folder + "/" + part);
        img.raycastTarget = false;
        return img;
    }

    private void BuildFinale()
    {
        RectTransform root = Layer("Finale");
        _finaleGroup = Grp(root);
        string[] lines = { "Three languages. One journey.", "Learn  ·  Play  ·  Speak" };
        float[] sizes = { 66f, 52f };
        for (int i = 0; i < 2; i++)
        {
            RectTransform pill = Mk(root, "pill", 1000f, i == 0 ? 100f : 84f);
            _finalePillRect[i] = pill;
            _finalePills[i] = Grp(pill);
            Image bg = Box(pill, "bg", 1000f, i == 0 ? 100f : 84f, new Color(Navy.r, Navy.g, Navy.b, 0.88f));
            At(bg.rectTransform, 500f, (i == 0 ? 100f : 84f) / 2f);
            TextMeshProUGUI t = Txt(pill, lines[i], sizes[i], Color.white, true, TextAlignmentOptions.Center, 1000f, 100f);
            At(t.rectTransform, 500f, (i == 0 ? 100f : 84f) / 2f);
            // fit the pill to the text
            float w = t.GetPreferredValues(lines[i]).x + 90f;
            pill.sizeDelta = new Vector2(w, pill.sizeDelta.y);
            bg.rectTransform.sizeDelta = new Vector2(w, pill.sizeDelta.y);
            At(bg.rectTransform, w / 2f, pill.sizeDelta.y / 2f);
            At(t.rectTransform, w / 2f, pill.sizeDelta.y / 2f);
        }
        for (int l = 0; l < 3; l++)
        {
            RectTransform chip = Mk(root, "chip", 250f, 70f);
            _finaleChips[l] = Grp(chip);
            Image bg = Box(chip, "bg", 250f, 70f, _langs[l].color);
            At(bg.rectTransform, 125f, 35f);
            TextMeshProUGUI t = Txt(chip, _langs[l].name, 40f, _langs[l].letter, true, TextAlignmentOptions.Center, 250f, 70f);
            At(t.rectTransform, 125f, 35f);
            At(chip, 915f, 575f + 175f * l - 70f);
        }
    }

    private void BuildHintAndFade()
    {
        RectTransform hint = Mk(stage, "skip_hint", 440f, 56f);
        At(hint, 1660f, 1028f);
        _hintGroup = Grp(hint);
        Image bg = Box(hint, "bg", 440f, 56f, new Color(10f / 255f, 20f / 255f, 45f / 255f, 0.6f));
        At(bg.rectTransform, 220f, 28f);
        TextMeshProUGUI t = Txt(hint, "Tap or press any key to skip", 30f, new Color(1f, 1f, 1f, 0.85f), false, TextAlignmentOptions.Center, 440f, 56f);
        At(t.rectTransform, 220f, 28f);
        RectTransform fade = Mk(stage, "fade", 1920f, 1080f);
        At(fade, 960f, 540f);
        _fade = fade.gameObject.AddComponent<Image>();
        _fade.color = Color.black;
        _fade.raycastTarget = false;
    }

    // ------------------------------------------------------------------ audio
    private void BuildAudio()
    {
        _voice = gameObject.AddComponent<AudioSource>();
        _voice.playOnAwake = false;
        _voice.spatialBlend = 0f;
        _taps = new[] { Resources.Load<AudioClip>("Audio SFX/key_tap_1"), Resources.Load<AudioClip>("Audio SFX/key_tap_2"), Resources.Load<AudioClip>("Audio SFX/key_tap_3") };
        for (int i = 0; i < 6; i++)
        {
            AddSfx(0.35f + 0.13f * i, "tile_flip", 0.5f);
        }
        AddSfx(2.6f, "ui_open", 0.45f);
        for (int l = 0; l < 3; l++)
        {
            float st = FirstLang + LangLen * l;
            AddSfx(st + 0.05f, "ui_open", 0.4f);
            for (int k = 0; k < 7; k++)
            {
                AddSfx(st + 0.45f + 0.12f * k, "tile_flip", 0.4f);
            }
            for (int j = 0; j < 3; j++)
            {
                AddSfx(st + 0.3f + 0.22f * j + 0.55f, "cw_cell_select", 0.5f);
                float ls = st + LineStart + LineStep * j;
                float voice = _langs[l].cast[j].voice;
                int blips = (int)(LineLen * 9f);
                for (int b = 0; b < blips; b += 2)
                {
                    int bb = b;
                    AddEvent(ls + b / 9f, delegate { Blip(1 + bb % 3, voice); });
                }
            }
        }
        for (int i = 0; i < 9; i++)
        {
            AddSfx(FinalStart + 0.1f + 0.11f * i + 0.55f, "cw_cell_select", 0.35f);
        }
        for (int b = 0; b < 16; b++)
        {
            int bb = b;
            AddEvent(FinalStart + 1f + b * 0.25f, delegate { Blip(1 + bb % 3, 1f + 0.12f * (bb % 4)); });
        }
        AddSfx(FinalStart + 0.6f, "tile_correct", 0.5f);
        AddSfx(28.6f, "win_jingle", 0.45f);
    }

    private void AddEvent(float time, Action act)
    {
        _events.Add(new TimedEvent { time = time, act = act });
    }

    private void AddSfx(float time, string clip, float volume)
    {
        AddEvent(time, delegate { SoundEffects.Play(clip, volume); });
    }

    private void Blip(int n, float pitch)
    {
        if (SoundEffects.Muted || _taps == null || _taps[n - 1] == null)
        {
            return;
        }
        _voice.pitch = pitch;
        _voice.PlayOneShot(_taps[n - 1], 0.5f);
    }

    // ------------------------------------------------------------------ run
    /// <summary>Jumps to a time of the cutscene and holds it there (used to check single frames); Seek(-1) plays on normally.</summary>
    public void Seek(float seconds)
    {
        _manualTime = seconds;
    }

    private void Update()
    {
        if (_finished || stage == null || _langs == null)
        {
            return;
        }
        if (allowSkip && _manualTime < 0f && Input.anyKeyDown)
        {
            Finish(true);
            return;
        }
        float t = _manualTime >= 0f ? _manualTime : Time.unscaledTime - _start;
        if (t >= Duration && _manualTime < 0f)
        {
            Finish(false);
            return;
        }
        FireEvents(t);
        Tick(t);
        _lastTime = t;
    }

    private void FireEvents(float t)
    {
        if (_manualTime >= 0f)
        {
            return;
        }
        foreach (TimedEvent e in _events)
        {
            if (e.time > _lastTime && e.time <= t)
            {
                e.act();
            }
        }
    }

    private void Finish(bool skipped)
    {
        _finished = true;
        SceneFade.To(nextScene, skipped ? 0.25f : 0f, 0.5f);
    }

    private void Tick(float t)
    {
        bool title = t < 3.1f;
        _titleGroup.gameObject.SetActive(title);
        _mapGroup.gameObject.SetActive(t >= 2.55f);
        bool lang = t >= FirstLang && t < FinalStart;
        _cardGroup.gameObject.SetActive(lang);
        _overGroup.gameObject.SetActive(lang);
        _infoGroup.gameObject.SetActive(lang);
        _charsGroup.gameObject.SetActive(t >= FirstLang);
        _finaleGroup.gameObject.SetActive(t >= FinalStart);
        if (title) TickTitle(t);
        if (t >= 2.55f) TickMap(t);
        if (lang) TickLanguage(t); else if (t >= FinalStart) TickFinale(t - FinalStart);
        _hintGroup.alpha = Prog(t, 0.8f, 1.3f) * (1f - Prog(t, 27.6f, 28.1f));
        float fade = 1f - Prog(t, 0f, 0.45f);
        if (t > 29.4f) fade = Prog(t, 29.4f, 29.9f);
        _fade.color = new Color(0f, 0f, 0f, fade);
    }

    // ------------------------------------------------------------------ title
    private void TickTitle(float t)
    {
        float a = 1f - Prog(t, 2.45f, 3f);
        _titleGroup.alpha = a;
        ScrollWave(_waveFillA, 0.05f);
        ScrollWave(_waveCrestA, 0.05f);
        ScrollWave(_waveFillB, -0.07f);
        ScrollWave(_waveCrestB, -0.07f);
        float bx = Mathf.Lerp(-260f, 1500f, EaseIO(Prog(t, 0f, 3f)));
        At(_boat, bx, 905f - 70f + 10f * Mathf.Sin(t * 3f) - 40f);
        _boat.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(t * 2.4f));
        At(_gullA, 1250f + 30f * Mathf.Sin(t), 190f + 14f * Mathf.Sin(t * 2f));
        At(_gullB, 1420f + 30f * Mathf.Sin(t + 1f), 120f + 14f * Mathf.Sin(t * 2f + 1f));
        const float s = 176f, gap = 22f;
        float x0 = (1920f - (6f * s + 5f * gap)) / 2f + s / 2f;
        string word = "LAKBAY";
        for (int i = 0; i < 6; i++)
        {
            float st = 0.25f + 0.13f * i;
            float p = Prog(t, st, st + 0.32f);
            float drop = (1f - EaseBack(Prog(t, st - 0.2f, st + 0.25f))) * -120f;
            Color letter = TitleColors[i] == Green ? Color.white : Ink;
            SetTile(_titleTiles[i], x0 + i * (s + gap), 430f + drop - 40f * EaseIO(Prog(t, 2.5f, 3f)), s, word[i].ToString(), p, TitleColors[i], letter);
        }
        _ribbonGroup.alpha = EaseOut(Prog(t, 1.3f, 1.75f));
        _tagGroup.alpha = EaseOut(Prog(t, 1.7f, 2.15f));
    }

    private static void ScrollWave(RawImage raw, float speed)
    {
        Rect r = raw.uvRect;
        r.x = Mathf.Repeat(r.x + speed * Time.unscaledDeltaTime, 1f);
        raw.uvRect = r;
    }

    // ------------------------------------------------------------------ map
    private void TickMap(float t)
    {
        float alpha = Prog(t, 2.55f, 3.25f) * (1f - Prog(t, 27.9f, 28.4f));
        _mapGroup.alpha = alpha;
        int shown;
        float route;
        if (t < FirstLang)
        {
            shown = -1;
            route = 0f;
        }
        else if (t < FinalStart)
        {
            shown = Mathf.Min(2, (int)((t - FirstLang) / LangLen));
            float local = (t - FirstLang) - LangLen * shown;
            route = shown > 0 ? (shown - 1) + EaseIO(Prog(local, 0f, 0.9f)) : 0f;
        }
        else
        {
            shown = 2;
            route = 2f;
        }
        // the dashed route: 16 dashes per leg
        for (int leg = 0; leg < 2; leg++)
        {
            float frac = Clamp01(route - leg);
            Vector2 a = Mp(Route[leg]), b = Mp(Route[leg + 1]);
            for (int j = 0; j < 16; j++)
            {
                Image d = _dashes[leg * 16 + j];
                float p0 = (j * 2f) / 32f, p1 = (j * 2f + 1f) / 32f;
                bool on = frac > 0f && p1 <= frac + 0.001f;
                d.enabled = on;
                if (!on) continue;
                Vector2 c0 = Vector2.Lerp(a, b, p0), c1 = Vector2.Lerp(a, b, p1);
                Vector2 mid = (c0 + c1) / 2f;
                d.rectTransform.sizeDelta = new Vector2(Vector2.Distance(c0, c1) + 1f, 6f);
                At(d.rectTransform, mid.x, mid.y);
                d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-(c1.y - c0.y), c1.x - c0.x) * Mathf.Rad2Deg);
            }
        }
        // markers of the languages introduced so far
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
        for (int i = 0; i < _markLang.Count; i++)
        {
            int l = _markLang[i];
            bool visible = l <= shown && shown >= 0;
            _markGlow[i].enabled = _markDot[i].enabled = _markFill[i].enabled = visible;
            if (!visible) continue;
            bool current = l == shown && t < FinalStart;
            float appear = t < FinalStart ? EaseBack(Prog(t, FirstLang + LangLen * l + (l > 0 ? 0.5f : 0.1f), FirstLang + LangLen * l + (l > 0 ? 0.9f : 0.5f))) : 1f;
            appear = Clamp01(appear);
            Vector2 c = Mp(_markPos[i]);
            float r = (22f + (current ? 10f * pulse : 0f)) * appear;
            _markGlow[i].rectTransform.sizeDelta = new Vector2((r + 8f) * 2f, (r + 8f) * 2f);
            _markDot[i].rectTransform.sizeDelta = new Vector2(42f * appear, 42f * appear);
            _markFill[i].rectTransform.sizeDelta = new Vector2(28f * appear, 28f * appear);
            At(_markGlow[i].rectTransform, c.x, c.y);
            At(_markDot[i].rectTransform, c.x, c.y);
            At(_markFill[i].rectTransform, c.x, c.y);
        }
        // the little boat travels along the route
        _mapBoatImage.enabled = shown >= 0;
        if (shown >= 0)
        {
            float rr = Mathf.Min(route, 2f);
            int k = Mathf.Min((int)rr, 1);
            float fr = rr < 2f ? rr - k : 1f;
            Vector2 p = Vector2.Lerp(Mp(Route[k]), Mp(Route[k + 1]), fr);
            At(_mapBoat, p.x, p.y - 34f + 5f * Mathf.Sin(t * 4f));
            _mapBoat.localRotation = Quaternion.Euler(0f, 0f, 5f * Mathf.Sin(t * 3f));
        }
    }

    // ------------------------------------------------------------------ the language scenes
    private void Pose(Puppet p, float k, float cx, float ground, float bob, bool blink, bool talking, float waveR, float waveL, float squash, float tilt, float hop, float look)
    {
        p.root.gameObject.SetActive(true);
        p.root.anchoredPosition = new Vector2(cx, -ground + hop * k);
        p.root.localScale = new Vector3(k / Mathf.Sqrt(squash), k * squash, 1f);
        p.root.localRotation = Quaternion.Euler(0f, 0f, tilt);
        p.headBack.anchoredPosition = p.headFront.anchoredPosition = new Vector2(look, -bob);
        p.eyesOpen.enabled = !blink;
        p.eyesClosed.enabled = blink;
        p.mouthOpen.enabled = talking;
        p.mouthClosed.enabled = !talking;
        p.armR.localRotation = Quaternion.Euler(0f, 0f, waveR * 155f);
        p.armL.localRotation = Quaternion.Euler(0f, 0f, -waveL * 155f);
    }

    private static bool Blink(float t, int j)
    {
        return ((t * 0.9f + j * 0.37f) % 3f) < 0.13f;
    }

    private void TickLanguage(float t)
    {
        int li = Mathf.Min(2, (int)((t - FirstLang) / LangLen));
        float lt = (t - FirstLang) - LangLen * li;
        LangInfo lg = _langs[li];
        const float cw = 1070f, chh = 850f;
        float slideIn = EaseOut(Prog(lt, 0f, 0.5f));
        float slideOut = EaseIO(Prog(lt, 6.5f, 7f));
        float off = (1f - slideIn) * 260f - slideOut * 260f;
        float a = slideIn * (1f - slideOut);
        float x = 790f + off, y = 110f;
        _cardGroup.alpha = a;
        _overGroup.alpha = a;
        _charsGroup.alpha = a;
        At(_card, x + cw / 2f, y + chh / 2f);

        if (_shownLang != li)
        {
            _shownLang = li;
            _shownLine = -1;
            _alsoLabel.text = lg.also;
            _spokenLabel.text = lg.spoken;
            _chipWords.text = lg.words;
            _chip.color = lg.color;
            _chipWords.color = lg.letter;
            _chipSub.color = lg.letter;
            for (int j = 0; j < 3; j++)
            {
                _tagLabel[j].text = lg.cast[j].name;
                _tagLabel[j].color = lg.letter;
                _tagImage[j].color = lg.color;
                float w = _tagLabel[j].GetPreferredValues(lg.cast[j].name).x + 44f;
                _tagRect[j].sizeDelta = new Vector2(w, 44f);
                _tagLabel[j].rectTransform.anchoredPosition = new Vector2(w / 2f, -22f);
            }
            for (int i = 0; i < 9; i++)
            {
                if (i / 3 != li) _puppets[i].root.gameObject.SetActive(false);
            }
            _spokenLabel.enableAutoSizing = true;
            _spokenLabel.fontSizeMin = 22f;
            _spokenLabel.fontSizeMax = 32f;
        }
        for (int k = 0; k < 7; k++)
        {
            float st = 0.4f + 0.12f * k;
            SetTile(_langTiles[k], x + 48f + 46f + k * 104f, y + 34f + 46f, 92f, lg.name.ToUpperInvariant()[k].ToString(), Prog(lt, st, st + 0.3f), lg.color, lg.letter);
        }
        At(_alsoLabel.rectTransform, x + 52f + 300f, y + 34f + 92f + 30f);
        _infoGroup.alpha = EaseOut(Prog(lt, 1.3f, 1.8f));
        At(_chip.rectTransform, x + cw - 165f, y + 85f);
        At(_chipWords.rectTransform, x + cw - 165f, y + 74f);
        At(_chipSub.rectTransform, x + cw - 165f, y + 106f);
        At(_spokenLabel.rectTransform, x + cw / 2f, y + 810f);

        int speaking = -1;
        for (int j = 0; j < 3; j++)
        {
            if (lt >= LineStart + LineStep * j && lt < LineStart + LineStep * j + LineLen + 0.15f) speaking = j;
        }
        float ground = y + 640f;
        float[] cxs = { x + 190f, x + 535f, x + 880f };
        const float k0 = 0.62f;
        for (int j = 0; j < 3; j++)
        {
            Puppet pup = _puppets[li * 3 + j];
            float st = 0.3f + 0.22f * j;
            if (lt < st)
            {
                pup.root.gameObject.SetActive(false);
                continue;
            }
            float pe = Prog(lt, st, st + 0.85f);
            float ex = (1f - EaseOut(pe)) * 520f + slideOut * 380f;
            float hop = pe < 1f ? 80f * Mathf.Abs(Mathf.Sin(Mathf.PI * pe * 2.2f)) * (1f - pe) : 0f;
            if (j == speaking) hop += 14f * Mathf.Abs(Mathf.Sin(Mathf.PI * (lt - LineStart - LineStep * j) * 3.2f));
            float sq = 1f + 0.10f * (hop / 80f) - (pe < 1f ? 0.10f * (1f - pe) * (1f - Mathf.Abs(Mathf.Sin(Mathf.PI * pe * 2.2f))) : 0f);
            bool talk = j == speaking && ((int)(lt * 9f) % 2 == 0);
            float wave = 0f;
            if (j == speaking && j == 0) wave = 0.8f + 0.2f * Mathf.Sin(lt * 15f);
            else if (j == speaking && j == 2) wave = 0.65f + 0.3f * Mathf.Sin(lt * 13f);
            float look = 0f;
            if (speaking >= 0 && speaking != j) look = 7f * (cxs[speaking] > cxs[j] ? 1f : -1f);
            float bob = 4f * Mathf.Sin(2f * Mathf.PI * (lt * 0.8f + j * 0.31f)) + ((j != speaking && speaking >= 0) ? 3f * Mathf.Sin(lt * 7f) : 0f);
            Pose(pup, k0, cxs[j] + ex, ground, bob, Blink(lt, j), talk, wave, 0f, sq, 2.2f * Mathf.Sin(lt * 1.3f + j) + (j == speaking ? 3f : 0f), hop, look);
        }

        // name tags
        for (int j = 0; j < 3; j++)
        {
            bool vis = lt >= 0.3f + 0.22f * j + 0.6f;
            _tagRect[j].gameObject.SetActive(vis);
            At(_tagRect[j], cxs[j], ground + 44f);
        }

        // speech bubble and subtitle
        int line = -1;
        for (int j = 0; j < 3; j++)
        {
            float ls = LineStart + LineStep * j;
            if (lt >= ls && lt < ls + LineStep && lt < 6.5f) line = j;
        }
        _bubble.gameObject.SetActive(line >= 0);
        _subtitle.gameObject.SetActive(line >= 0);
        if (line >= 0)
        {
            float ls = LineStart + LineStep * line;
            Cast c = lg.cast[line];
            if (_shownLine != line)
            {
                _shownLine = line;
                _bubbleLabel.text = c.phrase;
                float w = _bubbleLabel.GetPreferredValues(c.phrase).x + 70f;
                _bubbleOutline.color = lg.color;
                _bubbleTailOutline.color = lg.color;
                _bubbleOutline.rectTransform.sizeDelta = new Vector2(w, 96f);
                _bubbleInner.sizeDelta = new Vector2(w - 12f, 84f);
                _subtitle.text = c.name + " says: \"" + c.english + "\"";
            }
            float width = _bubbleOutline.rectTransform.sizeDelta.x;
            float bx = Mathf.Clamp(cxs[line], x + 40f + width / 2f, x + cw - 40f - width / 2f) - cxs[line];
            float pop = EaseBack(Prog(lt, ls, ls + 0.28f));
            float scale = pop < 1f ? Clamp01(pop) : 1f + 0.03f * Mathf.Sin((lt - ls) * 8f);
            // the bubble root's pivot is the tail tip; the body sits 26 px above it
            _bubble.anchoredPosition = new Vector2(cxs[line], -(ground - 340f));
            _bubble.localScale = new Vector3(scale, scale, 1f);
            _bubbleOutline.rectTransform.anchoredPosition = new Vector2(bx, 26f + 48f);
            _bubbleInner.anchoredPosition = new Vector2(bx, 26f + 48f);
            _bubbleTailOutline.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            _bubbleTailInner.anchoredPosition = new Vector2(0f, 24f);
            _bubbleLabel.enabled = scale > 0.85f;
            float sp = EaseOut(Prog(lt, ls + 0.1f, ls + 0.45f));
            _subtitle.color = new Color(Green.r, Green.g, Green.b, sp);
            At(_subtitle.rectTransform, x + cw / 2f, y + 746f + (1f - sp) * 14f);
        }
    }

    // ------------------------------------------------------------------ the dance
    private void TickFinale(float t)
    {
        float fade = 1f - Prog(t, 3.9f, 4.4f);
        _charsGroup.alpha = fade;
        _finaleGroup.alpha = fade;
        const float k = 0.34f;
        for (int r = 0; r < 3; r++)
        {
            float ground = 575f + 175f * r;
            for (int j = 0; j < 3; j++)
            {
                int idx = r * 3 + j;
                float st = 0.1f + 0.11f * idx;
                Puppet pup = _puppets[idx];
                if (t < st)
                {
                    pup.root.gameObject.SetActive(false);
                    continue;
                }
                float pe = Prog(t, st, st + 0.7f);
                float cx = 1190f + j * 215f + (1f - EaseOut(pe)) * 420f;
                float hop, sq;
                if (t < 0.9f)
                {
                    hop = 70f * Mathf.Abs(Mathf.Sin(Mathf.PI * pe * 2.2f)) * (1f - pe);
                    sq = 1f;
                }
                else
                {
                    float ph = (t * 2f + idx * 0.07f) % 1f;
                    hop = 46f * Mathf.Abs(Mathf.Sin(Mathf.PI * ph));
                    sq = 1f + 0.08f * (hop / 46f) - 0.12f * Mathf.Pow(1f - hop / 46f, 6f);
                }
                float wr = t > 0.9f ? Clamp01(0.55f + 0.45f * Mathf.Sin(t * 7f + idx)) : 0f;
                float wl = t > 0.9f ? Clamp01(0.55f + 0.45f * Mathf.Sin(t * 7f + idx + Mathf.PI)) : 0f;
                bool talk = t > 0.9f && ((int)(t * 5f + idx) % 2 == 0);
                Pose(pup, k, cx, ground, 3f * Mathf.Sin(t * 6f + idx), Blink(t, idx), talk, wr, wl, sq, 3.5f * Mathf.Sin(t * 4f + idx), hop, 0f);
            }
        }
        for (int i = 0; i < 2; i++)
        {
            float st = i == 0 ? 0.6f : 1.4f;
            float p = EaseOut(Prog(t, st, st + 0.45f));
            _finalePills[i].alpha = p;
            At(_finalePillRect[i], 1335f + (1f - p) * 40f, i == 0 ? 180f : 270f);
        }
        for (int l = 0; l < 3; l++)
        {
            float p = Clamp01(EaseBack(Prog(t, 0.7f + 0.12f * l, 1.15f + 0.12f * l)));
            _finaleChips[l].alpha = p;
            _finaleChips[l].transform.localScale = new Vector3(p, p, 1f);
        }
    }
}
