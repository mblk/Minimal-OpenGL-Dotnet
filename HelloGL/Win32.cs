using System.Runtime.InteropServices;

internal static class Win32
{
    internal static class User32
    {
        public const int CW_USEDEFAULT = unchecked((int)0x80000000);

        // Window styles
        public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        public const uint WS_VISIBLE = 0x10000000;

        // Window messages
        public const int WM_DESTROY = 0x0002;
        public const int WM_SIZE = 0x0005;
        public const int WM_QUIT = 0x0012;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEXW
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public UIntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
            public uint lPrivate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int left, top, right, bottom;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
        public static extern IntPtr CreateWindowExW(uint exStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")] public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool UpdateWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessageW(ref MSG lpMsg);
        [DllImport("user32.dll")] public static extern void PostQuitMessage(int nExitCode);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    }

    internal static class GDI32
    {
        // Pixel format descriptor flags
        public const uint PFD_DRAW_TO_WINDOW = 0x00000004;
        public const uint PFD_SUPPORT_OPENGL = 0x00000020;
        public const uint PFD_DOUBLEBUFFER = 0x00000001;
        public const byte PFD_TYPE_RGBA = 0;
        public const sbyte PFD_MAIN_PLANE = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize, nVersion;
            public uint dwFlags;
            public byte iPixelType, cColorBits, cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift, cAlphaBits, cAlphaShift;
            public byte cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
            public byte cDepthBits, cStencilBits, cAuxBuffers;
            public sbyte iLayerType, bReserved;
            public uint dwLayerMask, dwVisibleMask, dwDamageMask;
        }

        [DllImport("gdi32.dll")] public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] public static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] public static extern bool SwapBuffers(IntPtr hdc);
    }


    private static readonly object _resizeLock = new();
    private static bool _resizePending;
    private static int _clientW, _clientH;

    private static readonly WndProcDelegate _wndProc = new WndProcDelegate(WndProc);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);

    public static bool TryDequeueResize(out int w, out int h)
    {
        lock (_resizeLock)
        {
            if (_resizePending)
            {
                _resizePending = false;
                w = _clientW;
                h = _clientH;
                return true;
            }
        }

        w = h = 0;
        return false;
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam)
    {
        switch ((int)msg)
        {
            case User32.WM_DESTROY:
                User32.PostQuitMessage(0);
                return IntPtr.Zero;

            case User32.WM_SIZE:
                int w = lParam.ToInt32() & 0xFFFF;
                int h = (lParam.ToInt32() >> 16) & 0xFFFF;

                lock (_resizeLock)
                {
                    _clientW = w;
                    _clientH = h;
                    _resizePending = true;
                }
                break;
        }

        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public static (IntPtr Hwnd, IntPtr Hdc) CreateWindow(int width, int height, string title)
    {
        var cls = new User32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<User32.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            lpszClassName = "GLWndClass",
            hInstance = IntPtr.Zero,
        };

        if (User32.RegisterClassExW(ref cls) == 0)
            throw new Exception("RegisterClassExW failed.");

        IntPtr hwnd = User32.CreateWindowExW(0, cls.lpszClassName, title, User32.WS_OVERLAPPEDWINDOW | User32.WS_VISIBLE,
            User32.CW_USEDEFAULT, User32.CW_USEDEFAULT, width, height, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
            throw new Exception("CreateWindowExW failed.");

        User32.ShowWindow(hwnd, 1);
        User32.UpdateWindow(hwnd);

        IntPtr hdc = User32.GetDC(hwnd);
        if (hdc == IntPtr.Zero)
            throw new Exception("GetDC failed.");

        GetClientSize(hwnd, out _clientW, out _clientH);

        return (hwnd, hdc);
    }

    public static void DestroyWindow(IntPtr hwnd)
    {
        User32.ReleaseDC(hwnd, User32.GetDC(hwnd));
        User32.DestroyWindow(hwnd);
    }

    public static bool PeekMessage(out User32.MSG msg) => User32.PeekMessageW(out msg, IntPtr.Zero, 0, 0, 1);
    public static void TranslateMessage(ref User32.MSG msg) => User32.TranslateMessage(ref msg);
    public static void DispatchMessage(ref User32.MSG msg) => User32.DispatchMessageW(ref msg);

    public static void GetClientSize(IntPtr hwnd, out int w, out int h)
    {
        User32.RECT r;

        User32.GetClientRect(hwnd, out r);
        w = r.right - r.left;
        h = r.bottom - r.top;
    }

    public static void SetupPixelFormat(IntPtr hdc)
    {
        var pfd = new GDI32.PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<GDI32.PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = GDI32.PFD_DRAW_TO_WINDOW | GDI32.PFD_SUPPORT_OPENGL | GDI32.PFD_DOUBLEBUFFER,
            iPixelType = GDI32.PFD_TYPE_RGBA,
            cColorBits = 24,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = GDI32.PFD_MAIN_PLANE,
        };

        int pf = GDI32.ChoosePixelFormat(hdc, ref pfd);
        if (pf == 0)
            throw new Exception("ChoosePixelFormat failed.");

        if (!GDI32.SetPixelFormat(hdc, pf, ref pfd))
            throw new Exception("SetPixelFormat failed.");
    }

    public static void SwapBuffers(IntPtr hdc) => GDI32.SwapBuffers(hdc);
}
