using System.IO;
using System.Collections.Generic;
using System.Windows.Input;
using KeyBard.Controls;
using KeyBard.Interop;
using Xunit;
using KeyBard;

namespace KeyBard.Tests;

public class KeyBindingsStoreTests
{
    private class MockKeyboardInterop : IKeyboardInterop
    {
        public ushort KeyToScanCode(Key key) => (ushort)key;
        public Key ScanCodeToKey(ushort scanCode) => (Key)scanCode;
        public string GetDisplayStringForScanCode(ushort scanCode) => ((Key)scanCode).ToString();
        public void SendKeyDown(ushort scanCode) { }
        public void SendKeyUp(ushort scanCode) { }
    }

    [UIFact]
    public void RoundTrip_WithPianoControl_ShouldPreserveBindings()
    {
        // Arrange
        var filePath = "roundtrip_bindings.json";
        var control1 = new PianoControlWpf();
        var mockInterop = new MockKeyboardInterop();
        control1.SetKeyboardInterop(mockInterop);
        
        var buildKeysMethod = typeof(PianoControlWpf).GetMethod("BuildKeys", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        buildKeysMethod?.Invoke(control1, null);

        var originalBindings = new Dictionary<int, ushort>
        {
            { 60, (ushort)Key.A },
            { 62, (ushort)Key.S },
            { 64, (ushort)Key.D }
        };
        control1.ImportBindings(originalBindings);

        try
        {
            // Act
            var exported = control1.ExportBindings();
            KeyBindingsStore.Save(filePath, exported);
            
            var loaded = KeyBindingsStore.Load(filePath);
            var control2 = new PianoControlWpf();
            control2.SetKeyboardInterop(mockInterop);
            buildKeysMethod?.Invoke(control2, null);
            control2.ImportBindings(loaded);

            // Assert
            var finalBindings = control2.ExportBindings();
            Assert.Equal(originalBindings.Count, finalBindings.Count);
            foreach (var kvp in originalBindings)
            {
                Assert.Equal(kvp.Value, finalBindings[kvp.Key]);
            }
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPreserveBindings()
    {
        // Arrange
        var filePath = "test_bindings.json";
        var expectedBindings = new Dictionary<int, ushort>
        {
            { 60, 0x1E }, // Middle C -> 'A' scan code
            { 62, 0x1F }  // D4 -> 'S' scan code
        };

        try
        {
            // Act
            KeyBindingsStore.Save(filePath, expectedBindings);
            var actualBindings = KeyBindingsStore.Load(filePath);

            // Assert
            Assert.NotNull(actualBindings);
            Assert.Equal(expectedBindings.Count, actualBindings.Count);
            foreach (var key in expectedBindings.Keys)
            {
                Assert.Equal(expectedBindings[key], actualBindings[key]);
            }
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void Load_NonExistentFile_ShouldThrowException()
    {
        // Arrange
        var filePath = "non_existent.json";

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => KeyBindingsStore.Load(filePath));
    }
}
