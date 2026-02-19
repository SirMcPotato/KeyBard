using System.IO;
using System.Text.Json;

namespace KeyBard;

public static class KeyBindingsStore
{
    /// <summary>
    /// Saves keybindings to a JSON file.
    /// Format: { "midiNote": scanCode, ... }
    /// </summary>
    public static void Save(string filePath, Dictionary<int, ushort> bindings)
    {
        var json = JsonSerializer.Serialize(bindings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Loads keybindings from a JSON file.
    /// </summary>
    public static Dictionary<int, ushort> Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<Dictionary<int, ushort>>(json) ??
               throw new InvalidOperationException("Invalid bindings file.");
    }
}