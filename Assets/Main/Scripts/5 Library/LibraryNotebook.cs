using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// LibraryNotebook
/// The "dictionary notebook": a scrollable A-Z word list on a ruled paper page with spiral rings, a red
/// margin line and sticky-looking letter headers. It is virtualised - only about a dozen row objects
/// exist and they are re-bound as you scroll - so lists of 20,000+ words stay smooth. A slim A-Z strip
/// on the right edge lets you click or drag to jump to a letter (a bubble shows the letter while
/// dragging) and highlights the letter currently on screen. A slim scroll bar beside the page shows
/// where you are in the whole dictionary (drag the handle or click the track), and a small label
/// ("K   8,421 / 22,005") appears next to it while scrolling. Rows show the native word and its English
/// meaning plus a favorite star; clicking a row reports the entry through OnSelect.
/// Created and driven by LibraryController (SetData, ScrollToEntry, SetSelected, Refresh).
/// </summary>
public class LibraryNotebook : MonoBehaviour
{
    public const float RowH = 64f;
    private const float PadLeft = 70f;
    private const float PadRight = 76f;

    public Action<LibraryEntry> OnSelect;
    public Action<LibraryEntry> OnToggleFavorite;
    public Func<LibraryEntry, bool> IsFavorite;

    private class Item
    {
        public char Letter;
        public LibraryEntry Entry;
    }

    private class Row
    {
        public RectTransform Rt;
        public Image Highlight;
        public TextMeshProUGUI Word, Meaning, Head;
        public Image Line, HeadLine, StarImg;
        public Button RowButton, StarButton;
        public int Index = -1;
    }

    private RectTransform _root;
    private ScrollRect _scroll;
    private RectTransform _viewport;
    private RectTransform _content;
    private readonly List<Row> _pool = new List<Row>();
    private List<Item> _items = new List<Item>();
    private readonly Dictionary<char, int> _headerIndex = new Dictionary<char, int>();
    private readonly Dictionary<LibraryEntry, int> _entryIndex = new Dictionary<LibraryEntry, int>();
    private readonly List<char> _letters = new List<char>();
    private LibraryEntry _selected;
    private float _width, _height;

    private RectTransform _strip;
    private readonly List<TextMeshProUGUI> _stripLabels = new List<TextMeshProUGUI>();
    private Image _bubble;
    private TextMeshProUGUI _bubbleText;
    private char _currentLetter;

    // light amber fill of the slider's drag pill and letter bubble (dark text on top, so it stays readable)
    private static readonly Color PopupFill = new Color32(255, 226, 150, 255);

    private RectTransform _track;
    private Image _handle;
    private Image _posPill;
    private TextMeshProUGUI _posText;
    private int[] _ordinal = new int[0];
    private int _totalWords;
    private float _posHideTime;
    private bool _dragging;

    public GameObject Root { get { return _root.gameObject; } }

    public static LibraryNotebook Create(Transform parent, float x, float y, float w, float h)
    {
        RectTransform root = LibraryUiKit.NewRect("Notebook", parent);
        LibraryUiKit.Place(root, x, y, w, h);
        var nb = LibraryUiKit.GetOrAdd<LibraryNotebook>(root.gameObject);
        nb.Build(root, w, h);
        return nb;
    }

    private void Build(RectTransform root, float w, float h)
    {
        _root = root;
        _width = w;
        _height = h;

        Image page = LibraryUiKit.Box(root, "Page", LibraryUiKit.Paper, true);
        LibraryUiKit.Fill(page.rectTransform);

        for (int i = 0; i < 9; i++)
        {
            float cy = 36f + i * 82f;
            Image outer = LibraryUiKit.Icon(root, "Ring", LibraryUiKit.Circle, new Color32(138, 107, 71, 255));
            LibraryUiKit.Place(outer.rectTransform, 12, cy, 26, 26);
            Image inner = LibraryUiKit.Icon(root, "RingHole", LibraryUiKit.Circle, new Color32(217, 185, 139, 255));
            LibraryUiKit.Place(inner.rectTransform, 15, cy + 3, 20, 20);
        }
        Image margin = LibraryUiKit.Box(root, "Margin", new Color32(230, 184, 184, 255), false);
        LibraryUiKit.Place(margin.rectTransform, 56, 0, 2, h);

        float vw = w - PadLeft - PadRight;
        _scroll = LibraryUiKit.MakeScroll(root, "Scroll", PadLeft, 10, vw, h - 20, out _content);
        _viewport = _scroll.viewport;
        _scroll.onValueChanged.AddListener(delegate { UpdateRows(); UpdateHandle(); ShowPosition(); });

        int rowCount = Mathf.CeilToInt((h - 20) / RowH) + 3;
        for (int i = 0; i < rowCount; i++)
        {
            _pool.Add(MakeRow(vw));
        }
        BuildStrip(root, w, h);
        BuildScrollbar(root, w, h);
    }

    private void Update()
    {
        if (_posPill != null && _posPill.gameObject.activeSelf && !_dragging && Time.unscaledTime > _posHideTime)
        {
            _posPill.gameObject.SetActive(false);
        }
    }

    // -------------------------------------------------------------- scroll bar

    private void BuildScrollbar(RectTransform root, float w, float h)
    {
        _track = LibraryUiKit.NewRect("ScrollTrack", root);
        // light, easy-to-see slider: a soft sand track with a wider golden handle; the drag pill is light with dark text
        LibraryUiKit.Place(_track, w - 76, 12, 16, h - 24);
        var trackImg = LibraryUiKit.GetOrAdd<Image>(_track.gameObject);
        trackImg.sprite = LibraryUiKit.Rounded;
        trackImg.type = Image.Type.Sliced;
        trackImg.color = new Color32(226, 208, 175, 255);
        trackImg.raycastTarget = true;
        LibraryUiKit.GetOrAdd<LibraryScrollTrack>(_track.gameObject).Owner = this;

        _handle = LibraryUiKit.Box(_track, "Handle", LibraryUiKit.Accent, true);
        LibraryUiKit.Place(_handle.rectTransform, 0, 0, 16, 80);

        _posPill = LibraryUiKit.Box(root, "PositionPill", PopupFill, true);
        LibraryUiKit.Place(_posPill.rectTransform, w - 76 - 10 - 290, 0, 290, 48);
        _posText = LibraryUiKit.Label(_posPill.transform, "", 26, LibraryUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
        LibraryUiKit.Fill(_posText.rectTransform);
        _posPill.gameObject.SetActive(false);
    }

    private void UpdateHandle()
    {
        if (_handle == null || _viewport == null) return;
        float trackH = _height - 24f;
        float viewH = _viewport.rect.height;
        float contentH = _items.Count * RowH;
        float max = contentH - viewH;
        _handle.gameObject.SetActive(max > 0f);
        if (max <= 0f) return;
        float hh = Mathf.Clamp(trackH * viewH / contentH, 70f, trackH);
        float t = Mathf.Clamp01(_content.anchoredPosition.y / max);
        LibraryUiKit.Place(_handle.rectTransform, 0, t * (trackH - hh), 16, hh);
    }

    private void ShowPosition()
    {
        if (_posPill == null || _items.Count == 0 || _ordinal.Length != _items.Count) return;
        int first = Mathf.Clamp(Mathf.FloorToInt(_content.anchoredPosition.y / RowH) + 1, 0, _items.Count - 1);
        Item it = _items[first];
        char c = it.Entry != null ? it.Entry.Raw[0] : it.Letter;
        _posText.text = c + "   " + _ordinal[first].ToString("N0") + " / " + _totalWords.ToString("N0");
        RectTransform h = _handle.rectTransform;
        float y = Mathf.Clamp(12f - h.anchoredPosition.y + h.sizeDelta.y * 0.5f - 24f, 4f, _height - 52f);
        LibraryUiKit.Place(_posPill.rectTransform, _width - 76 - 10 - 290, y, 290, 48);
        _posPill.gameObject.SetActive(true);
        _posHideTime = Time.unscaledTime + 1.3f;
    }

    /// <summary>Called by the track while the pointer is down/dragging; yFromTop is in track pixels.</summary>
    public void TrackPointer(float yFromTop, bool active)
    {
        _dragging = active;
        if (!active)
        {
            _posHideTime = Time.unscaledTime + 0.8f;
            return;
        }
        float trackH = _height - 24f;
        float viewH = _viewport.rect.height;
        float contentH = _items.Count * RowH;
        float max = Mathf.Max(0f, contentH - viewH);
        float hh = Mathf.Clamp(trackH * viewH / Mathf.Max(1f, contentH), 70f, trackH);
        float t = Mathf.Clamp01((yFromTop - hh * 0.5f) / Mathf.Max(1f, trackH - hh));
        _content.anchoredPosition = new Vector2(0f, t * max);
        UpdateRows();
        UpdateHandle();
        ShowPosition();
    }

    private Row MakeRow(float vw)
    {
        var row = new Row();
        row.Highlight = LibraryUiKit.Box(_content, "Row", new Color(0.96f, 0.65f, 0.14f, 0f), true);
        row.Rt = row.Highlight.rectTransform;
        LibraryUiKit.Place(row.Rt, 0, 0, vw, RowH);
        row.RowButton = LibraryUiKit.MakeButton(row.Highlight, null);

        row.Word = LibraryUiKit.Label(row.Rt, "", 33, LibraryUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        LibraryUiKit.Place(row.Word.rectTransform, 12, 0, 290, RowH);
        row.Meaning = LibraryUiKit.Label(row.Rt, "", 27, LibraryUiKit.InkSoft, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        LibraryUiKit.Place(row.Meaning.rectTransform, 310, 0, vw - 310 - 70, RowH);
        row.Line = LibraryUiKit.Box(row.Rt, "Line", LibraryUiKit.Line, false);
        LibraryUiKit.Place(row.Line.rectTransform, 0, RowH - 2, vw, 2);

        row.StarImg = LibraryUiKit.Icon(row.Rt, "Star", LibraryUiKit.Star, LibraryUiKit.StarOff);
        LibraryUiKit.Place(row.StarImg.rectTransform, vw - 56, 12, 40, 40);
        row.StarButton = LibraryUiKit.MakeButton(row.StarImg, null);

        row.Head = LibraryUiKit.Label(row.Rt, "", 36, LibraryUiKit.Red, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        LibraryUiKit.Place(row.Head.rectTransform, 12, 0, 200, RowH - 4);
        row.HeadLine = LibraryUiKit.Box(row.Rt, "HeadLine", new Color32(230, 184, 184, 255), false);
        LibraryUiKit.Place(row.HeadLine.rectTransform, 0, RowH - 3, vw, 3);

        row.RowButton.onClick.AddListener(delegate { RowClicked(row); });
        row.StarButton.onClick.AddListener(delegate { StarClicked(row); });
        row.Rt.gameObject.SetActive(false);
        return row;
    }

    private void RowClicked(Row row)
    {
        if (row.Index >= 0 && row.Index < _items.Count && _items[row.Index].Entry != null && OnSelect != null)
        {
            OnSelect(_items[row.Index].Entry);
        }
    }

    private void StarClicked(Row row)
    {
        if (row.Index >= 0 && row.Index < _items.Count && _items[row.Index].Entry != null && OnToggleFavorite != null)
        {
            OnToggleFavorite(_items[row.Index].Entry);
        }
    }

    // ------------------------------------------------------------------ data

    public void SetData(List<LibraryEntry> entries)
    {
        _items = new List<Item>(entries.Count + 32);
        _headerIndex.Clear();
        _entryIndex.Clear();
        _letters.Clear();
        char letter = '\0';
        int counter = 0;
        var ordinal = new List<int>(entries.Count + 32);
        foreach (LibraryEntry e in entries)
        {
            char c = e.Raw[0];
            if (c != letter)
            {
                letter = c;
                _headerIndex[c] = _items.Count;
                _letters.Add(c);
                _items.Add(new Item { Letter = c });
                ordinal.Add(counter + 1);
            }
            _entryIndex[e] = _items.Count;
            _items.Add(new Item { Entry = e });
            counter++;
            ordinal.Add(counter);
        }
        _ordinal = ordinal.ToArray();
        _totalWords = counter;
        _content.sizeDelta = new Vector2(0f, _items.Count * RowH);
        _content.anchoredPosition = Vector2.zero;
        RebuildStrip();
        UpdateRows();
        UpdateHandle();
    }

    public void SetSelected(LibraryEntry e)
    {
        _selected = e;
        UpdateRows();
    }

    public void Refresh()
    {
        UpdateRows();
    }

    public void ScrollToEntry(LibraryEntry e)
    {
        int idx;
        if (e != null && _entryIndex.TryGetValue(e, out idx))
        {
            ScrollToIndex(idx - 2);
        }
    }

    private void ScrollToIndex(int index)
    {
        float viewH = _viewport.rect.height;
        float max = Mathf.Max(0f, _items.Count * RowH - viewH);
        _content.anchoredPosition = new Vector2(0f, Mathf.Clamp(index * RowH, 0f, max));
        UpdateRows();
        UpdateHandle();
        ShowPosition();
    }

    private void UpdateRows()
    {
        if (_items.Count == 0)
        {
            foreach (Row r in _pool) r.Rt.gameObject.SetActive(false);
            return;
        }
        int first = Mathf.Max(0, Mathf.FloorToInt(_content.anchoredPosition.y / RowH));
        for (int i = 0; i < _pool.Count; i++)
        {
            Row row = _pool[i];
            int idx = first + i;
            if (idx >= _items.Count)
            {
                row.Rt.gameObject.SetActive(false);
                row.Index = -1;
                continue;
            }
            Bind(row, idx);
        }

        int probe = Mathf.Min(_items.Count - 1, first + 1);
        char c = _items[probe].Entry != null ? _items[probe].Entry.Raw[0] : _items[probe].Letter;
        SetCurrentLetter(c);
    }

    private void Bind(Row row, int idx)
    {
        Item it = _items[idx];
        row.Index = idx;
        row.Rt.gameObject.SetActive(true);
        row.Rt.anchoredPosition = new Vector2(0f, -idx * RowH);
        bool header = it.Entry == null;
        row.Head.gameObject.SetActive(header);
        row.HeadLine.gameObject.SetActive(header);
        row.Word.gameObject.SetActive(!header);
        row.Meaning.gameObject.SetActive(!header);
        row.Line.gameObject.SetActive(!header);
        row.StarImg.gameObject.SetActive(!header);
        row.RowButton.enabled = !header;
        if (header)
        {
            row.Head.text = it.Letter.ToString();
            row.Highlight.color = new Color(1f, 1f, 1f, 0f);
            return;
        }
        LibraryEntry e = it.Entry;
        row.Word.text = e.Word;
        row.Meaning.text = e.Meaning;
        row.Meaning.color = e.HasMeaning ? LibraryUiKit.InkSoft : LibraryUiKit.StarOff;
        row.StarImg.color = IsFavorite != null && IsFavorite(e) ? LibraryUiKit.Accent : LibraryUiKit.StarOff;
        row.Highlight.color = e == _selected ? new Color(0.99f, 0.93f, 0.8f, 1f) : new Color(1f, 1f, 1f, 0f);
    }

    // ----------------------------------------------------------- letter strip

    private void BuildStrip(RectTransform root, float w, float h)
    {
        _strip = LibraryUiKit.NewRect("LetterStrip", root);
        LibraryUiKit.Place(_strip, w - 58, 12, 50, h - 24);
        var hit = LibraryUiKit.GetOrAdd<Image>(_strip.gameObject);
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;
        LibraryUiKit.GetOrAdd<LibraryLetterStrip>(_strip.gameObject).Owner = this;

        _bubble = LibraryUiKit.Icon(root, "Bubble", LibraryUiKit.Circle, PopupFill);
        LibraryUiKit.Place(_bubble.rectTransform, w - 64 - 110, 0, 90, 90);
        _bubbleText = LibraryUiKit.Label(_bubble.transform, "A", 54, LibraryUiKit.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
        LibraryUiKit.Fill(_bubbleText.rectTransform);
        _bubble.gameObject.SetActive(false);
    }

    private void RebuildStrip()
    {
        for (int c = _strip.childCount - 1; c >= 0; c--)
        {
            Transform old = _strip.GetChild(c);
            old.SetParent(null, false);
            LibraryUiKit.DestroyObject(old.gameObject);
        }
        _stripLabels.Clear();
        float stripH = _height - 24;
        float step = stripH / Mathf.Max(1, _letters.Count);
        for (int i = 0; i < _letters.Count; i++)
        {
            TextMeshProUGUI t = LibraryUiKit.Label(_strip, _letters[i].ToString(), 24, LibraryUiKit.InkSoft, FontStyles.Bold, TextAlignmentOptions.Center);
            LibraryUiKit.Place(t.rectTransform, 0, i * step, 50, step);
            _stripLabels.Add(t);
        }
        _currentLetter = '\0';
    }

    private void SetCurrentLetter(char c)
    {
        if (c == _currentLetter) return;
        _currentLetter = c;
        for (int i = 0; i < _letters.Count && i < _stripLabels.Count; i++)
        {
            bool on = _letters[i] == c;
            _stripLabels[i].color = on ? LibraryUiKit.Red : LibraryUiKit.InkSoft;
            _stripLabels[i].fontSize = on ? 28 : 24;
        }
    }

    /// <summary>Called by the strip while the pointer is down/dragging; t is 0 (top) to 1 (bottom).</summary>
    public void StripPointer(float t, bool active)
    {
        if (!active)
        {
            _bubble.gameObject.SetActive(false);
            return;
        }
        if (_letters.Count == 0) return;
        int i = Mathf.Clamp(Mathf.FloorToInt(t * _letters.Count), 0, _letters.Count - 1);
        char c = _letters[i];
        ScrollToIndex(_headerIndex[c]);
        _bubble.gameObject.SetActive(true);
        _bubbleText.text = c.ToString();
        float y = Mathf.Clamp(12 + (i + 0.5f) * ((_height - 24) / _letters.Count) - 45, 4, _height - 94);
        LibraryUiKit.Place(_bubble.rectTransform, _width - 64 - 110, y, 90, 90);
    }
}
