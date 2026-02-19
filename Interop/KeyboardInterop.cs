using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace KeyBard.Interop;

public static partial class KeyboardInterop
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeyboardInput ki;
        [FieldOffset(0)] public HardwareInput hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint type;
        public InputUnion U;
    }

    private static readonly int InputSize = Marshal.SizeOf<Input>();

    private const uint InputKeyboard = 1;
    private const uint KeyeventfScancode = 0x0008;
    private const uint KeyeventfKeyup = 0x0002;

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    private static partial uint MapVirtualKey(uint uCode, uint uMapType);

    [LibraryImport("user32.dll", EntryPoint = "GetKeyboardLayout")]
    private static partial IntPtr GetKeyboardLayout(uint idThread);

    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
    private static partial uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

    /// <summary>
    /// Converts a WPF Key to a hardware scan code.
    /// </summary>
    public static ushort KeyToScanCode(Key key)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        var scanCode = MapVirtualKey((uint)virtualKey, 0); // MAPVK_VK_TO_VSC
        return (ushort)scanCode;
    }

    /// <summary>
    /// Converts a hardware scan code back to a WPF Key (for the current keyboard layout).
    /// </summary>
    public static Key ScanCodeToKey(ushort scanCode)
    {
        var vk = MapVirtualKey(scanCode, 1); // MAPVK_VSC_TO_VK
        return KeyInterop.KeyFromVirtualKey((int)vk);
    }

    /// <summary>
    /// Gets a display-friendly string for a scan code based on the current keyboard layout.
    /// </summary>
    public static string GetDisplayStringForScanCode(ushort scanCode)
    {
        var hkl = GetKeyboardLayout(0);
        var vk = MapVirtualKeyEx(scanCode, 3, hkl); // MAPVK_VSC_TO_VK_EX

        if (vk == 0) return $"SC{scanCode:X2}";

        var keyState = new byte[256];
        var sb = new StringBuilder(4);

        // Call twice to clear any dead key state
        ToUnicodeEx(vk, scanCode, keyState, sb, sb.Capacity, 0, hkl);
        var result = ToUnicodeEx(vk, scanCode, keyState, sb, sb.Capacity, 0, hkl);

        if (result == 1 && !char.IsControl(sb[0]))
        {
            return sb.ToString().ToUpperInvariant();
        }

        var key = KeyInterop.KeyFromVirtualKey((int)vk);
        return key switch
        {
            Key.Space => "SPC",
            Key.Return => "ENT",
            Key.Back => "BS",
            Key.Tab => "TAB",
            Key.LeftShift => "LSH",
            Key.RightShift => "RSH",
            Key.LeftCtrl => "LCT",
            Key.RightCtrl => "RCT",
            Key.LeftAlt => "LAL",
            Key.RightAlt => "RAL",
            Key.Escape => "ESC",
            Key.CapsLock => "CAP",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.Left => "←",
            Key.Right => "→",
            Key.Delete => "DEL",
            Key.Insert => "INS",
            Key.Home => "HOM",
            Key.End => "END",
            Key.PageUp => "PGU",
            Key.PageDown => "PGD",
            >= Key.F1 and <= Key.F12 => key.ToString(),
            _ => key.ToString()
        };
    }

    // ── SendInput methods ────────────────────────────────────────────

    public static void SendKeyDown(ushort scanCode)
    {
        var input = new Input
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KeyboardInput
                {
                    wScan = scanCode,
                    dwFlags = KeyeventfScancode,
                    wVk = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        var result = SendInput(1, [input], InputSize);
        if (result != 1)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"SendInput failed for scan code {scanCode}. Win32 error: {error}. Input.Size={InputSize}");
        }
    }

    public static void SendKeyUp(ushort scanCode)
    {
        var input = new Input
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KeyboardInput
                {
                    wScan = scanCode,
                    dwFlags = KeyeventfScancode | KeyeventfKeyup,
                    wVk = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        var result = SendInput(1, [input], InputSize);

        if (result == 1) return;

        var error = Marshal.GetLastWin32Error();
        throw new InvalidOperationException(
            $"SendInput failed for scan code {scanCode}. Win32 error: {error}. Input.Size={InputSize}");
    }
}