using HelloGL.Platforms.Windows.Native;
using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace HelloGL.Platforms.Windows;

internal unsafe class WindowsPlatform : IPlatform
{
    private const string _windowClassName = "GLWndClass";

    private readonly FrozenDictionary<uint, string> _messagesToPrint = new Dictionary<uint, string>
    {
        [User32.WM_DESTROY] = "WM_DESTROY",
        [User32.WM_SIZE] = "WM_SIZE",
        [User32.WM_QUIT] = "WM_QUIT",
    }.ToFrozenDictionary();

    private readonly User32.WndProcDelegate _globalWindowProcedure;

    private readonly Dictionary<IntPtr, Window> _windows = [];

    public WindowsPlatform(PlatformOptions options)
    {
        // create new delegate instance and store it in a field, to prevent it from being GC'ed
        _globalWindowProcedure = GlobalWindowProcedure;

        RegisterWindowClass();
    }

    public void Dispose()
    {
        // ...
    }

    public IWindow CreateWindow(WindowOptions options)
    {
        nint windowHandle = CreateWindow(options.Width, options.Height, options.Title);
        nint deviceContext = CreateDeviceContext(windowHandle);
        SetPixelFormat(deviceContext);
        nint openGlContext = CreateOpenGlContext(deviceContext);

        //
        // load OpenGL entry points
        //

        WGL.LoadExtensions();
        var gl = new GL();
        gl.Load(WGL.GetProcAddress);

        //
        // 
        //

        SetSwapInterval(options.SwapInterval);

        User32.GetClientSize(windowHandle, out var cw, out var ch);
        Console.WriteLine($"Setting initial viewport: {cw} {ch}");
        gl.Viewport(0, 0, cw, ch);

        var window = new Window(windowHandle, deviceContext, openGlContext, gl);
        _windows.Add(windowHandle, window);
        return window;
    }

    private void RegisterWindowClass()
    {
        // register window class
        var windowClass = new User32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<User32.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_globalWindowProcedure),
            lpszClassName = _windowClassName,
            hInstance = IntPtr.Zero,
        };

        if (User32.RegisterClassExW(ref windowClass) == 0)
            throw new Exception("RegisterClassExW failed.");
    }

    private nint GlobalWindowProcedure(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        if (_messagesToPrint.TryGetValue(msg, out var name))
        {
            Console.WriteLine($"Global WndProc: hWnd={hWnd:X4} msg={msg:X4} ({name}) wParam={wParam:X} lParam={lParam:X}");
        }
        else
        {
            //Console.WriteLine($"Global WndProc: hWnd={hWnd:X4} msg={msg:X4} wParam={wParam:X} lParam={lParam:X}");
        }

        if (_windows.TryGetValue(hWnd, out var window))
        {
            var result = window.WindowProcedure(hWnd, msg, wParam, lParam);
            if (result.HasValue)
                return result.Value;
        }

        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static nint CreateWindow(int width, int height, string title)
    {
        var windowHandle = User32.CreateWindowExW(
            0,
            _windowClassName,
            title,
            User32.WS_OVERLAPPEDWINDOW | User32.WS_VISIBLE,
            User32.CW_USEDEFAULT,
            User32.CW_USEDEFAULT,
            width,
            height,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (windowHandle == IntPtr.Zero)
            throw new Exception("CreateWindowExW failed.");

        User32.ShowWindow(windowHandle, 1);
        User32.UpdateWindow(windowHandle);

        return windowHandle;
    }

    private static nint CreateDeviceContext(nint windowHandle)
    {
        var deviceContext = User32.GetDC(windowHandle);
        if (deviceContext == IntPtr.Zero)
            throw new Exception("GetDC failed.");
        return deviceContext;
    }

    private static void SetPixelFormat(nint deviceContext)
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

        var pf = GDI32.ChoosePixelFormat(deviceContext, ref pfd);
        if (pf == 0)
            throw new Exception("ChoosePixelFormat failed.");

        if (!GDI32.SetPixelFormat(deviceContext, pf, ref pfd))
            throw new Exception("SetPixelFormat failed.");
    }

    private static nint CreateOpenGlContext(nint deviceContext)
    {
        // Create dummy context to load wglCreateContextAttribsARB
        var dummyOpenGlContext = WGL.wglCreateContext(deviceContext);
        if (dummyOpenGlContext == IntPtr.Zero)
            throw new Exception("Failed to create dummy WGL context.");

        if (!WGL.wglMakeCurrent(deviceContext, dummyOpenGlContext))
            throw new Exception("Failed to make current dummy WGL context.");

        //var pCreateCtxAttribs = Wgl.GetProcAddress("wglCreateContextAttribsARB");
        var pCreateCtxAttribs = WGL.GetProcAddress("wglCreateContextAttribsARB");
        if (pCreateCtxAttribs == IntPtr.Zero)
            throw new Exception("wglCreateContextAttribsARB not available.");

        var wglCreateContextAttribsARB = (delegate* unmanaged<nint, nint, int*, nint>)pCreateCtxAttribs;

        // Create real core 3.3 context
        var attribs = new int[]
        {
            0x2091, 3,              // WGL_CONTEXT_MAJOR_VERSION_ARB
            0x2092, 3,              // WGL_CONTEXT_MINOR_VERSION_ARB
            0x2094, 0x00000001,     // WGL_CONTEXT_FLAGS_ARB = FORWARD_COMPATIBLE_BIT
            0x9126, 0x00000001,     // WGL_CONTEXT_PROFILE_MASK_ARB = CORE_PROFILE
            0
        };
        nint openGlContext;
        unsafe
        {
            fixed (int* p = attribs)
            {
                openGlContext = wglCreateContextAttribsARB(deviceContext, IntPtr.Zero, p);
            }
        }
        if (openGlContext == IntPtr.Zero)
            throw new Exception("Failed to create OpenGL 3.3 Core context.");

        // Delete dummy and switch to real context
        WGL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        WGL.wglDeleteContext(dummyOpenGlContext);
        if (!WGL.wglMakeCurrent(deviceContext, openGlContext))
            throw new Exception("Failed to activate OpenGL 3.3 Core context.");

        return openGlContext;
    }

    private static void SetSwapInterval(int interval)
    {
        WGL.SetVSync(interval);

        var current = WGL.GetVSync();
        if (current != interval)
        {
            Console.WriteLine($"Warning: Could not set swap interval to {interval}, current is {current}");
        }
        else
        {
            Console.WriteLine($"Swap interval set to {current}");
        }
    }




    private class Window : IWindow
    {
        private readonly nint _windowHandle;
        private readonly nint _deviceContext;
        private readonly nint _openGlContext;

        //private readonly Lock _resizeLock = new();
        private bool _resizePending;
        private int _clientW, _clientH;

        public GL Gl { get; }

        public (int, int) Size { get; private set; }

        public Window(nint windowHandle, nint deviceContext, nint openGlContext, GL gl)
        {
            _windowHandle = windowHandle;
            _deviceContext = deviceContext;
            _openGlContext = openGlContext;
            Gl = gl;
        }

        public void Dispose()
        {
            WGL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            WGL.wglDeleteContext(_openGlContext);

            User32.ReleaseDC(_windowHandle, _deviceContext);
            User32.DestroyWindow(_windowHandle);
        }

        public nint? WindowProcedure(nint hWnd, uint msg, nuint wParam, nint lParam)
        {
            //Console.WriteLine($"WindowProcedure {msg:X4}");

            switch (msg)
            {
                case User32.WM_DESTROY:
                {
                    User32.PostQuitMessage(0);
                    return IntPtr.Zero;
                }

                case User32.WM_SIZE:
                {
                    if (wParam == User32.SIZE_MINIMIZED)
                    {
                        // ignore (0,0) size
                        return IntPtr.Zero;
                    }

                    var w = lParam.ToInt32() & 0xFFFF;
                    var h = lParam.ToInt32() >> 16 & 0xFFFF;

                    //lock (_resizeLock)
                    {
                        _clientW = w;
                        _clientH = h;
                        _resizePending = true;
                    }

                    return IntPtr.Zero;
                }
            }

            return null; // not handled
        }

        private bool TryDequeueResize(out int w, out int h)
        {
            //lock (_resizeLock)
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

        public bool ProcessEvents()
        {
            var running = true;

            while (User32.PeekMessageW(out var msg, IntPtr.Zero, 0, 0, 1))
            {
                if (msg.message == User32.WM_QUIT)
                {
                    running = false;
                }
                User32.TranslateMessage(ref msg);
                User32.DispatchMessageW(ref msg);
            }

            if (TryDequeueResize(out var w, out var h))
            {
                Console.WriteLine($"Resize: {w} {h}");
                Size = (w, h);
                Gl.Viewport(0, 0, Math.Max(1, w), Math.Max(1, h));
            }

            return running;
        }

        public void SwapBuffers()
        {
            GDI32.SwapBuffers(_deviceContext);
        }
    }
}
