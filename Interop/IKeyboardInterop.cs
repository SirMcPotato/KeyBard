using System.Windows.Input;

namespace KeyBard.Interop;

public interface IKeyboardInterop
{
    ushort KeyToScanCode(Key key);
    Key ScanCodeToKey(ushort scanCode);
    string GetDisplayStringForScanCode(ushort scanCode);
    void SendKeyDown(ushort scanCode);
    void SendKeyUp(ushort scanCode);
}
