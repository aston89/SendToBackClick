using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class SendToBackClick
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTMINBUTTON = 8;
    private const uint GA_ROOT = 2;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    // DWM attribute: bounds of the caption-button area, relative to the window.
    private const int DWMWA_CAPTION_BUTTON_BOUNDS = 5;

    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private static readonly LowLevelMouseProc HookProc = HookCallback;
    private static IntPtr _hook;
    private static bool _swallowRightUp;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    private static IntPtr MakeLParam(int x, int y)
    {
        long value = ((long)(short)y << 16) | (ushort)(short)x;
        return new IntPtr(value);
    }

    private static bool IsMinimizeButton(IntPtr hwnd, POINT screenPoint)
    {
        // First choice: ask the window itself. This is exact for normal Win32 title bars.
        IntPtr hit = SendMessage(
            hwnd,
            WM_NCHITTEST,
            IntPtr.Zero,
            MakeLParam(screenPoint.X, screenPoint.Y));

        if (hit.ToInt32() == HTMINBUTTON)
            return true;

        // Fallback for applications using a custom title bar where WM_NCHITTEST
        // doesn't report HTMINBUTTON even though the standard caption buttons are visible.
        RECT caption;
        if (DwmGetWindowAttribute(
                hwnd,
                DWMWA_CAPTION_BUTTON_BOUNDS,
                out caption,
                Marshal.SizeOf(typeof(RECT))) != 0)
            return false;

        if (caption.Right <= caption.Left || caption.Bottom <= caption.Top)
            return false;

        RECT windowRect;
        if (!GetWindowRect(hwnd, out windowRect))
            return false;

        int captionLeft = windowRect.Left + caption.Left;
        int captionTop = windowRect.Top + caption.Top;
        int captionRight = windowRect.Left + caption.Right;
        int captionBottom = windowRect.Top + caption.Bottom;

        if (screenPoint.X < captionLeft || screenPoint.X >= captionRight ||
            screenPoint.Y < captionTop || screenPoint.Y >= captionBottom)
            return false;

        // On a normal LTR Windows title bar the three buttons are:
        // minimize, maximize/restore, close. DWM gives us their total bounds.
        int third = (captionRight - captionLeft) / 3;
        if (third <= 0)
            return false;

        int minimizeLeft = captionRight - third * 3;
        int minimizeRight = minimizeLeft + third;

        return screenPoint.X >= minimizeLeft && screenPoint.X < minimizeRight;
    }

    private static void SendToBack(IntPtr hwnd)
    {
        SetWindowPos(
            hwnd,
            HWND_BOTTOM,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                int message = unchecked((int)wParam.ToInt64());

                if (message == WM_RBUTTONUP && _swallowRightUp)
                {
                    _swallowRightUp = false;
                    return new IntPtr(1);
                }

                if (message == WM_RBUTTONDOWN)
                {
                    MSLLHOOKSTRUCT data = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                        lParam, typeof(MSLLHOOKSTRUCT));

                    IntPtr hwnd = WindowFromPoint(data.pt);
                    if (hwnd != IntPtr.Zero)
                        hwnd = GetAncestor(hwnd, GA_ROOT);

                    if (hwnd != IntPtr.Zero && IsMinimizeButton(hwnd, data.pt))
                    {
                        SendToBack(hwnd);
                        _swallowRightUp = true;
                        return new IntPtr(1);
                    }
                }
            }
        }
        catch
        {
            // Never allow an exception to escape from a low-level hook callback.
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    [STAThread]
    private static void Main()
    {
        using (Mutex mutex = new Mutex(false, "Local\\Zero.SendToBackClick.v2"))
        {
            if (!mutex.WaitOne(0, false))
                return;

            // WH_MOUSE_LL is delivered back to the thread that installed it,
            // so this thread must keep a Windows message loop alive.
            _hook = SetWindowsHookEx(
                WH_MOUSE_LL,
                HookProc,
                GetModuleHandle(null),
                0);

            if (_hook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                MessageBox.Show(
                    "Impossibile installare il mouse hook.\n\nErrore Win32: " + error,
                    "SendToBackClick",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Application.Run();
            UnhookWindowsHookEx(_hook);
        }
    }
}
