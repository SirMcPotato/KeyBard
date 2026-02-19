using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KeyBard.Midi;
using NAudio.Midi;
using Xunit;

namespace KeyBard.Tests;

public class MidiPlayerReproTests
{
    private sealed class FakeMidiOutput : IMidiOutput
    {
        public void Send(int message) { }
    }

    [Fact]
    public async Task ElapsedMs_Should_Be_Preserved_After_Playback_Finishes()
    {
        // Build a tiny MIDI with one NoteOn and one NoteOff
        var events = new MidiEventCollection(1, 480);
        var track = new List<MidiEvent>
        {
            new NoteOnEvent(0, 1, 60, 100, 0),
            new NoteEvent(480, 1, MidiCommandCode.NoteOff, 60, 0) // approx 500ms
        };
        events.AddTrack(track);

        var tmp = Path.Combine(Path.GetTempPath(), $"kb_repro_{Guid.NewGuid():N}.mid");
        try
        {
            MidiFile.Export(tmp, events);
            var file = new MidiFile(tmp, false);

            var fake = new FakeMidiOutput();
            var player = new MidiPlayer(fake) { PlaybackSpeed = 100.0 }; // super fast
            player.LoadMidi(file);

            var finished = new TaskCompletionSource<bool>();
            player.PlaybackFinished += () => finished.TrySetResult(true);
            
            player.Play();

            // Wait for it to finish
            var completed = await Task.WhenAny(finished.Task, Task.Delay(2000));
            Assert.Same(finished.Task, completed);

            // Check ElapsedMs - it should be at the end, not 0
            Assert.True(player.ElapsedMs > 0, $"ElapsedMs should be > 0 after finishing, but was {player.ElapsedMs}");
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
