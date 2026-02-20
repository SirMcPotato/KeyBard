using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KeyBard.Midi;
using NAudio.Midi;
using Xunit;

public class ReproduceMuteIssueTests
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
    public async Task Mute_During_Note_Should_Stop_Note()
    {
        // Setup a MIDI file with a very long note
        var events = new MidiEventCollection(1, 480);
        var track = new List<MidiEvent>
        {
            new NoteOnEvent(0, 1, 60, 100, 0),
            new NoteEvent(4800, 1, MidiCommandCode.NoteOff, 60, 0) // very long note
        };
        events.AddTrack(track);

        var tmp = Path.Combine(Path.GetTempPath(), $"kb_repro_{Guid.NewGuid():N}.mid");
        MidiFile.Export(tmp, events);
        var file = new MidiFile(tmp, false);

        var fake = new FakeMidiOutput();
        using var player = new MidiPlayer(fake);
        player.IsMuted = false;
        player.LoadMidi(file);
        
        player.Play();

        // Wait a bit for the 1s delay + note to start
        await Task.Delay(1200);

        // Verify NoteOn was sent
        lock (fake.Messages)
        {
            Assert.Contains(fake.Messages, m => (m & 0xF0) == 0x90 && ((m >> 8) & 0xFF) == 60 && ((m >> 16) & 0xFF) > 0);
        }

        int countBeforeMute = fake.Messages.Count;

        // Mute the player
        player.IsMuted = true;

        // Check if NoteOff (or AllNotesOff) was sent for that note/channel
        bool noteStopped = false;
        lock (fake.Messages)
        {
            for (int i = countBeforeMute; i < fake.Messages.Count; i++)
            {
                int m = fake.Messages[i];
                int status = m & 0xF0;
                int channel = (m & 0x0F) + 1;
                int data1 = (m >> 8) & 0xFF;
                int data2 = (m >> 16) & 0xFF;

                // NoteOff (0x80) or NoteOn with velocity 0 (0x90) or AllNotesOff (CC 123)
                if ((status == 0x80 && data1 == 60) || 
                    (status == 0x90 && data1 == 60 && data2 == 0) ||
                    (status == 0xB0 && data1 == 123))
                {
                    noteStopped = true;
                    break;
                }
            }
        }

        Assert.True(noteStopped, "Note should have been stopped when muting");
    }

    [Fact]
    public async Task Unmute_During_Note_Should_Resume_Note()
    {
        // Setup a MIDI file with a long note
        var events = new MidiEventCollection(1, 480);
        var track = new List<MidiEvent>
        {
            new NoteOnEvent(0, 1, 60, 100, 0),
            new NoteEvent(4800, 1, MidiCommandCode.NoteOff, 60, 0) // long note
        };
        events.AddTrack(track);

        var tmp = Path.Combine(Path.GetTempPath(), $"kb_repro_unmute_{Guid.NewGuid():N}.mid");
        MidiFile.Export(tmp, events);
        var file = new MidiFile(tmp, false);

        var fake = new FakeMidiOutput();
        using var player = new MidiPlayer(fake);
        player.IsMuted = true; // Start muted
        player.LoadMidi(file);
        
        player.Play();

        // Wait a bit while muted (1s delay + 200ms of note)
        await Task.Delay(1200);

        // Verify NO NoteOn was sent (or it was sent but ignored if player handles it internally)
        // Actually, current implementation sends it to midiOut anyway because it only mutes via Volume CC.
        // But if it's muted, we expect it to NOT be playing on the synth (which it currently does because only volume is 0).

        int countBeforeUnmute = fake.Messages.Count;

        // Unmute
        player.IsMuted = false;

        // Check if NoteOn was sent to "resume" the note
        bool noteResumed = false;
        lock (fake.Messages)
        {
            for (int i = countBeforeUnmute; i < fake.Messages.Count; i++)
            {
                int m = fake.Messages[i];
                int status = m & 0xF0;
                int data1 = (m >> 8) & 0xFF;
                int data2 = (m >> 16) & 0xFF;

                if (status == 0x90 && data1 == 60 && data2 > 0)
                {
                    noteResumed = true;
                    break;
                }
            }
        }

        Assert.True(noteResumed, "Note should have been resumed when unmuting");
    }
}
