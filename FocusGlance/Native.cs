using System.Runtime.InteropServices;

namespace FocusGlance;

static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int LWA_ALPHA = 0x2;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int Left, Right, Top, Bottom; }

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr h);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    public static bool IsForeground(IntPtr h) => GetForegroundWindow() == h;
    public static IntPtr Foreground() => GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int max);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
    public static uint WindowProcessId(IntPtr h) { GetWindowThreadProcessId(h, out uint pid); return pid; }

    /// <summary>SetForegroundWindow that also works when another app currently owns the foreground.</summary>
    public static void ForceForeground(IntPtr h)
    {
        var fg = GetForegroundWindow();
        if (fg == h) return;
        uint me = GetCurrentThreadId(), other = GetWindowThreadProcessId(fg, out _);
        bool attached = other != 0 && other != me && AttachThreadInput(me, other, true);
        try
        {
            BringWindowToTop(h);
            SetForegroundWindow(h);
            SetFocus(h);
        }
        finally { if (attached) AttachThreadInput(me, other, false); }
    }

    public static void SetExStyle(IntPtr h, int add, int remove)
    {
        long cur = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
        long next = (cur | (uint)add) & ~(long)(uint)remove;
        if (next != cur) SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(next));
    }
}

// ---- Core Audio (system volume) ----
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumeratorCom { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr p);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr p);
    [PreserveSig] int GetChannelCount(out uint c);
    [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid ctx);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
    [PreserveSig] int GetMasterVolumeLevel(out float level);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint ch, float level, ref Guid ctx);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
    [PreserveSig] int GetChannelVolumeLevel(uint ch, out float level);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

sealed class VolumeWatcher
{
    IAudioEndpointVolume? ep;
    DateTime opened = DateTime.MinValue;

    public (int level, bool muted)? Read()
    {
        try
        {
            if (ep == null || (DateTime.UtcNow - opened).TotalSeconds > 4)
            {
                if (ep != null) { try { Marshal.ReleaseComObject(ep); } catch { } }
                ep = Open();
                opened = DateTime.UtcNow;
            }
            if (ep == null) return null;
            if (ep.GetMasterVolumeLevelScalar(out float v) != 0) { ep = null; return null; }
            ep.GetMute(out bool m);
            return ((int)Math.Round(v * 100), m);
        }
        catch { ep = null; return null; }
    }

    static IAudioEndpointVolume? Open()
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        try
        {
            if (en.GetDefaultAudioEndpoint(0 /*eRender*/, 1 /*eMultimedia*/, out var dev) != 0 || dev == null) return null;
            var iid = typeof(IAudioEndpointVolume).GUID;
            if (dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out var o) != 0) return null;
            Marshal.ReleaseComObject(dev);
            return (IAudioEndpointVolume)o;
        }
        finally { Marshal.ReleaseComObject(en); }
    }
}
