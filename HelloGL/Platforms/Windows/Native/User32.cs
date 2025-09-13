using System.Runtime.InteropServices;

namespace HelloGL.Platforms.Windows.Native;

//| Win32-Typ                                           | Bedeutung                                                 | C#-Mapping           |
//| --------------------------------------------------- | --------------------------------------------------------- | -------------------- |
//| `HWND`, `HICON`, `HCURSOR`, `HINSTANCE`, `HBRUSH` … | * Handles*, also Zeiger auf interne Strukturen            | `IntPtr` bzw. `nint` |
//| `LRESULT` / `LPARAM`                                | „long“ Zeigergröße(4 Byte auf 32-bit, 8 Byte auf 64-bit)  | `nint`               |
//| `WPARAM`                                            | „unsigned long“ Zeigergröße                               | `nuint`              |
//| `UINT`                                              | Immer 32-Bit unsigned                                     | `uint`               |
//| `DWORD`                                             | Immer 32-Bit unsigned                                     | `uint`               |
//| `POINT`                                             | Zwei `LONG` (immer 32-Bit signed)                         | `int`/`int`          |


internal static class User32
{
    public delegate nint WndProcDelegate(nint hWnd, uint msg, nuint wParam, nint lParam);

    public const int CW_USEDEFAULT = unchecked((int)0x80000000);

    // Window styles
    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const uint WS_VISIBLE = 0x10000000;

    // Window messages
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SIZE = 0x0005;
    public const uint WM_QUIT = 0x0012;

    // WM_SIZE wParam values
    public const uint SIZE_RESTORED = 0;
    public const uint SIZE_MINIMIZED = 1;
    public const uint SIZE_MAXIMIZED = 2;
    public const uint SIZE_MAXSHOW = 3;
    public const uint SIZE_MAXHIDE = 4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
    public static extern nint CreateWindowExW(uint exStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")] public static extern nint DefWindowProcW(nint hWnd, uint msg, nuint wParam, nint lParam);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool UpdateWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern bool PeekMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] public static extern nint DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] public static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")] public static extern nint GetDC(nint hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(nint hWnd, nint hdc);
    [DllImport("user32.dll")] public static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    public static void GetClientSize(nint hwnd, out int w, out int h)
    {
        if (!GetClientRect(hwnd, out var r))
            throw new Exception("GetClientRect failed.");

        w = r.right - r.left;
        h = r.bottom - r.top;
    }
}
