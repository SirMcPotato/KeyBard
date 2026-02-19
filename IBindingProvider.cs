namespace KeyBard;

public interface IBindingProvider
{
    ushort? GetBinding(int midiNoteNumber);
    void SetBinding(int midiNoteNumber, ushort scanCode);
    void ClearBinding(int midiNoteNumber);
    void ClearAll();
    Dictionary<int, ushort> Export();
    void Import(Dictionary<int, ushort> bindings);
}

public class BindingProvider : IBindingProvider
{
    private readonly Dictionary<int, ushort> _bindings = new();

    public ushort? GetBinding(int midiNoteNumber)
    {
        return _bindings.TryGetValue(midiNoteNumber, out var scanCode) ? scanCode : null;
    }

    public void SetBinding(int midiNoteNumber, ushort scanCode)
    {
        _bindings[midiNoteNumber] = scanCode;
    }

    public void ClearBinding(int midiNoteNumber)
    {
        _bindings.Remove(midiNoteNumber);
    }

    public void ClearAll()
    {
        _bindings.Clear();
    }

    public Dictionary<int, ushort> Export()
    {
        return new Dictionary<int, ushort>(_bindings);
    }

    public void Import(Dictionary<int, ushort> bindings)
    {
        _bindings.Clear();
        foreach (var kvp in bindings)
        {
            _bindings[kvp.Key] = kvp.Value;
        }
    }
}
