using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KeyBard.Interop;

namespace KeyBard.Controls;

public partial class PianoKeyWpf
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private readonly Rectangle _rect = new();

    public readonly PianoControlWpf.KeyType KeyType;
    public readonly int KeyId;
    public readonly int MidiNoteNumber;
    private readonly IKeyboardInterop _keyboardInterop;

    private readonly LinearGradientBrush _whiteKeyOnBrush;
    private readonly LinearGradientBrush _blackKeyOnBrush;

    private readonly SolidColorBrush _whiteKeyOffBrush = new(Colors.White);

    public bool IsPianoKeyPressed { get; private set; }

    public bool IsAwaitingBinding { get; private set; }

    public ushort? BoundScanCode { get; private set; }

    public event Action<PianoKeyWpf>? BindingRequested;
    public event Action<int, ushort?>? BindingChanged;

    public PianoKeyWpf(PianoControlWpf.KeyType keyType, int keyId, int midiNoteNumber, IKeyboardInterop keyboardInterop)
    {
        _keyboardInterop = keyboardInterop;
        KeyType = keyType;
        KeyId = keyId;
        MidiNoteNumber = midiNoteNumber;
        IsPianoKeyPressed = false;
        Content = _rect;

        _whiteKeyOnBrush = new LinearGradientBrush();
        _whiteKeyOnBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
        _whiteKeyOnBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x20, 0x20, 0x20), 1.0));

        _blackKeyOnBrush = new LinearGradientBrush();
        _blackKeyOnBrush.GradientStops.Add(new GradientStop(Colors.LightGray, 0.0));
        _blackKeyOnBrush.GradientStops.Add(new GradientStop(Colors.Black, 1.0));

        SetDefaultColor();

        InitializeComponent();

        var noteName = NoteNames[midiNoteNumber % 12];
        var octave = (midiNoteNumber / 12) - 1;
        NoteLabel.Text = $"{noteName}{octave}";

        if (keyType == PianoControlWpf.KeyType.Black)
        {
            NoteLabel.Foreground = new SolidColorBrush(Colors.LightGray);
            BoundKeyLabel.Foreground = new SolidColorBrush(Colors.Orange);
        }

        MouseLeftButtonDown += OnKeyClicked;
    }

    private void OnKeyClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        BindingRequested?.Invoke(this);
    }

    public void EnterBindingMode()
    {
        IsAwaitingBinding = true;
        InnerBorder.BorderBrush = new SolidColorBrush(Colors.Gold);
        InnerBorder.BorderThickness = new Thickness(2);
    }

    public void ExitBindingMode()
    {
        IsAwaitingBinding = false;
        InnerBorder.BorderBrush = new SolidColorBrush(Colors.Black);
        InnerBorder.BorderThickness = new Thickness(0.5);
    }

    /// <summary>
    /// Bind a hardware scan code to this piano key.
    /// </summary>
    public void SetBinding(ushort? scanCode)
    {
        BoundScanCode = scanCode;
        if (scanCode.HasValue)
        {
            BoundKeyLabel.Text = _keyboardInterop.GetDisplayStringForScanCode(scanCode.Value);
        }
        else
        {
            BoundKeyLabel.Text = "";
        }
        ExitBindingMode();
        BindingChanged?.Invoke(MidiNoteNumber, scanCode);
    }

    public void UpdateBinding(ushort? scanCode)
    {
        BoundScanCode = scanCode;
        if (scanCode.HasValue)
        {
            BoundKeyLabel.Text = _keyboardInterop.GetDisplayStringForScanCode(scanCode.Value);
        }
        else
        {
            BoundKeyLabel.Text = "";
        }
    }

    /// <summary>
    /// Refreshes the display label based on the current keyboard layout.
    /// Call this if the user switches keyboard layout.
    /// </summary>
    public void RefreshBindingLabel()
    {
        if (BoundScanCode.HasValue)
            BoundKeyLabel.Text = _keyboardInterop.GetDisplayStringForScanCode(BoundScanCode.Value);
    }

    private void SetDefaultColor()
    {
        _rect.Fill = KeyType == PianoControlWpf.KeyType.White ? Brushes.White : Brushes.Black;
        _rect.Stroke = Brushes.Gray;
        _rect.StrokeThickness = 1;
    }

    public void PressPianoKey()
    {
        if (IsPianoKeyPressed) return;
        InnerBorder.Background = KeyType == PianoControlWpf.KeyType.White ? _whiteKeyOnBrush : _blackKeyOnBrush;
        IsPianoKeyPressed = true;
    }

    public void ReleasePianoKey()
    {
        if (!IsPianoKeyPressed) return;
        InnerBorder.Background = _whiteKeyOffBrush;
        IsPianoKeyPressed = false;
    }

    public Color NoteOnColor
    {
        set
        {
            Brush? brush = KeyType == PianoControlWpf.KeyType.White ? _whiteKeyOnBrush : _blackKeyOnBrush;
            InnerBorder.Background = brush;
        }
    }

    public Color NoteOffColor
    {
        get => _whiteKeyOffBrush.Color;
        set
        {
            _whiteKeyOffBrush.Color = value;
            InnerBorder.Background = _whiteKeyOffBrush;
        }
    }
}