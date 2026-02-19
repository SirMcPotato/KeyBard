using System.Windows.Input;

namespace KeyBard.Interop;

public class KeyboardInteropWrapper : IKeyboardInterop
{
    public ushort KeyToScanCode(Key key) => KeyboardInterop.KeyToScanCode(key);
    public Key ScanCodeToKey(ushort scanCode) => KeyboardInterop.ScanCodeToKey(scanCode);
    public string GetDisplayStringForScanCode(ushort scanCode) => KeyboardInterop.GetDisplayStringForScanCode(scanCode);
    public void SendKeyDown(ushort scanCode) => KeyboardInterop.SendKeyDown(scanCode);
    public void SendKeyUp(ushort scanCode) => KeyboardInterop.SendKeyUp(scanCode);
}
