using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using KeyBard.Midi;
using KeyBard.Visualizer;
using Microsoft.Win32;
using NAudio.Midi;

namespace KeyBard;

public partial class MainWindow : Window
{
    private MidiVisualizer? _visualizer;
    private MidiOut? _midiOut;
    private MidiPlayer? _player;
    private MidiFile? _midiFile;
    private string? _currentMidiPath;
    private double _totalDurationMs;
    private bool _isDraggingProgress;
    public ObservableCollection<ChannelFilterItem> Channels { get; } = new();

    public MainWindow()
    {
        if (!InitializeMidi())
        {
            Application.Current.Shutdown();
            return;
        }

        InitializeComponent();
        InitializeChannels();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        SizeChanged += MainWindow_SizeChanged;
    }

    private System.Timers.Timer? _resizeTimer;

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Debounce resize events to avoid flickering and heavy RebuildVisuals calls
        _resizeTimer?.Stop();
        _resizeTimer ??= new System.Timers.Timer(100) { AutoReset = false };
        _resizeTimer.Elapsed += (s, ev) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Progress.UpdateLoopMarkers(_loopStartMs, _loopEndMs, _totalDurationMs);
            }));
        };
        _resizeTimer.Start();
    }

    private void InitializeChannels()
    {
        for (int i = 1; i <= 16; i++)
        {
            var color = MidiVisualizer.ChannelColors[i - 1];
            var hexColor = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            var item = new ChannelFilterItem { ChannelNumber = i, IsActive = true, HexColor = hexColor };
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChannelFilterItem.IsActive))
                {
                    UpdateMidiChannels();
                }
            };
            Channels.Add(item);
        }

        ChannelFilter.SetChannelsSource(Channels);
    }

    private void UpdateMidiChannels()
    {
        if (_visualizer == null || _player == null) return;

        _visualizer.EnabledChannels.Clear();
        var activeChannels = Channels.Where(c => c.IsActive).Select(c => c.ChannelNumber).ToHashSet();
        
        foreach (var channel in activeChannels)
        {
            _visualizer.EnabledChannels.Add(channel);
        }

        for (int i = 1; i <= 16; i++)
        {
            _player.SetChannelEnabled(i, activeChannels.Contains(i));
        }

        _visualizer.OnEnabledChannelsChanged();
    }
    
    public bool InitializeMidi()
    {
        try
        {
            // Attempt to find the Microsoft GS Wavetable Synth or any other output device
            int deviceId = 0;
            bool foundBest = false;
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                var info = MidiOut.DeviceInfo(i);
                if (info.ProductName.Contains("Microsoft GS Wavetable Synth", StringComparison.OrdinalIgnoreCase))
                {
                    deviceId = i;
                    foundBest = true;
                    break;
                }
            }

            // Fallback to device 0 if nothing better found but devices exist
            if (!foundBest && MidiOut.NumberOfDevices > 0)
            {
                deviceId = 0;
            }

            if (MidiOut.NumberOfDevices == 0)
            {
                MessageBox.Show("No MIDI output devices found on this system.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            _midiOut = new MidiOut(deviceId);
            _player = new MidiPlayer(new KeyBard.Midi.MidiOutAdapter(_midiOut));
            _player.PlaybackFinished += () => Dispatcher.BeginInvoke(StopPlayback);
            
            // Log which device we're using for troubleshooting
            string deviceName = MidiOut.DeviceInfo(deviceId).ProductName;
            Debug.WriteLine($"[MIDI] Using output device {deviceId}: {deviceName}");
            
            // Ensure profiles directory exists
            if (!System.IO.Directory.Exists("profiles")) System.IO.Directory.CreateDirectory("profiles");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize MIDI device: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _visualizer = new MidiVisualizer(PianoRollCanvas, PianoKeyboard);
        if (_player != null) _visualizer.AttachPlayer(_player);

        // Ensure player initial mute state follows UI (muted by default)
        if (_player != null) _player.IsMuted = Settings.IsMuted;

        UpdateMidiChannels();
        LoadProfiles();
        UpdateProgressLoop();
    }

    private void LoadProfiles()
    {
        var profiles = System.IO.Directory.GetFiles("profiles", "*.json")
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .ToList();
        profiles.Insert(0, "Default");
        FileProfile.SetProfiles(profiles, "Default");
    }

    private void ProfileComboBox_SelectionChanged(object? sender, string profileName)
    {
        if (profileName == "Default")
        {
            // Optionally reset to a default state if needed
            return;
        }

        var path = System.IO.Path.Combine("profiles", profileName + ".json");
        if (System.IO.File.Exists(path))
        {
            try
            {
                var bindings = KeyBindingsStore.Load(path);
                PianoKeyboard.ImportBindings(bindings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading profile:\n{ex.Message}");
            }
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        StopPlayback();
        _visualizer?.Dispose();
        _player?.Dispose();
        _midiOut?.Dispose();

        // Ensure the application exits even if some background tasks are lingering
        Application.Current.Shutdown();
    }

    // ── Button handlers ──────────────────────────────────────────────

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open MIDI File",
            Filter = "MIDI Files (*.mid;*.midi)|*.mid;*.midi|All Files (*.*)|*.*",
            DefaultExt = ".mid"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadMidiFile(dialog.FileName);
        }
    }

    private void LoadMidiFile(string filePath)
    {
        _currentMidiPath = filePath;

        try
        {
            // Stop any current playback before loading a new file
            StopPlayback();

            _midiFile = new MidiFile(filePath, false);
            _player?.LoadMidi(_midiFile);
            _totalDurationMs = CalculateTotalDuration(_midiFile);
            Progress.SetTimes("00:00", FormatTime(_totalDurationMs));
            Progress.Maximum = _totalDurationMs;
            Progress.Value = 0;

            FileProfile.SetFileName(System.IO.Path.GetFileName(filePath), true);

            // Extract notes for visualization
            var (visualNotes, channelNames, usedChannels) = ExtractVisualNotes(_midiFile);
            _visualizer?.SetNotes(visualNotes);

            // Update channel filters to match used channels
            foreach (var channelItem in Channels)
            {
                bool exists = usedChannels.Contains(channelItem.ChannelNumber);
                channelItem.IsActive = exists;
                channelItem.ExistsInFile = exists;

                if (channelNames.TryGetValue(channelItem.ChannelNumber, out var name))
                    channelItem.InstrumentName = name;
                else
                    channelItem.InstrumentName = exists ? "Unknown Instrument" : "";
            }

            SetButtonStates(isStopped: true);

            // Enable Play since we now have a file handled by SetButtonStates or here
            // (SetButtonStates handles BtnPlay.IsEnabled = _midiFile != null;)

            // Auto-load neighboring keybindings if they exist
            string jsonPath = System.IO.Path.ChangeExtension(_currentMidiPath, ".json");
            if (System.IO.File.Exists(jsonPath))
            {
                try
                {
                    var bindings = KeyBindingsStore.Load(jsonPath);
                    PianoKeyboard.ImportBindings(bindings);
                }
                catch
                {
                    // Ignore errors during auto-load
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading MIDI file:\n{ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            _midiFile = null;
            _currentMidiPath = null;
            FileProfile.SetFileName("No file loaded", false);
        }
    }

    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        bool isCorrect = false;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                string file = files[0];
                string ext = System.IO.Path.GetExtension(file).ToLower();
                if (ext == ".mid" || ext == ".midi")
                {
                    isCorrect = true;
                }
            }
        }

        e.Effects = isCorrect ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                LoadMidiFile(files[0]);
            }
        }
    }

    private (List<MidiVisualizer.VisualNote> notes, Dictionary<int, string> channelNames, HashSet<int> usedChannels)
        ExtractVisualNotes(MidiFile midi)
    {
        var ticksPerQuarter = midi.DeltaTicksPerQuarterNote;
        var visualNotes = new List<MidiVisualizer.VisualNote>();
        var usedChannels = new HashSet<int>();
        var channelNames = new Dictionary<int, string>();

        // 1. First pass: Collect channel instruments from ALL tracks
        foreach (var track in midi.Events)
        {
            foreach (var midiEvent in track)
            {
                if (midiEvent is PatchChangeEvent patch)
                {
                    if (!channelNames.ContainsKey(patch.Channel))
                    {
                        channelNames[patch.Channel] = GeneralMidi.GetProgramName(patch.Patch);
                    }
                }

                if (midiEvent is NoteOnEvent { Velocity: > 0 } noteOn)
                {
                    usedChannels.Add(noteOn.Channel);
                    if (noteOn.Channel == 10 && !channelNames.ContainsKey(10)) channelNames[10] = "Percussion";
                }
            }
        }

        // 2. Second pass: Refine channel names using track names
        for (int i = 0; i < midi.Events.Tracks; i++)
        {
            var trackName = midi.Events[i]
                .OfType<TextEvent>()
                .FirstOrDefault(e => e.MetaEventType == MetaEventType.SequenceTrackName)
                ?.Text;

            if (!string.IsNullOrEmpty(trackName))
            {
                var trackChannels = midi.Events[i]
                    .OfType<NoteEvent>()
                    .Select(e => e.Channel)
                    .Distinct()
                    .ToList();

                foreach (var ch in trackChannels)
                {
                    if (channelNames.TryGetValue(ch, out var currentName))
                    {
                        if (!currentName.Contains(trackName))
                            channelNames[ch] = $"{trackName} ({currentName})";
                    }
                    else
                    {
                        channelNames[ch] = trackName;
                    }
                }
            }
        }

        // 3. Third pass: Extract actual notes for visualization
        // We still need a linear timeline for tempo changes
        var allEvents = midi.Events.SelectMany(t => t).OrderBy(e => e.AbsoluteTime).ToList();

        double currentTempoUsPerQuarter = 500_000;
        long lastTick = 0;
        double currentMs = 0;

        var openNotes = new Dictionary<(int, int), (long StartTick, double StartMs, int Velocity)>();

        foreach (var midiEvent in allEvents)
        {
            var deltaTicks = midiEvent.AbsoluteTime - lastTick;
            currentMs += deltaTicks * currentTempoUsPerQuarter / ticksPerQuarter / 1000.0;
            lastTick = midiEvent.AbsoluteTime;

            if (midiEvent is TempoEvent tempo) currentTempoUsPerQuarter = tempo.MicrosecondsPerQuarterNote;

            if (midiEvent is NoteOnEvent noteOn)
            {
                var key = (noteOn.Channel, noteOn.NoteNumber);
                if (noteOn.Velocity > 0)
                {
                    if (openNotes.Remove(key, out var existing))
                    {
                        var durationTicks = noteOn.AbsoluteTime - existing.StartTick;
                        var durationMs = durationTicks * currentTempoUsPerQuarter / ticksPerQuarter / 1000.0;
                        visualNotes.Add(new MidiVisualizer.VisualNote
                        {
                            NoteNumber = key.Item2,
                            Channel = key.Item1,
                            StartMs = existing.StartMs,
                            DurationMs = durationMs,
                            Velocity = existing.Velocity
                        });
                    }

                    openNotes[key] = (noteOn.AbsoluteTime, currentMs, noteOn.Velocity);
                }
                else
                {
                    if (openNotes.Remove(key, out var startInfo))
                    {
                        var durationTicks = noteOn.AbsoluteTime - startInfo.StartTick;
                        var durationMs = durationTicks * currentTempoUsPerQuarter / ticksPerQuarter / 1000.0;
                        visualNotes.Add(new MidiVisualizer.VisualNote
                        {
                            NoteNumber = noteOn.NoteNumber,
                            Channel = noteOn.Channel,
                            StartMs = startInfo.StartMs,
                            DurationMs = durationMs,
                            Velocity = startInfo.Velocity
                        });
                    }
                }
            }
            else if (midiEvent is NoteEvent noteOff && noteOff.CommandCode == MidiCommandCode.NoteOff)
            {
                var key = (noteOff.Channel, noteOff.NoteNumber);
                if (openNotes.Remove(key, out var startInfo))
                {
                    var durationTicks = noteOff.AbsoluteTime - startInfo.StartTick;
                    var durationMs = durationTicks * currentTempoUsPerQuarter / ticksPerQuarter / 1000.0;
                    visualNotes.Add(new MidiVisualizer.VisualNote
                    {
                        NoteNumber = noteOff.NoteNumber,
                        Channel = noteOff.Channel,
                        StartMs = startInfo.StartMs,
                        DurationMs = durationMs,
                        Velocity = startInfo.Velocity
                    });
                }
            }
        }

        return (visualNotes, channelNames, usedChannels);
    }

    private void BtnPlay_Click(object? sender, EventArgs e)
    {
        if (_midiFile == null || _player == null) return;

        if (_player.IsPlaying)
        {
            _player.Resume();
            _visualizer?.Start();
            SetButtonStates(isPlaying: true);
            return;
        }

        StartPlayback();
    }

    private void BtnPause_Click(object? sender, EventArgs e)
    {
        _player?.Pause();
        _visualizer?.Pause();
        SetButtonStates(isPaused: true);
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        StopPlayback();
        SetButtonStates(isStopped: true);
    }

    private void BtnRestart_Click(object? sender, EventArgs e)
    {
        if (_player == null) return;
        _player.ElapsedMs = 0;
        _visualizer?.Reset();
        _visualizer?.Start();
        _player.Play();
        SetButtonStates(isPlaying: true);
    }

    private void BtnSaveBindings_Click(object? sender, EventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Key Bindings",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "keybindings.json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var bindings = PianoKeyboard.ExportBindings();
            KeyBindingsStore.Save(dialog.FileName, bindings);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving bindings:\n{ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BtnSaveProfileAs_Click(object? sender, EventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Profile As",
            InitialDirectory = System.IO.Path.GetFullPath("profiles"),
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "New Profile.json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var fileName = dialog.FileName;
            var profilesDir = System.IO.Path.GetFullPath("profiles");

            // Ensure we are saving in the profiles directory
            if (!fileName.StartsWith(profilesDir, StringComparison.OrdinalIgnoreCase))
            {
                var result = MessageBox.Show(
                    "Profiles should be saved in the 'profiles' folder to appear in the dropdown. Save there instead?",
                    "Save Location", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var nameOnly = System.IO.Path.GetFileName(fileName);
                    fileName = System.IO.Path.Combine(profilesDir, nameOnly);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            var bindings = PianoKeyboard.ExportBindings();
            KeyBindingsStore.Save(fileName, bindings);

            // Refresh profiles list and select the new one
            LoadProfiles();
            var newProfileName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            FileProfile.SetProfiles(
                FileProfile.SelectedProfile != null
                    ? (IEnumerable<string>)FileProfile.ProfileComboBox.ItemsSource
                    : new List<string>(), newProfileName);
            // Actually, LoadProfiles already calls FileProfile.SetProfiles.
            // But we want to select the new one.
            LoadProfiles();
            // Need a way to set selected profile in FileProfileControl.
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving profile:\n{ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BtnSaveForSong_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_currentMidiPath)) return;

        try
        {
            string jsonPath = System.IO.Path.ChangeExtension(_currentMidiPath, ".json");
            var bindings = PianoKeyboard.ExportBindings();
            KeyBindingsStore.Save(jsonPath, bindings);
            MessageBox.Show($"Bindings saved for song:\n{jsonPath}", "Success", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving bindings for song:\n{ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BtnLoadBindings_Click(object? sender, EventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Key Bindings",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var bindings = KeyBindingsStore.Load(dialog.FileName);
            PianoKeyboard.ImportBindings(bindings);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading bindings:\n{ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BtnClearBindings_Click(object? sender, EventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to clear ALL key bindings?", "Confirm Clear",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            PianoKeyboard.ClearAllBindings();
        }
    }

    private void VolumeSlider_ValueChanged(object? sender, double newValue)
    {
        if (_player != null) _player.Volume = newValue / 100.0;
    }

    private void SpeedSlider_ValueChanged(object? sender, double newValue)
    {
        if (_player != null) _player.PlaybackSpeed = newValue;
    }

    private double _loopStartMs = -1;
    private double _loopEndMs = -1;

    private void BtnSetLoopStart_Click(object? sender, EventArgs e)
    {
        if (_player == null) return;
        double currentPos = _player.ElapsedMs;
        if (currentPos <= 0 && _totalDurationMs > 0 && !_player.IsPlaying && !_player.IsPaused)
        {
            currentPos = 0; // it's already 0 but just to be explicit
        }

        _loopStartMs = currentPos;
        _player.LoopStartMs = _loopStartMs;
        Playback.UpdateLoopStart($"A: {FormatTime(_loopStartMs)}");
        CheckLoopPoints();
        Progress.UpdateLoopMarkers(_loopStartMs, _loopEndMs, _totalDurationMs);
    }

    private void BtnSetLoopEnd_Click(object? sender, EventArgs e)
    {
        if (_player == null) return;
        // If playback finished, ElapsedMs might be 0 because Stop() resets it.
        // But if the user clicks "Set B" they probably want the end of the song if it ended.
        double currentPos = _player.ElapsedMs;
        if (currentPos <= 0 && _totalDurationMs > 0 && !_player.IsPlaying && !_player.IsPaused)
        {
            currentPos = _totalDurationMs;
        }

        _loopEndMs = currentPos;
        _player.LoopEndMs = _loopEndMs;
        Playback.UpdateLoopEnd($"B: {FormatTime(_loopEndMs)}");
        CheckLoopPoints();
        Progress.UpdateLoopMarkers(_loopStartMs, _loopEndMs, _totalDurationMs);
    }

    private void BtnClearLoop_Click(object? sender, EventArgs e)
    {
        _loopStartMs = -1;
        _loopEndMs = -1;
        if (_player != null)
        {
            _player.LoopStartMs = -1;
            _player.LoopEndMs = -1;
        }

        Playback.ResetLoopButtons();
        Progress.UpdateLoopMarkers(_loopStartMs, _loopEndMs, _totalDurationMs);
    }

    private void CheckLoopPoints()
    {
        if (_loopStartMs != -1 && _loopEndMs != -1 && _loopStartMs > _loopEndMs)
        {
            // Swap if out of order
            (_loopStartMs, _loopEndMs) = (_loopEndMs, _loopStartMs);
            Playback.UpdateLoopStart($"A: {FormatTime(_loopStartMs)}");
            Playback.UpdateLoopEnd($"B: {FormatTime(_loopEndMs)}");
        }
    }

    private void BtnMute_Click(object? sender, bool isMuted)
    {
        if (_player != null) _player.IsMuted = isMuted;
    }

    // ── Playback logic ───────────────────────────────────────────────

    private void StartPlayback()
    {
        if (_midiFile == null || _player == null) return;

        StopPlayback();
        _visualizer?.Start();
        _player.Play();
        SetButtonStates(isPlaying: true);
        FileProfile.SetBrowseEnabled(false);
    }

    private void StopPlayback()
    {
        _player?.Stop();
        _visualizer?.Reset();
        SetButtonStates(isStopped: true);
        FileProfile.SetBrowseEnabled(true);
    }

    private void SetButtonStates(bool isPlaying = false, bool isPaused = false, bool isStopped = false)
    {
        Playback.SetPlaybackState(isPlaying, isPaused, _midiFile != null);

        if (isPlaying || isPaused)
        {
            FileProfile.SetBrowseEnabled(false);
        }
        else // stopped
        {
            FileProfile.SetBrowseEnabled(true);
            if (isStopped)
            {
                Progress.Value = 0;
                Progress.SetTimes("00:00", FormatTime(_totalDurationMs));
            }
        }
    }

    private double CalculateTotalDuration(MidiFile midi)
    {
        var ticksPerQuarter = midi.DeltaTicksPerQuarterNote;
        var allEvents = midi.Events.SelectMany(t => t).OrderBy(e => e.AbsoluteTime).ToList();

        double totalMs = 0;
        double currentTempoUsPerQuarter = 500_000;
        long lastTick = 0;

        foreach (var midiEvent in allEvents)
        {
            var deltaTicks = midiEvent.AbsoluteTime - lastTick;
            totalMs += deltaTicks * currentTempoUsPerQuarter / ticksPerQuarter / 1000.0;
            lastTick = midiEvent.AbsoluteTime;

            if (midiEvent is TempoEvent tempo) currentTempoUsPerQuarter = tempo.MicrosecondsPerQuarterNote;
        }

        return totalMs;
    }

    private string FormatTime(double ms)
    {
        var time = TimeSpan.FromMilliseconds(ms);
        return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
    }

    private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDraggingProgress = true;
    }

    private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDraggingProgress = false;
        if (_player != null)
        {
            _player.ElapsedMs = Progress.Value;
            _visualizer?.RebuildVisuals();
        }
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Progress.SetTimes(FormatTime(e.NewValue), FormatTime(_totalDurationMs));

        if (_player == null) return;

        // Update visuals if dragging while paused
        if (_isDraggingProgress && !_player.IsPlaying)
        {
            _player.ElapsedMs = e.NewValue;
            _visualizer?.RebuildVisuals();
        }
    }

    private void UpdateProgressLoop()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                if (_player != null && _player.IsPlaying && !_isDraggingProgress)
                {
                    Dispatcher.Invoke(() => { Progress.Value = _player.ElapsedMs; });
                }

                await Task.Delay(50);
            }
        });
    }

    public class ChannelFilterItem : INotifyPropertyChanged
    {
        public int ChannelNumber { get; init; }
        public string HexColor { get; init; } = "#FF4A90E2"; // Default color

        private bool _isActive;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged();
            }
        }

        private bool _existsInFile = true;

        public bool ExistsInFile
        {
            get => _existsInFile;
            set
            {
                if (_existsInFile == value) return;
                _existsInFile = value;
                OnPropertyChanged();
            }
        }

        private string _instrumentName = "";

        public string InstrumentName
        {
            get => _instrumentName;
            set
            {
                if (_instrumentName == value) return;
                _instrumentName = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}