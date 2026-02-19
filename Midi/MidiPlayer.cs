using System.Diagnostics;
using NAudio.Midi;

namespace KeyBard.Midi;

public sealed class MidiPlayer(IMidiOutput midiOut) : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private readonly Stopwatch _stopwatch = new();
    private long _offsetMs;
    private double _playbackSpeed = 1.0;
    private double _volume = 1.0;
    private bool _isMuted = true;
    private readonly HashSet<int> _enabledChannels = new(Enumerable.Range(1, 16));
    private readonly List<NoteOnEvent> _activeNotes = new();
    private readonly object _notesLock = new();
    private readonly ManualResetEventSlim _seekEvent = new(false);

    private MidiFile? _midiFile;
    private List<MidiEvent> _allEvents = new();

    public event Action<MidiEvent>? MidiEventReceived;
    public event Action<double>? PositionChanged;
    public event Action? PlaybackFinished;

    public bool IsPlaying => _cts != null && !_cts.IsCancellationRequested;
    public bool IsPaused => !_pauseEvent.IsSet;

    public double ElapsedMs
    {
        get
        {
            lock (_stopwatch)
            {
                return _offsetMs + (_stopwatch.Elapsed.TotalMilliseconds * _playbackSpeed);
            }
        }
        set
        {
            lock (_stopwatch)
            {
                _offsetMs = (long)value;
                if (_stopwatch.IsRunning)
                {
                    _stopwatch.Restart();
                }
                else
                {
                    _stopwatch.Reset();
                }
            }

            // Any time jump should immediately stop currently sounding notes
            StopAllActiveNotes();
            _seekEvent.Set();
            PositionChanged?.Invoke(value);
        }
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (value <= 0) return;
            var current = ElapsedMs;
            _playbackSpeed = value;
            ElapsedMs = current;
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            if (!_isMuted) SendVolumeToAllChannels();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted == value) return;
            _isMuted = value;
            if (_isMuted)
            {
                StopAllActiveNotes();
            }
            else
            {
                ResumeActiveNotes();
            }
            SendVolumeToAllChannels();
        }
    }

    private void StopAllActiveNotes()
    {
        if (midiOut == null) return;
        lock (_notesLock)
        {
            foreach (var note in _activeNotes)
            {
                midiOut.Send(new NoteEvent(0, note.Channel, MidiCommandCode.NoteOff, note.NoteNumber, 0).GetAsShortMessage());
            }
        }
        // Also send All Notes Off to be sure
        for (int i = 1; i <= 16; i++)
        {
            midiOut.Send(new ControlChangeEvent(0, i, MidiController.AllNotesOff, 0).GetAsShortMessage());
        }
    }

    private void ResumeActiveNotes()
    {
        if (midiOut == null || _isMuted) return;
        lock (_notesLock)
        {
            foreach (var note in _activeNotes)
            {
                if (_enabledChannels.Contains(note.Channel))
                {
                    midiOut.Send(note.GetAsShortMessage());
                }
            }
        }
    }

    private void SendVolumeToAllChannels()
    {
        if (midiOut == null) return;
        int vol = _isMuted ? 0 : (int)(_volume * 127);
        for (int i = 1; i <= 16; i++)
        {
            try
            {
                // Reset All Controllers (CC 121) and All Notes Off (CC 123)
                // We only do this if it's the start of playback to avoid interrupting notes
                if (IsPlaying && ElapsedMs < 100)
                {
                    midiOut.Send(new ControlChangeEvent(0, i, MidiController.ResetAllControllers, 0).GetAsShortMessage());
                    midiOut.Send(new ControlChangeEvent(0, i, MidiController.AllNotesOff, 0).GetAsShortMessage());
                }

                midiOut.Send(new ControlChangeEvent(0, i, MidiController.MainVolume, vol).GetAsShortMessage());
                // Also send Expression Controller (CC 11) and reset Pan (CC 10) / Modulation (CC 1)
                midiOut.Send(new ControlChangeEvent(0, i, MidiController.Expression, 127).GetAsShortMessage());
                midiOut.Send(new ControlChangeEvent(0, i, MidiController.Pan, 64).GetAsShortMessage());
            }
            catch
            {
                // Ignore transient MIDI errors during mass-send
            }
        }
    }

    public double LoopStartMs { get; set; } = -1;
    public double LoopEndMs { get; set; } = -1;

    public ISet<int> EnabledChannels => _enabledChannels;

    public void SetChannelEnabled(int channel, bool enabled)
    {
        lock (_notesLock)
        {
            if (enabled)
            {
                if (_enabledChannels.Add(channel))
                {
                    ResumeActiveNotesForChannel(channel);
                }
            }
            else
            {
                if (_enabledChannels.Remove(channel))
                {
                    StopActiveNotesForChannel(channel);
                }
            }
        }
    }

    private void StopActiveNotesForChannel(int channel)
    {
        if (midiOut == null) return;
        lock (_notesLock)
        {
            foreach (var note in _activeNotes.Where(n => n.Channel == channel))
            {
                midiOut.Send(new NoteEvent(0, note.Channel, MidiCommandCode.NoteOff, note.NoteNumber, 0).GetAsShortMessage());
            }
            midiOut.Send(new ControlChangeEvent(0, channel, MidiController.AllNotesOff, 0).GetAsShortMessage());
        }
    }

    private void ResumeActiveNotesForChannel(int channel)
    {
        if (midiOut == null || _isMuted) return;
        lock (_notesLock)
        {
            foreach (var note in _activeNotes.Where(n => n.Channel == channel))
            {
                midiOut.Send(note.GetAsShortMessage());
            }
        }
    }

    public void LoadMidi(MidiFile midiFile)
    {
        Stop();
        _midiFile = midiFile;
        _allEvents = midiFile.Events.SelectMany(t => t).OrderBy(e => e.AbsoluteTime).ToList();
        ElapsedMs = 0;
    }

    public void Play()
    {
        if (IsPlaying)
        {
            if (IsPaused) Resume();
            return;
        }

        _cts = new CancellationTokenSource();
        _pauseEvent.Set();
        _stopwatch.Start();
        SendVolumeToAllChannels();

        Task.Run(() => PlaybackLoop(_cts.Token));
    }

    public void Pause()
    {
        _pauseEvent.Reset();
        _stopwatch.Stop();
        StopAllActiveNotes();
    }

    public void Resume()
    {
        _pauseEvent.Set();
        _stopwatch.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _pauseEvent.Set();
        _stopwatch.Stop();
        _stopwatch.Reset();
        _offsetMs = 0;

        lock (_notesLock)
        {
            _activeNotes.Clear();
        }

        // All Notes Off
        for (var i = 1; i <= 16; i++)
        {
            // Control Change 123 is "All Notes Off"
            midiOut.Send(new ControlChangeEvent(0, i, MidiController.AllNotesOff, 0).GetAsShortMessage());
        }
    }

    private async Task PlaybackLoop(CancellationToken ct)
    {
        RestartLoop:
        _seekEvent.Reset();
        StopAllActiveNotes();

        while (true)
        {
            if (_midiFile == null) return;

            var ticksPerQuarter = _midiFile.DeltaTicksPerQuarterNote;
            double currentTempoUsPerQuarter = 500_000;
            long lastTick = 0;
            double scheduledMs = 0;

            foreach (var midiEvent in _allEvents)
            {
                if (ct.IsCancellationRequested) return;
                if (_seekEvent.IsSet) goto RestartLoop;

                _pauseEvent.Wait(ct);
                if (ct.IsCancellationRequested) return;
                if (_seekEvent.IsSet) goto RestartLoop;

                var deltaTicks = midiEvent.AbsoluteTime - lastTick;
                var deltaMs = deltaTicks * currentTempoUsPerQuarter / ticksPerQuarter / 1000.0;
                scheduledMs += deltaMs;
                lastTick = midiEvent.AbsoluteTime;

                if (midiEvent is TempoEvent t1) currentTempoUsPerQuarter = t1.MicrosecondsPerQuarterNote;

                if (scheduledMs < ElapsedMs)
                {
                    // Catch-up / Seek handling: 
                    // Skip NoteEvents but process state-changing events (Patch, Volume, etc.)
                    // to ensure the synth is in the correct state.
                    if (midiEvent is NoteEvent)
                    {
                        // If we are significantly behind (e.g. during a seek), skip.
                        // If we just started, process to avoid silent start.
                        if (ElapsedMs - scheduledMs > 500) continue;
                    }
                }

                while (ElapsedMs < scheduledMs)
                {
                    if (ct.IsCancellationRequested) return;
                    if (_seekEvent.IsSet) goto RestartLoop;

                    // Loop check
                    if (LoopStartMs != -1 && LoopEndMs != -1 && ElapsedMs >= LoopEndMs)
                    {
                        ElapsedMs = LoopStartMs;
                        goto RestartLoop;
                    }

                    var remaining = scheduledMs - ElapsedMs;
                    if (remaining > 5)
                        await Task.Delay(1, ct);
                    else if (remaining > 0)
                    {
                        if (remaining > 0.5)
                            await Task.Yield();
                        else
                            Thread.SpinWait(100);
                    }
                }

                if (midiEvent is TempoEvent t2) currentTempoUsPerQuarter = t2.MicrosecondsPerQuarterNote;

                // Dispatch event
                MidiEventReceived?.Invoke(midiEvent);

                try
                {
                    if (midiEvent is NoteEvent noteEvent)
                    {
                        if (noteEvent is NoteOnEvent { Velocity: > 0 } noteOn)
                        {
                            lock (_notesLock)
                            {
                                // Remove any existing active note with same number and channel
                                _activeNotes.RemoveAll(n => n.Channel == noteOn.Channel && n.NoteNumber == noteOn.NoteNumber);
                                _activeNotes.Add(noteOn);
                            }
                            if (!_isMuted && _enabledChannels.Contains(noteOn.Channel))
                            {
                                midiOut.Send(noteOn.GetAsShortMessage());
                            }
                        }
                        else if (noteEvent.CommandCode == MidiCommandCode.NoteOff ||
                                 (noteEvent is NoteOnEvent { Velocity: 0 }))
                        {
                            lock (_notesLock)
                            {
                                _activeNotes.RemoveAll(n => n.Channel == noteEvent.Channel && n.NoteNumber == noteEvent.NoteNumber);
                            }
                            if (!_isMuted && _enabledChannels.Contains(noteEvent.Channel))
                            {
                                midiOut.Send(noteEvent.GetAsShortMessage());
                            }
                        }
                    }
                    else if (midiEvent is ControlChangeEvent cce)
                    {
                        if (_enabledChannels.Contains(cce.Channel))
                        {
                            // Intercept and merge user volume/mute if it's CC 7
                            int finalValue = cce.ControllerValue;
                            if (cce.Controller == MidiController.MainVolume)
                            {
                                int userVol = _isMuted ? 0 : (int)(_volume * 127);
                                finalValue = userVol;
                            }

                            midiOut.Send(new ControlChangeEvent(0, cce.Channel, cce.Controller, finalValue)
                                .GetAsShortMessage());
                        }
                    }
                    else if (midiEvent is PatchChangeEvent pce)
                    {
                        if (_enabledChannels.Contains(pce.Channel))
                            midiOut.Send(pce.GetAsShortMessage());
                    }
                    else if (midiEvent is PitchWheelChangeEvent pwce)
                    {
                        if (_enabledChannels.Contains(pwce.Channel))
                            midiOut.Send(pwce.GetAsShortMessage());
                    }
                    else if (midiEvent is ChannelAfterTouchEvent cate)
                    {
                        if (_enabledChannels.Contains(cate.Channel))
                            midiOut.Send(cate.GetAsShortMessage());
                    }
                    // SysEx events are handled differently but often not needed for simple playback.
                    // If needed, we could use NAudio's SysexEvent data here.
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MIDI] Error sending event: {ex.Message}");
                }
            }

            PlaybackFinished?.Invoke();
            return;
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}