using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KeyBard.Midi;
using NAudio.Midi;

public class MidiPlayerTests
{
    private sealed class FakeMidiOutput : IMidiOutput
    {
        public readonly List<int> Messages = new();
        public void Send(int message)
        {
            Messages.Add(message);
        }
    }

    private static int MakeShortMessage(int status, int data1, int data2)
    {
        return status | (data1 << 8) | (data2 << 16);
    }

    [Fact]
    public void Volume_And_Mute_Send_CC_On_All_Channels()
    {
        var fake = new FakeMidiOutput();
        var player = new MidiPlayer(fake);

        // When unmuted and volume set, it should push CC 7 across channels
        player.IsMuted = false;
        player.Volume = 0.5; // ~64

        // Expect at least 16 volume messages (one per channel), plus other CCs the implementation sends
        Assert.True(fake.Messages.Count >= 16);

        // Validate at least one message is CC for MainVolume (status 0xB0..0xBF, controller 7, value ~64)
        var volValues = new List<int>();
        foreach (var msg in fake.Messages)
        {
            int status = msg & 0xFF; // 0xB0..0xBF
            int data1 = (msg >> 8) & 0xFF; // controller
            int data2 = (msg >> 16) & 0xFF; // value
            if ((status & 0xF0) == 0xB0 && data1 == 7)
            {
                volValues.Add(data2);
            }
        }
        Assert.NotEmpty(volValues);
        Assert.Contains(volValues, v => v >= 60 && v <= 70);
    }

    [Fact]
    public void Stop_Sends_AllNotesOff_On_All_Channels()
    {
        var fake = new FakeMidiOutput();
        var player = new MidiPlayer(fake);

        player.Stop();

        int allNotesOffCount = 0;
        foreach (var msg in fake.Messages)
        {
            int status = msg & 0xFF; // 0xB0..0xBF
            int data1 = (msg >> 8) & 0xFF; // controller
            if ((status & 0xF0) == 0xB0 && data1 == 123)
                allNotesOffCount++;
        }

        Assert.Equal(16, allNotesOffCount);
    }

    [Fact]
    public async Task Play_Sends_NoteOn_And_NoteOff_For_Enabled_Channel()
    {
        // Build a tiny MIDI with one NoteOn and one NoteOff on channel 1
        var events = new MidiEventCollection(1, 480);
        var track = new List<MidiEvent>
        {
            new NoteOnEvent(0, 1, 60, 100, 0),      // at tick 0
            new NoteEvent(240, 1, MidiCommandCode.NoteOff, 60, 0) // after 240 ticks (approx 250ms)
        };
        events.AddTrack(track);

        var tmp = Path.Combine(Path.GetTempPath(), $"kb_test_{Guid.NewGuid():N}.mid");
        MidiFile.Export(tmp, events);
        var file = new MidiFile(tmp, false);

        var fake = new FakeMidiOutput();
        var player = new MidiPlayer(fake) { PlaybackSpeed = 50.0, IsMuted = false }; // accelerate and unmute
        player.LoadMidi(file);

        var finished = new TaskCompletionSource<bool>();
        player.PlaybackFinished += () => finished.TrySetResult(true);
        player.Play();

        // Wait up to a second for playback to complete
        await Task.WhenAny(finished.Task, Task.Delay(1000));
        player.Stop();

        // We expect at least one NoteOn (0x90) and one NoteOff (0x80 or NoteOn with velocity 0)
        bool sawNoteOn = false;
        bool sawNoteOff = false;
        foreach (var msg in fake.Messages)
        {
            int status = msg & 0xFF;
            int data1 = (msg >> 8) & 0xFF;
            int data2 = (msg >> 16) & 0xFF;
            if ((status & 0xF0) == 0x90 && data1 == 60 && data2 > 0) sawNoteOn = true;
            if (((status & 0xF0) == 0x80 && data1 == 60) || ((status & 0xF0) == 0x90 && data1 == 60 && data2 == 0))
                sawNoteOff = true;
        }

        Assert.True(sawNoteOn, "Expected a NoteOn for middle C");
        Assert.True(sawNoteOff, "Expected a NoteOff for middle C");
    }
}
