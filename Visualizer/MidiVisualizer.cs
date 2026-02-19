using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KeyBard.Controls;
using KeyBard.Interop;
using KeyBard.Midi;

namespace KeyBard.Visualizer;

public class MidiVisualizer
{
    private readonly PianoControlWpf _piano;
    private readonly Canvas _canvas;
    private readonly int _minNote;
    private readonly double _pixelsPerMs;

    // External dependency: MidiPlayer provides the source of time
    private MidiPlayer? _player;

    public class VisualNote
    {
        public int NoteNumber { get; init; }
        public int Channel { get; init; }
        public double StartMs { get; init; }
        public double DurationMs { get; init; }
        public int Velocity { get; init; }
    }

    private List<VisualNote> _allNotes = [];
    private readonly List<ActiveNote> _activeNotes = [];
    private readonly List<Rectangle> _rectPool = [];
    private bool _rebuilding;

    public void SetNotes(IEnumerable<VisualNote> notes)
    {
        _allNotes = notes.OrderBy(n => n.StartMs).ToList();
        Reset();
    }

    // Hue-based color palette for MIDI channels
    public static readonly Color[] ChannelColors =
    [
        Color.FromRgb(0xFF, 0x4B, 0x4B), // Ch 1  - Red
        Color.FromRgb(0xFF, 0x90, 0x4B), // Ch 2  - Orange
        Color.FromRgb(0xFF, 0xD7, 0x4B), // Ch 3  - Amber
        Color.FromRgb(0xD7, 0xFF, 0x4B), // Ch 4  - Lime
        Color.FromRgb(0x90, 0xFF, 0x4B), // Ch 5  - Green
        Color.FromRgb(0x4B, 0xFF, 0x4B), // Ch 6  - Bright Green
        Color.FromRgb(0x4B, 0xFF, 0x90), // Ch 7  - Spring Green
        Color.FromRgb(0x4B, 0xFF, 0xD7), // Ch 8  - Turquoise
        Color.FromRgb(0x4B, 0xD7, 0xFF), // Ch 9  - Sky Blue
        Color.FromRgb(0x4B, 0x90, 0xFF), // Ch 10 - Azure
        Color.FromRgb(0x4B, 0x4B, 0xFF), // Ch 11 - Blue
        Color.FromRgb(0x90, 0x4B, 0xFF), // Ch 12 - Violet
        Color.FromRgb(0xD7, 0x4B, 0xFF), // Ch 13 - Magenta
        Color.FromRgb(0xFF, 0x4B, 0xD7), // Ch 14 - Pink
        Color.FromRgb(0xFF, 0x4B, 0x90), // Ch 15 - Rose
        Color.FromRgb(0xC0, 0xC0, 0xC0), // Ch 16 - Silver/Gray
    ];

    private class ActiveNote(
        Rectangle rect,
        int noteNumber,
        int channel,
        int keyIndex,
        double startMs,
        double durationMs,
        double rectHeight,
        int velocity)
    {
        public Rectangle Rect { get; } = rect;
        public int NoteNumber { get; } = noteNumber;
        public int Channel { get; } = channel;
        public int KeyIndex { get; } = keyIndex;
        public double StartMs { get; } = startMs;
        public double DurationMs { get; } = durationMs;
        public double RectHeight { get; } = rectHeight;
        public bool KeyHighlighted { get; set; }
        public bool NoteSounding { get; set; }
        public int Velocity { get; } = velocity;
    }

    public MidiVisualizer(Canvas canvas, PianoControlWpf piano, double pixelsPerMs = 0.2)
    {
        _canvas = canvas;
        _piano = piano;
        _minNote = PianoControlWpf.LowNoteId;
        _pixelsPerMs = pixelsPerMs;

        canvas.SizeChanged += OnCanvasSizeChanged;
    }

    public void AttachPlayer(MidiPlayer player)
    {
        _player = player;
        _player.PositionChanged += OnPlayerPositionChanged;
    }

    private void OnPlayerPositionChanged(double newPosition)
    {
        // If we detect a backward jump (like a loop or seek), reset the visualizer state
        // We use a small threshold because minor jitter in ElapsedMs is normal
        var currentMs = _player?.ElapsedMs ?? 0;
        // In some cases PositionChanged might be called with a value that hasn't updated ElapsedMs yet
        // or vice versa. If we are at the very beginning of a loop, newPosition will be LoopStartMs.
        
        // We look for a significant backward jump or a jump that puts current position before the last spawned note
        if (_allNotes.Count > 0 && _nextNoteIndex > 0)
        {
            var lastSpawnedStartMs = _allNotes[Math.Min(_nextNoteIndex, _allNotes.Count - 1)].StartMs;
            if (newPosition < lastSpawnedStartMs - 500) // 500ms buffer for safety
            {
                _canvas.Dispatcher.BeginInvoke(RebuildVisuals);
            }
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RebuildVisuals();
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        CompositionTarget.Rendering += OnRender;
    }

    public void Pause()
    {
        if (!IsRunning) return;
        IsRunning = false;
        CompositionTarget.Rendering -= OnRender;

        foreach (var note in _activeNotes.Where(n => n.NoteSounding && n.KeyHighlighted))
        {
            _piano.HighlightKey(note.KeyIndex, false);
            SendBoundKeyUp(note.NoteNumber);
            note.KeyHighlighted = false;
            note.NoteSounding = false;
        }
    }

    public void Reset()
    {
        IsRunning = false;
        System.Windows.Media.CompositionTarget.Rendering -= OnRender;
        _nextNoteIndex = 0;

        // Cleanup
        foreach (var note in _activeNotes)
        {
            if (note.KeyHighlighted) _piano.HighlightKey(note.KeyIndex, false);
            if (note.NoteSounding)
            {
                SendBoundKeyUp(note.NoteNumber);
            }
        }

        _activeNotes.Clear();
        _rectPool.Clear();
        _canvas.Children.Clear();
    }

    public void Dispose()
    {
        Reset();
    }

    public bool IsRunning { get; private set; }

    public void RebuildVisuals()
    {
        if (_player == null) return;

        var wasRunning = IsRunning;
        if (wasRunning) CompositionTarget.Rendering -= OnRender;

        _rebuilding = true;
        try
        {
            foreach (var note in _activeNotes)
            {
                if (note.KeyHighlighted) _piano.HighlightKey(note.KeyIndex, false);
                if (note.NoteSounding)
                {
                    SendBoundKeyUp(note.NoteNumber);
                }

                ReturnRectangleToPool(note.Rect);
            }

            _activeNotes.Clear();
            foreach (var rect in _rectPool) rect.Visibility = Visibility.Collapsed;

            _nextNoteIndex = _allNotes.Count;

            if (_allNotes.Count != 0)
            {
                var currentMs = _player.ElapsedMs;
                var canvasHeight = _canvas.ActualHeight;
                if (canvasHeight <= 0) canvasHeight = 600;

                var lookaheadMs = canvasHeight / _pixelsPerMs;

                for (int i = 0; i < _allNotes.Count; i++)
                {
                    var vn = _allNotes[i];
                    if (vn.StartMs + vn.DurationMs < currentMs) continue;
                    if (vn.StartMs > currentMs + lookaheadMs)
                    {
                        _nextNoteIndex = i;
                        break;
                    }

                    var rectHeight = Math.Max(5, vn.DurationMs * _pixelsPerMs);
                    var y = canvasHeight - (vn.StartMs - currentMs) * _pixelsPerMs - rectHeight;

                    var isEnabled = EnabledChannels.Contains(vn.Channel);
                    var rect = GetOrCreateRectangle(vn.NoteNumber, vn.Channel, vn.Velocity, rectHeight);
                    rect.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;

                    var keyIndex = vn.NoteNumber - _minNote;
                    var x = _piano.GetKeyX(keyIndex, _canvas.ActualWidth);

                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    if (rect.Parent == null) _canvas.Children.Add(rect);

                    var activeNote = new ActiveNote(rect, vn.NoteNumber, vn.Channel, keyIndex, vn.StartMs,
                        vn.DurationMs, rectHeight, vn.Velocity);

                    if (isEnabled && vn.StartMs <= currentMs && vn.StartMs + vn.DurationMs >= currentMs)
                    {
                        activeNote.NoteSounding = true;
                        activeNote.KeyHighlighted = true;
                        _piano.HighlightKey(keyIndex, true);
                        SendBoundKeyDown(activeNote.NoteNumber);
                    }

                    _activeNotes.Add(activeNote);
                }
            }
        }
        finally
        {
            _rebuilding = false;
            if (wasRunning) CompositionTarget.Rendering += OnRender;
        }
    }

    private Rectangle CreateNoteRectangle(int noteNumber, int channel, int velocity, double rectHeight)
    {
        var canvasWidth = _canvas.ActualWidth;
        var whiteKeyWidth = canvasWidth / _piano.WhiteKeyCount;
        var isBlack = _piano.GetKeyType(noteNumber) == PianoControlWpf.KeyType.Black;
        var rectWidth = isBlack ? whiteKeyWidth * 0.6 : whiteKeyWidth;

        var baseColor = ChannelColors[(channel - 1) % 16];
        var brightness = 0.5 + (velocity / 127.0) * 0.5;
        var fillColor = Color.FromRgb((byte)(baseColor.R * brightness), (byte)(baseColor.G * brightness),
            (byte)(baseColor.B * brightness));

        var rect = new Rectangle
        {
            Width = rectWidth,
            Height = rectHeight,
            RadiusX = 3,
            RadiusY = 3,
            Fill = new SolidColorBrush(fillColor),
            Stroke = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
            StrokeThickness = 0.5,
            Tag = noteNumber
        };

        return rect;
    }

    public HashSet<int> EnabledChannels { get; } = new(Enumerable.Range(1, 16));

    public void OnEnabledChannelsChanged()
    {
        var currentNotes = _activeNotes.ToArray();
        foreach (var note in currentNotes)
        {
            bool isEnabled = EnabledChannels.Contains(note.Channel);

            if (isEnabled)
            {
                if (note.Rect.Visibility != Visibility.Visible)
                {
                    note.Rect.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (note.Rect.Visibility != Visibility.Collapsed)
                {
                    note.Rect.Visibility = Visibility.Collapsed;
                    if (note.KeyHighlighted)
                    {
                        note.KeyHighlighted = false;
                        _piano.HighlightKey(note.KeyIndex, false);
                    }

                    if (note.NoteSounding)
                    {
                        note.NoteSounding = false;
                        SendBoundKeyUp(note.NoteNumber);
                    }
                }
            }
        }
    }

    private int _nextNoteIndex;

    private void OnRender(object? sender, EventArgs e)
    {
        if (_rebuilding || _player == null) return;
        var currentMs = _player.ElapsedMs;
        var canvasHeight = _canvas.ActualHeight;
        if (canvasHeight <= 0) return;

        var lookaheadMs = canvasHeight / _pixelsPerMs;

        // 1. Spawn new notes
        while (_nextNoteIndex < _allNotes.Count)
        {
            var vn = _allNotes[_nextNoteIndex];
            if (vn.StartMs > currentMs + lookaheadMs) break;

            if (vn.StartMs + vn.DurationMs >= currentMs)
            {
                var rectHeight = Math.Max(5, vn.DurationMs * _pixelsPerMs);
                var isEnabled = EnabledChannels.Contains(vn.Channel);
                var rect = GetOrCreateRectangle(vn.NoteNumber, vn.Channel, vn.Velocity, rectHeight);
                rect.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;

                var keyIndex = vn.NoteNumber - _minNote;
                var x = _piano.GetKeyX(keyIndex, _canvas.ActualWidth);
                Canvas.SetLeft(rect, x);

                if (rect.Parent == null) _canvas.Children.Add(rect);
                _activeNotes.Add(new ActiveNote(rect, vn.NoteNumber, vn.Channel, keyIndex, vn.StartMs, vn.DurationMs,
                    rectHeight, vn.Velocity));
            }

            _nextNoteIndex++;
        }

        // 2. Update existing notes
        for (var i = 0; i < _activeNotes.Count; i++)
        {
            var note = _activeNotes[i];
            var y = canvasHeight - (note.StartMs - currentMs) * _pixelsPerMs - note.RectHeight;
            Canvas.SetTop(note.Rect, y);

            if (!note.NoteSounding && currentMs >= note.StartMs && currentMs < note.StartMs + note.DurationMs)
            {
                if (EnabledChannels.Contains(note.Channel))
                {
                    note.NoteSounding = true;
                    note.KeyHighlighted = true;
                    _piano.HighlightKey(note.KeyIndex, true);
                    SendBoundKeyDown(note.NoteNumber);
                }
            }

            if (note.NoteSounding && currentMs >= note.StartMs + note.DurationMs)
            {
                note.NoteSounding = false;
                if (note.KeyHighlighted)
                {
                    note.KeyHighlighted = false;
                    _piano.HighlightKey(note.KeyIndex, false);
                }

                SendBoundKeyUp(note.NoteNumber);
            }

            if (y > canvasHeight)
            {
                ReturnRectangleToPool(note.Rect);
                _activeNotes.RemoveAt(i);
                i--;
            }
        }
    }

    private Rectangle GetOrCreateRectangle(int noteNumber, int channel, int velocity, double rectHeight)
    {
        Rectangle rect;
        if (_rectPool.Count > 0)
        {
            rect = _rectPool[^1];
            _rectPool.RemoveAt(_rectPool.Count - 1);
            UpdateRectangle(rect, noteNumber, channel, velocity, rectHeight);
        }
        else
        {
            rect = CreateNoteRectangle(noteNumber, channel, velocity, rectHeight);
        }

        return rect;
    }

    private void ReturnRectangleToPool(Rectangle rect)
    {
        rect.Visibility = Visibility.Collapsed;
        _rectPool.Add(rect);
    }

    private void UpdateRectangle(Rectangle rect, int noteNumber, int channel, int velocity, double rectHeight)
    {
        var canvasWidth = _canvas.ActualWidth;
        var whiteKeyWidth = canvasWidth / _piano.WhiteKeyCount;
        var isBlack = _piano.GetKeyType(noteNumber) == PianoControlWpf.KeyType.Black;
        var rectWidth = isBlack ? whiteKeyWidth * 0.6 : whiteKeyWidth;

        var baseColor = ChannelColors[(channel - 1) % 16];
        var brightness = 0.5 + (velocity / 127.0) * 0.5;
        var fillColor = Color.FromRgb((byte)(baseColor.R * brightness), (byte)(baseColor.G * brightness),
            (byte)(baseColor.B * brightness));

        rect.Width = rectWidth;
        rect.Height = rectHeight;
        rect.Fill = new SolidColorBrush(fillColor);
        rect.Tag = noteNumber;
    }

    private void SendBoundKeyDown(int midiNoteNumber)
    {
        var scanCode = _piano.GetBoundScanCode(midiNoteNumber);
        if (scanCode is > 0) KeyboardInterop.SendKeyDown(scanCode.Value);
    }

    private void SendBoundKeyUp(int midiNoteNumber)
    {
        var scanCode = _piano.GetBoundScanCode(midiNoteNumber);
        if (scanCode is > 0) KeyboardInterop.SendKeyUp(scanCode.Value);
    }
}