using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

internal static class Program
{
    [STAThread]
    private unsafe static void Main()
    {
        // 1) Win32 window + DC
        var win = Win32.CreateWindow(1280, 720, "OpenGL 3.3 Triangle (C# WGL)");

        // 2) Choose and set a pixel format (legacy path works fine)
        Win32.SetupPixelFormat(win.Hdc);

        // 3) Dummy legacy context just to load WGL extensions
        IntPtr dummyRC = Wgl.wglCreateContext(win.Hdc);
        if (dummyRC == IntPtr.Zero || !Wgl.wglMakeCurrent(win.Hdc, dummyRC))
            throw new Exception("Failed to create/make current dummy WGL context.");

        // Load wglCreateContextAttribsARB
        var pCreateCtxAttribs = Wgl.GetProcAddress("wglCreateContextAttribsARB");
        if (pCreateCtxAttribs == IntPtr.Zero)
            throw new Exception("wglCreateContextAttribsARB not available (need Vista+ driver-level support).");
        var wglCreateContextAttribsARB = (delegate* unmanaged<IntPtr, IntPtr, int*, IntPtr>)pCreateCtxAttribs;

        // 4) Create real core 3.3 context
        int[] attribs = new int[]
        {
            0x2091, 3,     // WGL_CONTEXT_MAJOR_VERSION_ARB
            0x2092, 3,     // WGL_CONTEXT_MINOR_VERSION_ARB
            0x2094, 0x00000001, // WGL_CONTEXT_FLAGS_ARB = FORWARD_COMPATIBLE_BIT
            0x9126, 0x00000001, // WGL_CONTEXT_PROFILE_MASK_ARB = CORE_PROFILE
            0
        };
        IntPtr realRC;
        unsafe
        {
            fixed (int* p = attribs)
            {
                realRC = wglCreateContextAttribsARB(win.Hdc, IntPtr.Zero, p);
            }
        }
        if (realRC == IntPtr.Zero)
            throw new Exception("Failed to create OpenGL 3.3 Core context.");

        // Switch to real context and delete dummy
        Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        Wgl.wglDeleteContext(dummyRC);
        if (!Wgl.wglMakeCurrent(win.Hdc, realRC))
            throw new Exception("Failed to make current real GL context.");

        // 5) Load GL entry points
        Gpu.InitPlatformGL();
        var gl = new GL();
        gl.Load(Wgl.GetProcAddressWithFallback);

        Console.WriteLine($"GL_VERSION: {gl.GetStringAnsi(GL.GL_VERSION)}");
        Console.WriteLine($"GL_VENDOR : {gl.GetStringAnsi(GL.GL_VENDOR)}");
        Console.WriteLine($"GL_RENDERER: {gl.GetStringAnsi(GL.GL_RENDERER)}");

        // 6) Create pipeline objects (VAO/VBO/Shader)
        uint vao = 0, vbo = 0;
        unsafe
        {
            gl.GenVertexArrays(1, &vao);
            gl.BindVertexArray(vao);
            gl.GenBuffers(1, &vbo);
            gl.BindBuffer(GL.GL_ARRAY_BUFFER, vbo);

            // simple triangle
            ReadOnlySpan<float> verts = new float[]
            {
                // pos(x,y)
                -0.6f, -0.5f,
                 0.6f, -0.5f,
                 0.0f,  0.6f,
            };
            fixed (float* pv = verts)
            {
                gl.BufferData(GL.GL_ARRAY_BUFFER, (nint)(verts.Length * sizeof(float)), (IntPtr)pv, GL.GL_STATIC_DRAW);
            }

            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, GL.GL_FLOAT, GL.GL_FALSE, 2 * sizeof(float), IntPtr.Zero);
        }

        // Vertex/Fragment shaders
        string vs = "#version 330 core\n"
                  + "layout(location=0) in vec2 aPos;\n"
                  + "void main(){ gl_Position = vec4(aPos, 0.0, 1.0); }\n";

        string fs = "#version 330 core\n"
                  + "out vec4 FragColor;\n"
                  + "void main(){ FragColor = vec4(0.95, 0.4, 0.2, 1.0); }\n";

        uint prog = GLHelpers.CompileProgram(gl, vs, fs);
        gl.UseProgram(prog);

        // Initial viewport
        Win32.GetClientSize(win.Hwnd, out int cw, out int ch);
        gl.Viewport(0, 0, (uint)cw, (uint)ch);

        // 7) Main loop
        bool running = true;
        while (running)
        {
            while (Win32.PeekMessage(out var msg))
            {
                if (msg.message == Win32.Imports.WM_QUIT)
                    running = false;
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }

            // Handle resize (updated by WndProc)
            if (Win32.TryDequeueResize(out int w, out int h))
            {
                gl.Viewport(0, 0, (uint) Math.Max(1, w), (uint) Math.Max(1, h));
            }

            // Render
            gl.ClearColor(0.07f, 0.08f, 0.12f, 1f);
            gl.Clear(GL.GL_COLOR_BUFFER_BIT);
            gl.DrawArrays(GL.GL_TRIANGLES, 0, 3);

            Win32.SwapBuffers(win.Hdc);
        }

        // Cleanup
        unsafe
        {
            gl.DeleteProgram(prog);
            gl.DeleteBuffers(1, &vbo);
            gl.DeleteVertexArrays(1, &vao);
        }
        Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        Wgl.wglDeleteContext(realRC);
        Win32.DestroyWindow(win.Hwnd);
    }
}

internal static class GLHelpers
{
    public static unsafe uint CompileProgram(GL gl, string vsSource, string fsSource)
    {
        uint vs = gl.CreateShader(GL.GL_VERTEX_SHADER);
        GLSetSource(gl, vs, vsSource);
        gl.CompileShader(vs);
        CheckShader(gl, vs, "Vertex");

        uint fs = gl.CreateShader(GL.GL_FRAGMENT_SHADER);
        GLSetSource(gl, fs, fsSource);
        gl.CompileShader(fs);
        CheckShader(gl, fs, "Fragment");

        uint p = gl.CreateProgram();
        gl.AttachShader(p, vs);
        gl.AttachShader(p, fs);
        gl.LinkProgram(p);
        CheckProgram(gl, p);

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return p;
    }

    private static unsafe void GLSetSource(GL gl, uint shader, string src)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(src);
        fixed (byte* p = bytes)
        {
            byte*[] arr = new byte*[1] { p };
            int len = bytes.Length;
            fixed (byte** parr = arr)
            {
                gl.ShaderSource(shader, 1, parr, &len);
            }
        }
    }

    private static unsafe void CheckShader(GL gl, uint shader, string label)
    {
        int status = 0;
        gl.GetShaderiv(shader, GL.GL_COMPILE_STATUS, &status);
        if (status == 0)
        {
            int len = 0;
            gl.GetShaderiv(shader, GL.GL_INFO_LOG_LENGTH, &len);
            var sb = new byte[Math.Max(len, 1024)];
            fixed (byte* p = sb)
            {
                gl.GetShaderInfoLog(shader, sb.Length, IntPtr.Zero, (IntPtr)p);
                throw new Exception($"{label} compile error: {Encoding.ASCII.GetString(sb)}");
            }
        }
    }

    private static unsafe void CheckProgram(GL gl, uint prog)
    {
        int status = 0;
        gl.GetProgramiv(prog, GL.GL_LINK_STATUS, &status);
        if (status == 0)
        {
            int len = 0;
            gl.GetProgramiv(prog, GL.GL_INFO_LOG_LENGTH, &len);
            var sb = new byte[Math.Max(len, 1024)];
            fixed (byte* p = sb)
            {
                gl.GetProgramInfoLog(prog, sb.Length, IntPtr.Zero, (IntPtr)p);
                throw new Exception($"Link error: {Encoding.ASCII.GetString(sb)}");
            }
        }
    }
}

internal unsafe class GL
{
    // Constants
    public const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    public const uint GL_TRIANGLES = 0x0004;
    public const uint GL_ARRAY_BUFFER = 0x8892;
    public const uint GL_STATIC_DRAW = 0x88E4;
    public const uint GL_FLOAT = 0x1406;
    public const uint GL_FALSE = 0;
    public const uint GL_TRUE = 1;
    public const uint GL_VERTEX_SHADER = 0x8B31;
    public const uint GL_FRAGMENT_SHADER = 0x8B30;
    public const uint GL_COMPILE_STATUS = 0x8B81;
    public const uint GL_LINK_STATUS = 0x8B82;
    public const uint GL_INFO_LOG_LENGTH = 0x8B84;
    public const uint GL_VENDOR = 0x1F00;
    public const uint GL_RENDERER = 0x1F01;
    public const uint GL_VERSION = 0x1F02;

    // Function pointers
    public delegate* unmanaged<float, float, float, float, void> ClearColor;
    public delegate* unmanaged<uint, void> Clear;
    public delegate* unmanaged<int, int, uint, uint, void> Viewport;
    public delegate* unmanaged<uint, IntPtr> GetString;
    public delegate* unmanaged<uint, uint> CreateShader;
    public delegate* unmanaged<uint, int, byte**, int*, void> ShaderSource;
    public delegate* unmanaged<uint, void> CompileShader;
    public delegate* unmanaged<uint, uint, int*, void> GetShaderiv;
    public delegate* unmanaged<uint, int, IntPtr, IntPtr, void> GetShaderInfoLog;
    public delegate* unmanaged<uint> CreateProgram;
    public delegate* unmanaged<uint, uint, void> AttachShader;
    public delegate* unmanaged<uint, void> LinkProgram;
    public delegate* unmanaged<uint, uint, int*, void> GetProgramiv;
    public delegate* unmanaged<uint, int, IntPtr, IntPtr, void> GetProgramInfoLog;
    public delegate* unmanaged<uint, void> UseProgram;
    public delegate* unmanaged<int, uint*, void> GenVertexArrays;
    public delegate* unmanaged<uint, void> BindVertexArray;
    public delegate* unmanaged<int, uint*, void> GenBuffers;
    public delegate* unmanaged<uint, uint, void> BindBuffer;
    public delegate* unmanaged<uint, nint, IntPtr, uint, void> BufferData;
    public delegate* unmanaged<uint, int, uint, uint, int, IntPtr, void> VertexAttribPointer;
    public delegate* unmanaged<uint, void> EnableVertexAttribArray;
    public delegate* unmanaged<uint, void> DisableVertexAttribArray;
    public delegate* unmanaged<uint, int, uint, void> DrawArrays;
    public delegate* unmanaged<uint, void> DeleteShader;
    public delegate* unmanaged<uint, void> DeleteProgram;
    public delegate* unmanaged<int, uint*, void> DeleteBuffers;
    public delegate* unmanaged<int, uint*, void> DeleteVertexArrays;

    public void Load(Func<string, IntPtr> getProc)
    {
        ClearColor = (delegate* unmanaged<float, float, float, float, void>)Load(getProc, "glClearColor");
        Clear = (delegate* unmanaged<uint, void>)Load(getProc, "glClear");
        Viewport = (delegate* unmanaged<int, int, uint, uint, void>)Load(getProc, "glViewport");
        GetString = (delegate* unmanaged<uint, IntPtr>)Load(getProc, "glGetString");

        CreateShader = (delegate* unmanaged<uint, uint>)Load(getProc, "glCreateShader");
        ShaderSource = (delegate* unmanaged<uint, int, byte**, int*, void>)Load(getProc, "glShaderSource");
        CompileShader = (delegate* unmanaged<uint, void>)Load(getProc, "glCompileShader");
        GetShaderiv = (delegate* unmanaged<uint, uint, int*, void>)Load(getProc, "glGetShaderiv");
        GetShaderInfoLog = (delegate* unmanaged<uint, int, IntPtr, IntPtr, void>)Load(getProc, "glGetShaderInfoLog");

        CreateProgram = (delegate* unmanaged<uint>)Load(getProc, "glCreateProgram");
        AttachShader = (delegate* unmanaged<uint, uint, void>)Load(getProc, "glAttachShader");
        LinkProgram = (delegate* unmanaged<uint, void>)Load(getProc, "glLinkProgram");
        GetProgramiv = (delegate* unmanaged<uint, uint, int*, void>)Load(getProc, "glGetProgramiv");
        GetProgramInfoLog = (delegate* unmanaged<uint, int, IntPtr, IntPtr, void>)Load(getProc, "glGetProgramInfoLog");
        UseProgram = (delegate* unmanaged<uint, void>)Load(getProc, "glUseProgram");

        GenVertexArrays = (delegate* unmanaged<int, uint*, void>)Load(getProc, "glGenVertexArrays");
        BindVertexArray = (delegate* unmanaged<uint, void>)Load(getProc, "glBindVertexArray");
        GenBuffers = (delegate* unmanaged<int, uint*, void>)Load(getProc, "glGenBuffers");
        BindBuffer = (delegate* unmanaged<uint, uint, void>)Load(getProc, "glBindBuffer");
        BufferData = (delegate* unmanaged<uint, nint, IntPtr, uint, void>)Load(getProc, "glBufferData");
        VertexAttribPointer = (delegate* unmanaged<uint, int, uint, uint, int, IntPtr, void>)Load(getProc, "glVertexAttribPointer");
        EnableVertexAttribArray = (delegate* unmanaged<uint, void>)Load(getProc, "glEnableVertexAttribArray");
        DisableVertexAttribArray = (delegate* unmanaged<uint, void>)Load(getProc, "glDisableVertexAttribArray");
        DrawArrays = (delegate* unmanaged<uint, int, uint, void>)Load(getProc, "glDrawArrays");

        DeleteShader = (delegate* unmanaged<uint, void>)Load(getProc, "glDeleteShader");
        DeleteProgram = (delegate* unmanaged<uint, void>)Load(getProc, "glDeleteProgram");
        DeleteBuffers = (delegate* unmanaged<int, uint*, void>)Load(getProc, "glDeleteBuffers");
        DeleteVertexArrays = (delegate* unmanaged<int, uint*, void>)Load(getProc, "glDeleteVertexArrays");
    }

    private static IntPtr Load(Func<string, IntPtr> gp, string name)
    {
        var p = gp(name);
        if (p == IntPtr.Zero)
            throw new InvalidOperationException($"GL function not found: {name}");
        return p;
    }

    public string GetStringAnsi(uint pname)
    {
        var ptr = GetString(pname);
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(ptr)!;
    }
}

internal static class Gpu
{
    private static IntPtr _libGL = IntPtr.Zero;

    public static void InitPlatformGL()
    {
        // opengl32.dll for fallback (core functions <= 1.1)
        NativeLibrary.TryLoad("opengl32.dll", out _libGL);
    }

    public static IntPtr GetExport(string name)
    {
        if (_libGL != IntPtr.Zero && NativeLibrary.TryGetExport(_libGL, name, out var p))
            return p;
        return IntPtr.Zero;
    }
}

internal static class Wgl
{
    [DllImport("opengl32.dll", ExactSpelling = true)] public static extern IntPtr wglCreateContext(IntPtr hdc);
    [DllImport("opengl32.dll", ExactSpelling = true)] public static extern bool wglDeleteContext(IntPtr hglrc);
    [DllImport("opengl32.dll", ExactSpelling = true)] public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
    [DllImport("opengl32.dll", EntryPoint = "wglGetProcAddress", CharSet = CharSet.Ansi)] private static extern IntPtr _wglGetProcAddress(string name);

    public static IntPtr GetProcAddress(string name)
    {
        var p = _wglGetProcAddress(name);
        // Filter out bogus values sometimes returned
        if (p == IntPtr.Zero || p == new IntPtr(1) || p == new IntPtr(2) || p == new IntPtr(3) || p == new IntPtr(-1))
            return IntPtr.Zero;
        return p;
    }

    public static IntPtr GetProcAddressWithFallback(string name)
    {
        var p = GetProcAddress(name);
        if (p != IntPtr.Zero) return p;
        // core symbols may be in opengl32.dll
        p = Gpu.GetExport(name);
        return p;
    }
}

internal static class Win32
{
    internal static class Imports
    {
        // Window + message loop helpers
        public const int CW_USEDEFAULT = unchecked((int)0x80000000);
        public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        public const uint WS_VISIBLE = 0x10000000;

        public const int WM_DESTROY = 0x0002;
        public const int WM_SIZE = 0x0005;
        public const int WM_QUIT = 0x0012;

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
        [DllImport("gdi32.dll")] public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] public static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] public static extern bool SwapBuffers(IntPtr hdc);


        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    }

    private static int _clientW, _clientH;
    private static readonly object _resizeLock = new();
    private static bool _resizePending;

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
            case Imports.WM_DESTROY:
                Imports.PostQuitMessage(0);
                return IntPtr.Zero;

            case Imports.WM_SIZE:
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

        return Imports.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public static (IntPtr Hwnd, IntPtr Hdc) CreateWindow(int width, int height, string title)
    {
        var cls = new Imports.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Imports.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            lpszClassName = "GLWndClass",
            hInstance = IntPtr.Zero,
        };

        if (Imports.RegisterClassExW(ref cls) == 0)
            throw new Exception("RegisterClassExW failed.");

        IntPtr hwnd = Imports.CreateWindowExW(0, cls.lpszClassName, title, Imports.WS_OVERLAPPEDWINDOW | Imports.WS_VISIBLE,
            Imports.CW_USEDEFAULT, Imports.CW_USEDEFAULT, width, height, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
            throw new Exception("CreateWindowExW failed.");

        Imports.ShowWindow(hwnd, 1);
        Imports.UpdateWindow(hwnd);

        IntPtr hdc = Imports.GetDC(hwnd);
        if (hdc == IntPtr.Zero)
            throw new Exception("GetDC failed.");

        GetClientSize(hwnd, out _clientW, out _clientH);

        return (hwnd, hdc);
    }

    public static void DestroyWindow(IntPtr hwnd)
    {
        Imports.ReleaseDC(hwnd, Imports.GetDC(hwnd));
        Imports.DestroyWindow(hwnd);
    }

    public static void GetClientSize(IntPtr hwnd, out int w, out int h)
    {
        Imports.RECT r;

        Imports.GetClientRect(hwnd, out r);
        w = r.right - r.left;
        h = r.bottom - r.top;
    }

    public static void SetupPixelFormat(IntPtr hdc)
    {
        var pfd = new Imports.PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<Imports.PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = Imports.PFD_DRAW_TO_WINDOW | Imports.PFD_SUPPORT_OPENGL | Imports.PFD_DOUBLEBUFFER,
            iPixelType = Imports.PFD_TYPE_RGBA,
            cColorBits = 24,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = Imports.PFD_MAIN_PLANE,
        };

        int pf = Imports.ChoosePixelFormat(hdc, ref pfd);
        if (pf == 0)
            throw new Exception("ChoosePixelFormat failed.");

        if (!Imports.SetPixelFormat(hdc, pf, ref pfd))
            throw new Exception("SetPixelFormat failed.");
    }

    public static bool PeekMessage(out Imports.MSG msg) => Imports.PeekMessageW(out msg, IntPtr.Zero, 0, 0, 1);
    public static void TranslateMessage(ref Imports.MSG msg) => Imports.TranslateMessage(ref msg);
    public static void DispatchMessage(ref Imports.MSG msg) => Imports.DispatchMessageW(ref msg);
    public static void SwapBuffers(IntPtr hdc) => Imports.SwapBuffers(hdc);
}
