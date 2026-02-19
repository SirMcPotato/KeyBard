using NAudio.Midi;

namespace KeyBard.Midi;

public interface IMidiOutput
{
    void Send(int message);
}

public sealed class MidiOutAdapter : IMidiOutput
{
    private readonly MidiOut _midiOut;
    public MidiOutAdapter(MidiOut midiOut)
    {
        _midiOut = midiOut;
    }
    public void Send(int message)
    {
        _midiOut.Send(message);
    }
}
