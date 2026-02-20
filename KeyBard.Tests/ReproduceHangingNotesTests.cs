using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KeyBard.Midi;
using NAudio.Midi;
using Xunit;

public class ReproduceHangingNotesTests
{
    private sealed class FakeMidiOutput : IMidiOutput
    {
        public readonly List<int> Messages = new();
        public void Send(int message)
        {
            lock (Messages)
            {
                Messages.Add(message);
            }
        }
    }

    [Fact]
    public async Task Pause_Should_Stop_Active_Notes()
    {
        var events = new MidiEventCollection(1, 480);
        var track = new List<MidiEvent>
        {
            new NoteOnEvent(0, 1, 60, 100, 0),
            new NoteEvent(4800, 1, MidiCommandCode.NoteOff, 60, 0)
        };
        events.AddTrack(track);

        var tmp = Path.Combine(Path.GetTempPath(), $"kb_pause_test_{Guid.NewGuid():N}.mid");
        MidiFile.Export(tmp, events);
        var file = new MidiFile(tmp, false);

        var fake = new FakeMidiOutput();
        using var player = new MidiPlayer(fake);
        player.IsMuted = false;
        player.LoadMidi(file);
        
        player.Play();

        // Wait for 1s delay + some time for note to start
        await Task.Delay(1200);

        bool noteStarted = false;
        lock (fake.Messages)
        {
            noteStarted = fake.Messages.Exists(m => (m & 0xF0) == 0x90 && ((m >> 8) & 0xFF) == 60 && ((m >> 16) & 0xFF) > 0);
        }
        Assert.True(noteStarted, "Note should have started");

        int countBeforePause = fake.Messages.Count;

        // Pause
        player.Pause();

        // Check for NoteOff or AllNotesOff
        bool noteStopped = false;
        lock (fake.Messages)
        {
            for (int i = countBeforePause; i < fake.Messages.Count; i++)
            {
                int m = fake.Messages[i];
                if (((m & 0xF0) == 0x80 && ((m >> 8) & 0xFF) == 60) || 
                    ((m & 0xF0) == 0x90 && ((m >> 8) & 0xFF) == 60 && ((m >> 16) & 0xFF) == 0) ||
                    ((m & 0xF0) == 0xB0 && ((m >> 8) & 0xFF) == 123))
                {
                    noteStopped = true;
                    break;
                }
            }
        }

        Assert.True(noteStopped, "Note should have stopped when pausing");
    }

    [Fact]
    public async Task Restart_Should_Stop_Active_Notes()
    {
        var events = new MidiEventCollection(1, 480);
        var track = new List<MidiEvent>
        {
            new NoteOnEvent(0, 1, 60, 100, 0),
            new NoteEvent(4800, 1, MidiCommandCode.NoteOff, 60, 0)
        };
        events.AddTrack(track);

        var tmp = Path.Combine(Path.GetTempPath(), $"kb_restart_test_{Guid.NewGuid():N}.mid");
        MidiFile.Export(tmp, events);
        var file = new MidiFile(tmp, false);

        var fake = new FakeMidiOutput();
        using var player = new MidiPlayer(fake);
        player.IsMuted = false;
        player.LoadMidi(file);
        
        player.Play();

        // Wait for 1s delay + some time for note to start
        await Task.Delay(1200);

        // Restarting is basically setting ElapsedMs = 0 then Play()
        int countBeforeRestart = fake.Messages.Count;
        player.ElapsedMs = 0;
        
        // Wait for loop to catch up if it's playing
        await Task.Delay(100);
        player.Play();

        bool noteStopped = false;
        lock (fake.Messages)
        {
            for (int i = countBeforeRestart; i < fake.Messages.Count; i++)
            {
                int m = fake.Messages[i];
                if (((m & 0xF0) == 0x80 && ((m >> 8) & 0xFF) == 60) || 
                    ((m & 0xF0) == 0x90 && ((m >> 8) & 0xFF) == 60 && ((m >> 16) & 0xFF) == 0) ||
                    ((m & 0xF0) == 0xB0 && ((m >> 8) & 0xFF) == 123))
                {
                    noteStopped = true;
                    break;
                }
            }
        }

        Assert.True(noteStopped, "Note should have stopped when restarting (seeking to 0)");
    }

}
