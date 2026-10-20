using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LibraryController
/// Runs the Library (dictionary) menu scene. At Start it builds the whole screen in code on top of the
/// scene's existing background and back button, loads the three dictionary JSON files (assign them
/// below), then lets the player look up English words in Cebuano, Hiligaynon and Tagalog:
///  - language tabs (Cebuano / Hiligaynon / Tagalog / All three side by side),
///  - with the search box empty: a scrollable A-Z "notebook" list with an A-Z jump strip,
///  - with text typed: ranked search results (exact matches first, related words below), cross-language
///    suggestions when a language has no exact match, and "did you mean" spelling help,
///  - on the left: the opened word's details (meaning, definition, same word in the other languages),
///    word of the day, recent searches and favorites (stars are saved with SaveStore).
/// All data and search logic lives in LibraryData; list rendering in LibraryNotebook; shared widgets in
/// LibraryUiKit. Nothing here needs to be set up in the editor except the three TextAsset fields.
/// </summary>
public class LibraryController : MonoBehaviour
{
    [SerializeField] private TextAsset cebuanoJson;
    [SerializeField] private TextAsset hiligaynonJson;
    [SerializeField] private TextAsset tagalogJson;
    [Tooltip("Canvas to build the screen into. Leave empty to use the scene's 'library_menu' canvas.")]
    [SerializeField] private Canvas targetCanvas;

    private const float LX = 120f, LW = 560f, RX = 710f, RY = 290f, RW = 1090f, RH = 750f, SideH = 264f;
    private static readonly string[] TabNames = { "Cebuano", "Hiligaynon", "Tagalog", "All three" };

    private LibraryData _data;
    private RectTransform _ui;
    private int _lang;
    private string _query = string.Empty;
    private bool _showFavorites;
    private LibraryEntry _selected;
    private LibraryEntry _wotd;
    private bool _ready;
    private float _pendingTime = -1f;
    private bool _keepScroll;

    private TMP_InputField _input;
    private GameObject _clearButton;
    private TextMeshProUGUI _totalText, _resultCountText, _favText;
    private Image _favButton;
    private readonly Image[] _tabBg = new Image[4];
    private readonly TextMeshProUGUI[] _tabText = new TextMeshProUGUI[4];
    private Image _detailCard, _sideCard;
    private LibraryNotebook _notebook;
    private int _notebookLang = -1;
    private ScrollRect _resultsScroll;
    private RectTransform _resultsRoot, _resultsContent, _compareRoot, _hintRoot;
    private float _listY;
    private GameObject _loading;
    private TextMeshProUGUI _loadingText;
    private Image _loadingBar;

    private void ResolveCanvas()
    {
        if (targetCanvas == null)
        {
            GameObject go = GameObject.Find("library_menu");
            if (go != null) targetCanvas = go.GetComponent<Canvas>();
            if (targetCanvas == null) targetCanvas = FindObjectOfType<Canvas>();
        }
    }

    private void Start()
    {
        ResolveCanvas();
        _data = new LibraryData();
        BuildUi(false);
        StartCoroutine(LoadRoutine());
    }

    /// <summary>
    /// Builds the whole Library screen as real, saved scene objects right now, in Edit mode, filled with
    /// the real dictionary data - so the design is visible in the Scene/Game view before pressing Play.
    /// Run it again after changing the layout code (it deletes and rebuilds the "LibraryUI" object), then
    /// save the scene. At Play the screen simply re-attaches to these objects.
    /// </summary>
    [ContextMenu("Build Library UI (Edit Mode)")]
    public void BuildPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }
        ResolveCanvas();
        _data = new LibraryData();
        TextAsset[] files = { cebuanoJson, hiligaynonJson, tagalogJson };
        for (int l = 0; l < 3; l++)
        {
            if (files[l] != null) _data.Load(l, files[l].text);
        }
        _selected = null;
        _query = string.Empty;
        _showFavorites = false;
        _lang = 0;
        _notebookLang = -1;
        BuildUi(true);
        FinishLoad();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetCanvas);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    /// <summary>Set by the Wordle result card ("See in Library"): a word to search as soon as the Library opens.</summary>
    public static string PendingSearch;

    /// <summary>Tab to show for PendingSearch: 0 Cebuano, 1 Hiligaynon, 2 Tagalog, -1 leave as is.</summary>
    public static int PendingLanguage = -1;

    private void FinishLoad()
    {
        _ready = true;
        _wotd = _data.WordOfTheDay(DateTime.Today);
        _totalText.text = _data.Total.ToString("N0") + " words";
        RefreshFavButton();
        RefreshDetailCard();
        RefreshSideCard();
        ApplyView(false);

        if (Application.isPlaying && !string.IsNullOrEmpty(PendingSearch))
        {
            string query = PendingSearch;
            int lang = PendingLanguage;
            PendingSearch = null;
            PendingLanguage = -1;
            if (lang >= 0 && lang < 3)
            {
                SetLang(lang);
            }
            _input.text = query;
        }
    }

    private IEnumerator LoadRoutine()
    {
        TextAsset[] files = { cebuanoJson, hiligaynonJson, tagalogJson };
        for (int l = 0; l < 3; l++)
        {
            SetLoading(l / 3f, "Reading " + LibraryData.LanguageNames[l] + "...");
            yield return null;
            if (files[l] == null)
            {
                Debug.LogWarning("LibraryController: no JSON assigned for " + LibraryData.LanguageNames[l]);
                continue;
            }
            _data.Load(l, files[l].text);
        }
        SetLoading(1f, "Ready");
        yield return null;
        Destroy(_loading);
        FinishLoad();
    }

    private void SetLoading(float progress, string text)
    {
        if (_loadingText != null) _loadingText.text = text;
        if (_loadingBar != null) LibraryUiKit.Place(_loadingBar.rectTransform, 40, 190, 640 * Mathf.Clamp01(progress), 16);
    }

    // ================================================================== build

    /// <summary>Builds (or, at Play, re-attaches to) the screen. preview = Edit-mode build that replaces any
    /// existing "LibraryUI" object and skips the loading overlay.</summary>
    private void BuildUi(bool preview)
    {
        Transform existing = targetCanvas.transform.Find("LibraryUI");
        if (existing != null && preview)
        {
            existing.SetParent(null, false);
            LibraryUiKit.DestroyObject(existing.gameObject);
            existing = null;
        }
        if (existing != null)
        {
            _ui = (RectTransform)existing;
            LibraryUiKit.BeginReuse();
        }
        else
        {
            _ui = LibraryUiKit.NewRect("LibraryUI", targetCanvas.transform);
        }
        LibraryUiKit.Fill(_ui);
        _ui.SetSiblingIndex(1);

        Image titlePill = LibraryUiKit.Box(_ui, "TitlePill", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(titlePill.rectTransform, 190, 22, 250, 76);
        TextMeshProUGUI title = LibraryUiKit.Label(titlePill.transform, "Library", 52, LibraryUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
        LibraryUiKit.Fill(title.rectTransform);
        Image totalPill = LibraryUiKit.Box(_ui, "TotalPill", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(totalPill.rectTransform, 1320, 34, 240, 58);
        _totalText = LibraryUiKit.Label(totalPill.transform, "", 26, LibraryUiKit.Ink, FontStyles.Normal, TextAlignmentOptions.Center);
        LibraryUiKit.Fill(_totalText.rectTransform);

        _favButton = LibraryUiKit.Pill(_ui, "Favorites (0)", 1580, 34, 220, 58, new Color(1f, 1f, 1f, 0.6f), LibraryUiKit.Ink, 26, FontStyles.Bold);
        _favText = _favButton.GetComponentInChildren<TextMeshProUGUI>();
        LibraryUiKit.MakeButton(_favButton, ToggleFavoritesView);

        float tx = 190f;
        float[] widths = { 190f, 240f, 190f, 220f };
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            _tabBg[i] = LibraryUiKit.Pill(_ui, TabNames[i], tx, 112, widths[i], 62, Color.white, LibraryUiKit.Ink, 28, FontStyles.Bold);
            _tabText[i] = _tabBg[i].GetComponentInChildren<TextMeshProUGUI>();
            LibraryUiKit.MakeButton(_tabBg[i], delegate { SetLang(index); });
            tx += widths[i] + 12f;
        }

        BuildSearchBar();

        _detailCard = LibraryUiKit.Box(_ui, "DetailCard", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(_detailCard.rectTransform, LX, RY, LW, 470);
        _sideCard = LibraryUiKit.Box(_ui, "SideCard", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(_sideCard.rectTransform, LX, RY + 486, LW, SideH);

        _notebook = LibraryNotebook.Create(_ui, RX, RY, RW, RH);
        _notebook.OnSelect = OpenEntry;
        _notebook.OnToggleFavorite = ToggleFavorite;
        _notebook.IsFavorite = delegate (LibraryEntry e) { return _data.IsFavorite(e); };

        _resultsScroll = LibraryUiKit.MakeScroll(_ui, "Results", RX, RY, RW, RH, out _resultsContent);
        _resultsRoot = (RectTransform)_resultsScroll.transform;
        _compareRoot = LibraryUiKit.NewRect("Compare", _ui);
        LibraryUiKit.Place(_compareRoot, RX, RY, RW, RH);

        Image hint = LibraryUiKit.Box(_ui, "Hint", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(hint.rectTransform, RX, RY, RW, RH);
        _hintRoot = hint.rectTransform;
        TextMeshProUGUI h1 = LibraryUiKit.Label(hint.transform, "Compare a word in all three languages", 44, LibraryUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.TopLeft, true);
        LibraryUiKit.Place(h1.rectTransform, 50, 60, RW - 100, 110);
        TextMeshProUGUI h2 = LibraryUiKit.Label(hint.transform, "Type an English word in the search box to see its Cebuano, Hiligaynon and Tagalog words side by side.\n\nTry: house, water, mother, eat, friend.", 32, LibraryUiKit.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
        LibraryUiKit.Place(h2.rectTransform, 50, 190, RW - 100, 300);

        LibraryUiKit.EndReuse();

        if (!preview)
        {
            Image block = LibraryUiKit.Box(_ui, "Loading", new Color(0.23f, 0.16f, 0.1f, 0.35f), false);
            LibraryUiKit.Fill(block.rectTransform);
            block.raycastTarget = true;
            _loading = block.gameObject;
            Image card = LibraryUiKit.Box(block.transform, "LoadingCard", LibraryUiKit.Paper, true);
            LibraryUiKit.Place(card.rectTransform, 560, 380, 720, 260);
            TextMeshProUGUI lt = LibraryUiKit.Label(card.transform, "Preparing your dictionary", 42, LibraryUiKit.Ink, FontStyles.Bold);
            LibraryUiKit.Place(lt.rectTransform, 40, 36, 640, 60);
            _loadingText = LibraryUiKit.Label(card.transform, "Starting...", 28, LibraryUiKit.InkSoft);
            LibraryUiKit.Place(_loadingText.rectTransform, 40, 110, 640, 44);
            Image track = LibraryUiKit.Box(card.transform, "Track", LibraryUiKit.Line, true);
            LibraryUiKit.Place(track.rectTransform, 40, 190, 640, 16);
            _loadingBar = LibraryUiKit.Box(card.transform, "Bar", LibraryUiKit.Accent, true);
            LibraryUiKit.Place(_loadingBar.rectTransform, 40, 190, 1, 16);
        }

        RefreshTabs();
        HideRight();
    }

    private void BuildSearchBar()
    {
        Image bg = LibraryUiKit.Box(_ui, "SearchBox", Color.white, true);
        LibraryUiKit.Place(bg.rectTransform, 190, 196, 1610, 80);
        bg.raycastTarget = true;

        RectTransform area = LibraryUiKit.NewRect("TextArea", bg.transform);
        LibraryUiKit.Fill(area);
        area.offsetMin = new Vector2(32, 6);
        area.offsetMax = new Vector2(-360, -6);
        LibraryUiKit.GetOrAdd<RectMask2D>(area.gameObject);

        TextMeshProUGUI placeholder = LibraryUiKit.Label(area, "Search English or native words, for example: house or ama", 32, new Color32(155, 132, 104, 255), FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
        LibraryUiKit.Fill(placeholder.rectTransform);
        TextMeshProUGUI text = LibraryUiKit.Label(area, "", 34, LibraryUiKit.Ink, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        text.overflowMode = TextOverflowModes.Overflow;
        LibraryUiKit.Fill(text.rectTransform);

        _input = LibraryUiKit.GetOrAdd<TMP_InputField>(bg.gameObject);
        _input.textViewport = area;
        _input.textComponent = text;
        _input.placeholder = placeholder;
        _input.targetGraphic = bg;
        _input.lineType = TMP_InputField.LineType.SingleLine;
        _input.customCaretColor = true;
        _input.caretColor = LibraryUiKit.Ink;
        _input.caretWidth = 3;
        _input.onValueChanged.AddListener(OnQueryChanged);
        _input.onEndEdit.AddListener(OnEndEdit);

        _resultCountText = LibraryUiKit.Label(bg.transform, "", 24, new Color32(155, 132, 104, 255), FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        LibraryUiKit.Place(_resultCountText.rectTransform, 1170, 0, 320, 80);

        Image clear = LibraryUiKit.Icon(bg.transform, "Clear", LibraryUiKit.Circle, LibraryUiKit.Line);
        LibraryUiKit.Place(clear.rectTransform, 1536, 16, 48, 48);
        TextMeshProUGUI x = LibraryUiKit.Label(clear.transform, "x", 30, LibraryUiKit.InkSoft, FontStyles.Bold, TextAlignmentOptions.Center);
        LibraryUiKit.Fill(x.rectTransform);
        LibraryUiKit.MakeButton(clear, delegate { _input.text = string.Empty; _pendingTime = 0f; });
        _clearButton = clear.gameObject;
        _clearButton.SetActive(false);
    }

    // ================================================================ updates

    private void Update()
    {
        if (_pendingTime >= 0f && Time.unscaledTime >= _pendingTime)
        {
            _pendingTime = -1f;
            ApplyView(false);
        }
    }

    private void OnQueryChanged(string s)
    {
        _query = s ?? string.Empty;
        _clearButton.SetActive(_query.Length > 0);
        if (_showFavorites && _query.Length > 0)
        {
            _showFavorites = false;
            RefreshFavButton();
        }
        _pendingTime = Time.unscaledTime + 0.15f;
    }

    private void OnEndEdit(string s)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            _data.AddRecent(LibraryData.NormalizeQuery(s));
            RefreshSideCard();
            _pendingTime = 0f;
        }
    }

    private void SetLang(int lang)
    {
        _lang = lang;
        _showFavorites = false;
        RefreshTabs();
        RefreshFavButton();
        ApplyView(false);
    }

    private void ToggleFavoritesView()
    {
        if (!_ready) return;
        _showFavorites = !_showFavorites;
        RefreshFavButton();
        ApplyView(false);
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < 4; i++)
        {
            bool on = i == _lang && !_showFavorites;
            _tabBg[i].color = on ? LibraryUiKit.Ink : new Color(1f, 1f, 1f, 0.6f);
            _tabText[i].color = on ? Color.white : LibraryUiKit.Ink;
        }
    }

    private void RefreshFavButton()
    {
        if (_data == null) return;
        _favText.text = _showFavorites ? "Back to words" : "Favorites (" + _data.Favorites().Count + ")";
        _favButton.color = _showFavorites ? LibraryUiKit.Accent : new Color(1f, 1f, 1f, 0.6f);
        RefreshTabs();
    }

    private void HideRight()
    {
        _notebook.Root.SetActive(false);
        _resultsRoot.gameObject.SetActive(false);
        _compareRoot.gameObject.SetActive(false);
        _hintRoot.gameObject.SetActive(false);
    }

    private void ApplyView(bool keepScroll)
    {
        if (!_ready) return;
        _keepScroll = keepScroll;
        HideRight();
        string q = LibraryData.NormalizeQuery(_query);
        if (_showFavorites)
        {
            ShowFavorites();
        }
        else if (q.Length == 0)
        {
            if (_lang < 3) ShowNotebook();
            else
            {
                _hintRoot.gameObject.SetActive(true);
                _resultCountText.text = string.Empty;
            }
        }
        else if (_lang < 3)
        {
            ShowResults(q);
        }
        else
        {
            ShowCompare(q);
        }
    }

    // ================================================================ views

    private void ShowNotebook()
    {
        if (_notebookLang != _lang)
        {
            _notebook.SetData(_data.Entries[_lang]);
            _notebookLang = _lang;
        }
        _notebook.SetSelected(_selected != null && _selected.Lang == _lang ? _selected : null);
        _notebook.Root.SetActive(true);
        _resultCountText.text = _data.Entries[_lang].Count.ToString("N0") + " words";
    }

    private void ShowResults(string q)
    {
        LibraryData.SearchResult r = _data.Search(_lang, q, 30, 40);
        _resultsRoot.gameObject.SetActive(true);
        BeginList();
        if (r.Count == 0)
        {
            string s = _data.Suggest(q);
            if (s != null) AddSuggestion(s);
        }
        if (r.Exact.Count > 0)
        {
            AddHeading("Exact match");
            foreach (LibraryEntry e in r.Exact) AddCard(e, false);
        }
        else if (r.Count > 0)
        {
            AddInfo("No exact match for \"" + q + "\" in " + LibraryData.LanguageNames[_lang], "These words mention it:");
        }
        if (r.Related.Count > 0)
        {
            AddHeading("Related");
            foreach (LibraryEntry e in r.Related) AddCard(e, false);
        }
        int others = 0;
        if (r.Exact.Count == 0)
        {
            for (int l = 0; l < 3; l++)
            {
                if (l == _lang) continue;
                LibraryData.SearchResult o = _data.Search(l, q, 2, 0);
                if (o.Exact.Count == 0) continue;
                if (others == 0) AddHeading("Try another language");
                foreach (LibraryEntry e in o.Exact) { AddCard(e, true); others++; }
            }
        }
        if (r.Count == 0 && others == 0 && _data.Suggest(q) == null)
        {
            AddInfo("No words match \"" + q + "\"", "Try a different English word.");
        }
        EndList();
        _resultCountText.text = r.Count + (r.Count == 1 ? " result" : " results");
        _resultsRoot.gameObject.SetActive(true);
    }

    private void ShowFavorites()
    {
        List<LibraryEntry> favs = _data.Favorites();
        _resultsRoot.gameObject.SetActive(true);
        BeginList();
        AddHeading("Favorites");
        if (favs.Count == 0)
        {
            AddInfo("No favorites yet", "Tap the star next to a word to save it here.");
        }
        foreach (LibraryEntry e in favs) AddCard(e, true);
        EndList();
        _resultCountText.text = favs.Count + (favs.Count == 1 ? " word" : " words");
        _resultsRoot.gameObject.SetActive(true);
    }

    private void ShowCompare(string q)
    {
        ClearChildren(_compareRoot);
        float colW = (RW - 28f) / 3f;
        var langsOf = new Dictionary<string, List<int>>();
        int total = 0;
        for (int l = 0; l < 3; l++)
        {
            LibraryData.SearchResult r = _data.Search(l, q, 3, 3);
            var list = new List<LibraryEntry>(r.Exact);
            foreach (LibraryEntry e in r.Related) { if (list.Count >= 3) break; list.Add(e); }
            total += list.Count;

            Image col = LibraryUiKit.Box(_compareRoot, "Col", LibraryUiKit.CardTint, true);
            LibraryUiKit.Place(col.rectTransform, l * (colW + 14f), 0, colW, 590);
            Image pill = LibraryUiKit.Pill(col.transform, LibraryData.LanguageNames[l], 18, 18, 170, 38, LibraryUiKit.LangColors[l], Color.white, 22, FontStyles.Bold);
            if (list.Count == 0)
            {
                TextMeshProUGUI none = LibraryUiKit.Label(col.transform, "No match in " + LibraryData.LanguageNames[l], 28, LibraryUiKit.InkSoft, FontStyles.Italic, TextAlignmentOptions.TopLeft, true);
                LibraryUiKit.Place(none.rectTransform, 22, 78, colW - 44, 80);
            }
            for (int i = 0; i < list.Count; i++)
            {
                LibraryEntry e = list[i];
                float y = 72f + i * 170f;
                Image block = LibraryUiKit.Box(col.transform, "Block", new Color(1f, 1f, 1f, 0f), true);
                LibraryUiKit.Place(block.rectTransform, 8, y, colW - 16, 160);
                LibraryUiKit.MakeButton(block, delegate { OpenEntry(e); });
                TextMeshProUGUI w = LibraryUiKit.Label(block.transform, e.Word, 44, LibraryUiKit.Ink, FontStyles.Bold);
                LibraryUiKit.Place(w.rectTransform, 14, 6, colW - 90, 56);
                TextMeshProUGUI m = LibraryUiKit.Label(block.transform, e.Meaning, 26, LibraryUiKit.Ink);
                LibraryUiKit.Place(m.rectTransform, 14, 62, colW - 44, 34);
                TextMeshProUGUI d = LibraryUiKit.Label(block.transform, e.Definition, 21, LibraryUiKit.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
                LibraryUiKit.Place(d.rectTransform, 14, 98, colW - 44, 56);
                Image star = LibraryUiKit.Icon(block.transform, "Star", LibraryUiKit.Star, _data.IsFavorite(e) ? LibraryUiKit.Accent : LibraryUiKit.StarOff);
                LibraryUiKit.Place(star.rectTransform, colW - 72, 12, 40, 40);
                LibraryUiKit.MakeButton(star, delegate { ToggleFavorite(e); });
                if (r.Exact.Contains(e))
                {
                    List<int> set;
                    if (!langsOf.TryGetValue(e.Raw, out set)) { set = new List<int>(); langsOf[e.Raw] = set; }
                    if (!set.Contains(l)) set.Add(l);
                }
            }
        }

        string note = "These are different words in each language.";
        foreach (KeyValuePair<string, List<int>> kv in langsOf)
        {
            if (kv.Value.Count < 2) continue;
            var names = new List<string>();
            foreach (int l in kv.Value) names.Add(LibraryData.LanguageNames[l]);
            string word = kv.Key[0] + kv.Key.Substring(1).ToLowerInvariant();
            note = word + " appears in " + string.Join(kv.Value.Count == 2 ? " and " : ", ", names.ToArray()) + ".";
            break;
        }
        Image noteBox = LibraryUiKit.Box(_compareRoot, "Note", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(noteBox.rectTransform, 0, 606, RW, 80);
        TextMeshProUGUI nt = LibraryUiKit.Label(noteBox.transform, note, 30, LibraryUiKit.Ink, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        LibraryUiKit.Place(nt.rectTransform, 28, 0, RW - 56, 80);

        _resultCountText.text = total + (total == 1 ? " word" : " words");
        _compareRoot.gameObject.SetActive(true);
    }

    // ------------------------------------------------------------ list builder

    private void BeginList()
    {
        ClearChildren(_resultsContent);
        _listY = 0f;
    }

    private void EndList()
    {
        float keep = _resultsContent.anchoredPosition.y;
        _resultsContent.sizeDelta = new Vector2(0f, _listY + 20f);
        float max = Mathf.Max(0f, _listY + 20f - RH);
        _resultsContent.anchoredPosition = new Vector2(0f, _keepScroll ? Mathf.Clamp(keep, 0f, max) : 0f);
    }

    private void AddHeading(string text)
    {
        TextMeshProUGUI probe = LibraryUiKit.Label(_resultsContent, text, 28, LibraryUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
        float w = probe.preferredWidth + 40f;
        Image pill = LibraryUiKit.Box(_resultsContent, "Heading", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(pill.rectTransform, 0, _listY, w, 42);
        probe.transform.SetParent(pill.transform, false);
        LibraryUiKit.Fill(probe.rectTransform);
        _listY += 52f;
    }

    private void AddInfo(string title, string sub)
    {
        Image card = LibraryUiKit.Box(_resultsContent, "Info", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(card.rectTransform, 0, _listY, RW - 12, 100);
        TextMeshProUGUI a = LibraryUiKit.Label(card.transform, title, 32, LibraryUiKit.Ink, FontStyles.Bold);
        LibraryUiKit.Place(a.rectTransform, 24, 12, RW - 60, 42);
        TextMeshProUGUI b = LibraryUiKit.Label(card.transform, sub, 26, LibraryUiKit.InkSoft);
        LibraryUiKit.Place(b.rectTransform, 24, 56, RW - 60, 36);
        _listY += 112f;
    }

    private void AddSuggestion(string word)
    {
        Image card = LibraryUiKit.Box(_resultsContent, "Suggest", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(card.rectTransform, 0, _listY, RW - 12, 100);
        TextMeshProUGUI a = LibraryUiKit.Label(card.transform, "Did you mean \"" + word + "\"?", 34, LibraryUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        LibraryUiKit.Place(a.rectTransform, 24, 0, 640, 100);
        Image btn = LibraryUiKit.Pill(card.transform, "Search " + word, RW - 12 - 330, 26, 300, 48, LibraryUiKit.Ink, Color.white, 26, FontStyles.Bold);
        LibraryUiKit.MakeButton(btn, delegate { _input.text = word; _pendingTime = 0f; });
        _listY += 112f;
    }

    private void AddCard(LibraryEntry e, bool showLang)
    {
        float cw = RW - 12f;
        Image card = LibraryUiKit.Box(_resultsContent, "Card", LibraryUiKit.CardTint, true);
        LibraryUiKit.Place(card.rectTransform, 0, _listY, cw, 104);
        LibraryUiKit.MakeButton(card, delegate { OpenEntry(e); });

        TextMeshProUGUI w = LibraryUiKit.Label(card.transform, e.Word, 42, LibraryUiKit.Ink, FontStyles.Bold);
        LibraryUiKit.Place(w.rectTransform, 24, showLang ? 8 : 24, 340, 52);
        if (showLang)
        {
            LibraryUiKit.Pill(card.transform, LibraryData.LanguageNames[e.Lang], 24, 64, 150, 30, LibraryUiKit.LangColors[e.Lang], Color.white, 19, FontStyles.Bold);
        }
        TextMeshProUGUI m = LibraryUiKit.Label(card.transform, e.Meaning, 30, e.HasMeaning ? LibraryUiKit.Ink : LibraryUiKit.StarOff);
        LibraryUiKit.Place(m.rectTransform, 380, 14, cw - 380 - 110, 42);
        TextMeshProUGUI d = LibraryUiKit.Label(card.transform, e.Definition, 22, LibraryUiKit.InkSoft);
        LibraryUiKit.Place(d.rectTransform, 380, 58, cw - 380 - 110, 36);

        Image star = LibraryUiKit.Icon(card.transform, "Star", LibraryUiKit.Star, _data.IsFavorite(e) ? LibraryUiKit.Accent : LibraryUiKit.StarOff);
        LibraryUiKit.Place(star.rectTransform, cw - 76, 28, 48, 48);
        LibraryUiKit.MakeButton(star, delegate { ToggleFavorite(e); });
        _listY += 114f;
    }

    // ---------------------------------------------------------- left column

    private void RefreshDetailCard()
    {
        ClearChildren(_detailCard.transform);
        LibraryEntry e = _selected ?? _wotd;
        if (e == null) return;
        Transform t = _detailCard.transform;
        TextMeshProUGUI label = LibraryUiKit.Label(t, _selected == null ? "Word of the day" : "Opened word", 24, LibraryUiKit.InkSoft);
        LibraryUiKit.Place(label.rectTransform, 24, 16, 300, 32);

        Image star = LibraryUiKit.Icon(t, "Star", LibraryUiKit.Star, _data.IsFavorite(e) ? LibraryUiKit.Accent : LibraryUiKit.StarOff);
        LibraryUiKit.Place(star.rectTransform, LW - 78, 10, 46, 46);
        LibraryUiKit.MakeButton(star, delegate { ToggleFavorite(e); });

        TextMeshProUGUI word = LibraryUiKit.Label(t, e.Word, 66, LibraryUiKit.Ink, FontStyles.Bold);
        LibraryUiKit.Place(word.rectTransform, 24, 48, LW - 48, 80);
        LibraryUiKit.Pill(t, LibraryData.LanguageNames[e.Lang], 24, 132, 150, 34, LibraryUiKit.LangColors[e.Lang], Color.white, 20, FontStyles.Bold);
        TextMeshProUGUI letters = LibraryUiKit.Label(t, e.Raw.Length + " letters", 22, LibraryUiKit.InkSoft, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        LibraryUiKit.Place(letters.rectTransform, 190, 132, 200, 34);

        TextMeshProUGUI meaning = LibraryUiKit.Label(t, e.Meaning, 34, LibraryUiKit.Ink);
        LibraryUiKit.Place(meaning.rectTransform, 24, 176, LW - 48, 48);
        TextMeshProUGUI def = LibraryUiKit.Label(t, e.Definition, 24, LibraryUiKit.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
        LibraryUiKit.Place(def.rectTransform, 24, 226, LW - 48, 92);

        Image sep = LibraryUiKit.Box(t, "Sep", new Color(0.23f, 0.16f, 0.1f, 0.15f), false);
        LibraryUiKit.Place(sep.rectTransform, 24, 326, LW - 48, 2);
        TextMeshProUGUI other = LibraryUiKit.Label(t, "Same meaning in other languages", 22, LibraryUiKit.InkSoft);
        LibraryUiKit.Place(other.rectTransform, 24, 336, LW - 48, 30);

        List<LibraryEntry> others = _data.OtherLanguages(e);
        if (others.Count == 0)
        {
            TextMeshProUGUI none = LibraryUiKit.Label(t, "No matching word found.", 22, LibraryUiKit.InkSoft, FontStyles.Italic);
            LibraryUiKit.Place(none.rectTransform, 24, 372, LW - 48, 30);
        }
        for (int i = 0; i < others.Count; i++)
        {
            LibraryEntry o = others[i];
            float y = 370f + i * 28f * 1.6f;
            Image row = LibraryUiKit.Box(t, "OtherRow", new Color(1f, 1f, 1f, 0f), true);
            LibraryUiKit.Place(row.rectTransform, 14, y, LW - 28, 42);
            LibraryUiKit.MakeButton(row, delegate { GoToEntry(o); });
            LibraryUiKit.Pill(row.transform, LibraryData.LanguageNames[o.Lang], 10, 6, 130, 30, LibraryUiKit.LangColors[o.Lang], Color.white, 18, FontStyles.Bold);
            TextMeshProUGUI ow = LibraryUiKit.Label(row.transform, o.Word, 28, LibraryUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LibraryUiKit.Place(ow.rectTransform, 154, 0, 160, 42);
            TextMeshProUGUI om = LibraryUiKit.Label(row.transform, o.Meaning, 22, LibraryUiKit.InkSoft, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LibraryUiKit.Place(om.rectTransform, 318, 0, LW - 28 - 330, 42);
        }
    }

    private void RefreshSideCard()
    {
        ClearChildren(_sideCard.transform);
        Transform t = _sideCard.transform;
        TextMeshProUGUI recent = LibraryUiKit.Label(t, "Recent searches", 24, LibraryUiKit.InkSoft);
        LibraryUiKit.Place(recent.rectTransform, 24, 14, 300, 32);
        float y = 52f;
        float x = 24f;
        int shown = 0;
        foreach (string r in _data.Recent)
        {
            if (shown >= 6) break;
            string text = r;
            if (!Chip(t, text, ref x, ref y, delegate { _input.text = text; _pendingTime = 0f; })) break;
            shown++;
        }
        if (shown == 0)
        {
            TextMeshProUGUI none = LibraryUiKit.Label(t, "Nothing yet. Press Enter after a search.", 22, LibraryUiKit.StarOff, FontStyles.Italic);
            LibraryUiKit.Place(none.rectTransform, 24, y, LW - 48, 30);
            y += 40f;
        }

        List<LibraryEntry> favs = _data.Favorites();
        float fy = Mathf.Max(y + 30f, 168f);
        TextMeshProUGUI favLabel = LibraryUiKit.Label(t, "Favorites (" + favs.Count + ")", 24, LibraryUiKit.InkSoft);
        LibraryUiKit.Place(favLabel.rectTransform, 24, fy, 300, 32);
        fy += 38f;
        x = 24f;
        if (favs.Count == 0)
        {
            TextMeshProUGUI none = LibraryUiKit.Label(t, "Tap a star to save a word.", 22, LibraryUiKit.StarOff, FontStyles.Italic);
            LibraryUiKit.Place(none.rectTransform, 24, fy, LW - 48, 30);
        }
        shown = 0;
        foreach (LibraryEntry f in favs)
        {
            if (shown >= 8) break;
            LibraryEntry fav = f;
            if (!Chip(t, fav.Word, ref x, ref fy, delegate { GoToEntry(fav); })) break;
            shown++;
        }
    }

    /// <summary>Adds a flowing chip button; returns false when there is no vertical room left in the card.</summary>
    private bool Chip(Transform parent, string text, ref float x, ref float y, UnityEngine.Events.UnityAction click)
    {
        TextMeshProUGUI probe = LibraryUiKit.Label(parent, text, 24, LibraryUiKit.Ink);
        float w = Mathf.Min(probe.preferredWidth + 36f, LW - 48f);
        LibraryUiKit.DestroyObject(probe.gameObject);
        if (x + w > LW - 24f)
        {
            x = 24f;
            y += 52f;
        }
        if (y + 44f > SideH - 12f)
        {
            return false;
        }
        Image chip = LibraryUiKit.Pill(parent, text, x, y, w, 44, new Color(1f, 1f, 1f, 0.8f), LibraryUiKit.Ink, 24);
        LibraryUiKit.MakeButton(chip, click);
        x += w + 10f;
        return true;
    }

    // ================================================================ actions

    private void OpenEntry(LibraryEntry e)
    {
        _selected = e;
        string q = LibraryData.NormalizeQuery(_query);
        if (q.Length > 0)
        {
            _data.AddRecent(q);
            RefreshSideCard();
        }
        _notebook.SetSelected(_notebookLang == e.Lang ? e : null);
        RefreshDetailCard();
    }

    /// <summary>Jumps to an entry in the notebook of its own language (clears the search).</summary>
    private void GoToEntry(LibraryEntry e)
    {
        _query = string.Empty;
        _input.SetTextWithoutNotify(string.Empty);
        _clearButton.SetActive(false);
        _showFavorites = false;
        _lang = e.Lang;
        _selected = e;
        RefreshTabs();
        RefreshFavButton();
        RefreshDetailCard();
        ApplyView(false);
        _notebook.ScrollToEntry(e);
    }

    private void ToggleFavorite(LibraryEntry e)
    {
        _data.ToggleFavorite(e);
        _notebook.Refresh();
        RefreshDetailCard();
        RefreshSideCard();
        RefreshFavButton();
        if (!_notebook.Root.activeSelf)
        {
            ApplyView(true);
        }
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Transform c = t.GetChild(i);
            c.gameObject.SetActive(false);
            c.SetParent(null, false);
            LibraryUiKit.DestroyObject(c.gameObject);
        }
    }
}
