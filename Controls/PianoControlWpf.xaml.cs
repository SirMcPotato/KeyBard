using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyBard.Interop;

namespace KeyBard.Controls;

public partial class PianoControlWpf
{
    public enum KeyType
    {
        White,
        Black
    }

    private static readonly KeyType[] KeyTypeTable =
    {
        // MIDI 0-11
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 12-23
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 24-35
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 36-47
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 48-59
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 60-71
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 72-83
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 84-95
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 96-107
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 108-119
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White,
        // MIDI 120-127
        KeyType.White, KeyType.Black, KeyType.White, KeyType.Black, KeyType.White, KeyType.White, KeyType.Black, KeyType.White
    };

    private static readonly int[] Ids =
    {
        // MIDI 0-11
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 12-23
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 24-35
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 36-47
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 48-59
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 60-71
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 72-83
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 84-95
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 96-107
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 108-119
        0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0,
        // MIDI 120-127
        0, 1, 0, 1, 0, 0, 1, 0
    };

    // Store references to keys for highlighting
    private readonly List<PianoKeyWpf> _keys = [];

    /// <summary>
    /// The key currently in binding mode, or null if none.
    /// </summary>
    private PianoKeyWpf? _keyAwaitingBinding;

    public int WhiteKeyCount { get; private set; }

    public const int LowNoteId = 21;

    public const int HighNoteId = 109;

    // Container for keys
    private readonly Canvas _keysCanvas = new();

    private IKeyboardInterop _keyboardInterop = new KeyboardInteropWrapper();

    public PianoControlWpf()
    {
        InitializeComponent();

        Content = _keysCanvas; // set Canvas as content of UserControl

        Loaded += (_, _) => BuildKeys();
        SizeChanged += (_, _) => LayoutKeys();
    }

    public void SetKeyboardInterop(IKeyboardInterop keyboardInterop)
    {
        _keyboardInterop = keyboardInterop;
        // Optionally rebuild or refresh existing keys if needed, 
        // but typically this should be called before BuildKeys (Loaded event).
    }

    private void BuildKeys()
    {
        _keysCanvas.Children.Clear();
        _keys.Clear();
        WhiteKeyCount = 0;

        for (var i = 0; i < HighNoteId - LowNoteId; i++)
        {
            var noteId = LowNoteId + i;
            var key = new PianoKeyWpf(KeyTypeTable[noteId], Ids[noteId], noteId, _keyboardInterop);

            if (KeyTypeTable[noteId] == KeyType.White)
            {
                key.NoteOffColor = Colors.White;
                WhiteKeyCount++;
                key.SetValue(Panel.ZIndexProperty, 0);
            }
            else
            {
                key.NoteOffColor = Colors.Black;
                key.SetValue(Panel.ZIndexProperty, 10);
            }

            _keys.Add(key);
            _keysCanvas.Children.Add(key);

            key.BindingRequested += OnKeyBindingRequested;
        }

        LayoutKeys();
    }

    private void OnKeyBindingRequested(PianoKeyWpf pianoKey)
    {
        // If another key was already awaiting binding, cancel it
        _keyAwaitingBinding?.ExitBindingMode();

        if (_keyAwaitingBinding == pianoKey)
        {
            // Clicking the same key again cancels binding mode
            _keyAwaitingBinding = null;
            return;
        }

        _keyAwaitingBinding = pianoKey;
        pianoKey.EnterBindingMode();

        // Focus the control so it receives keyboard input
        Focusable = true;
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_keyAwaitingBinding == null) return;

        // Resolve the actual key (handles System keys, dead keys on international layouts)
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key
        };

        switch (key)
        {
            case Key.Escape:
                _keyAwaitingBinding.ExitBindingMode();
                _keyAwaitingBinding = null;
                e.Handled = true;
                return;
            case Key.Delete or Key.Back:
                _keyAwaitingBinding.SetBinding(null);
                _keyAwaitingBinding = null;
                e.Handled = true;
                return;
        }

        // Convert to scan code and bind
        var scanCode = _keyboardInterop.KeyToScanCode(key);
        if (scanCode != 0)
        {
            _keyAwaitingBinding.SetBinding(scanCode);
        }
        else
        {
            _keyAwaitingBinding.ExitBindingMode();
        }

        _keyAwaitingBinding = null;
        e.Handled = true;
    }

    /// <summary>
    /// Gets the scan code bound to a given MIDI note number, or null if unbound.
    /// </summary>
    public ushort? GetBoundScanCode(int midiNoteNumber)
    {
        var keyIndex = midiNoteNumber - LowNoteId;
        if (keyIndex < 0 || keyIndex >= _keys.Count) return null;
        return _keys[keyIndex].BoundScanCode;
    }

    private void LayoutKeys()
    {
        if (_keys.Count == 0) return;

        var canvasWidth = ActualWidth;
        var canvasHeight = ActualHeight;

        // Count white keys
        var whiteKeys = _keys.Count(k => k.KeyType == KeyType.White);
        var whiteKeyWidth = canvasWidth / whiteKeys;

        double currentX = 0;
        foreach (var key in _keys.Where(key => key.KeyType == KeyType.White))
        {
            key.Width = whiteKeyWidth;
            key.Height = canvasHeight;
            Canvas.SetLeft(key, currentX);
            Canvas.SetTop(key, 0);
            currentX += whiteKeyWidth;
        }

        // Position black keys on top
        for (var i = 0; i < _keys.Count; i++)
        {
            var key = _keys[i];
            if (key.KeyType != KeyType.Black) continue;
            key.Width = whiteKeyWidth * 0.6;
            key.Height = canvasHeight * 0.6;

            // place between neighboring white keys
            var x = Canvas.GetLeft(_keys[Math.Max(0, i - 1)]) + whiteKeyWidth - key.Width / 2;
            Canvas.SetLeft(key, x);
            Canvas.SetTop(key, 0);
        }
    }

    public void HighlightKey(int keyIndex, bool highlight)
    {
        if (keyIndex < 0 || keyIndex >= _keys.Count) return;

        var key = _keys[keyIndex];
        if (highlight)
            key.PressPianoKey();
        else
            key.ReleasePianoKey();
    }

    /// <summary>Return whether a MIDI note is White or Black</summary>
    public KeyType GetKeyType(int midiNote)
    {
        if (midiNote < 0 || midiNote >= KeyTypeTable.Length) throw new ArgumentOutOfRangeException(nameof(midiNote));
        return KeyTypeTable[midiNote];
    }

    /// <summary>Return the X coordinate of a key on a canvas</summary>
    public double GetKeyX(int keyIndex, double canvasWidth)
    {
        var midiNote = LowNoteId + keyIndex;
        var whiteKeyWidth = canvasWidth / WhiteKeyCount;

        if (GetKeyType(midiNote) == KeyType.White)
        {
            var whiteIndex = GetWhiteKeyIndex(keyIndex);
            return whiteIndex * whiteKeyWidth;
        }

        // Match LayoutKeys: black key is centered on the right edge of the previous white key
        // Find the previous key (keyIndex - 1) which should be a white key
        var whiteIndexBefore = GetWhiteKeyIndex(keyIndex);

        var x = (whiteIndexBefore - 1) * whiteKeyWidth + whiteKeyWidth - (whiteKeyWidth * 0.6) / 2.0;
        return x;
    }

    private readonly Dictionary<int, int> _whiteKeyIndexCache = new();

    /// <summary>Return how many white keys come before this key index (relative to LowNoteId)</summary>
    public int GetWhiteKeyIndex(int keyIndex)
    {
        if (_whiteKeyIndexCache.TryGetValue(keyIndex, out int cached)) return cached;

        int count = 0;
        for (int i = 0; i < keyIndex; i++)
        {
            if (GetKeyType(LowNoteId + i) == KeyType.White) count++;
        }

        _whiteKeyIndexCache[keyIndex] = count;
        return count;
    }

    /// <summary>
    /// Exports all current keybindings as a dictionary of MIDI note number → scan code.
    /// </summary>
    public Dictionary<int, ushort> ExportBindings()
    {
        var bindings = new Dictionary<int, ushort>();
        foreach (var key in _keys)
        {
            if (key.BoundScanCode.HasValue)
            {
                bindings[key.MidiNoteNumber] = key.BoundScanCode.Value;
            }
        }

        return bindings;
    }

    /// <summary>
    /// Clears all MIDI to key bindings.
    /// </summary>
    public void ClearAllBindings()
    {
        foreach (var key in _keys)
        {
            key.SetBinding(null);
        }
    }

    /// <summary>
    /// Imports keybindings from a dictionary of MIDI note number → scan code.
    /// Clears all existing bindings first.
    /// </summary>
    public void ImportBindings(Dictionary<int, ushort> bindings)
    {
        ClearAllBindings();

        foreach (var (midiNote, scanCode) in bindings)
        {
            var keyIndex = midiNote - LowNoteId;
            if (keyIndex >= 0 && keyIndex < _keys.Count)
            {
                _keys[keyIndex].SetBinding(scanCode);
            }
        }
    }
}