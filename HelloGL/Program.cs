using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HelloGL;


public record PlatformOptions();
public record WindowOptions(int Width, int Height, string Title, int SwapInterval = 1); // 0=off, 1=vsync


// vsync
// GLX.glXSwapIntervalEXT(_display, _glxWindow, 0); // VSync AUS (nur zum Debug)




public interface IPlatform : IDisposable
{
    IWindow CreateWindow(WindowOptions options);
}

public interface IWindow : IDisposable
{
    GL Gl { get; }

    (int, int) Size { get; }



    bool ProcessEvents();

    void SwapBuffers();
}

public static class PlatformFactory
{
    public static IPlatform CreatePlatform(PlatformOptions options)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatform(options);
        }
        else if (OperatingSystem.IsLinux())
        {
            var xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            if (string.Equals(xdgSessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            {
                return new LinuxWaylandPlatform(options);
            }
            else
            {
                return new LinuxXorgPlatform(options);
            }
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported OS platform.");
        }
    }
}

public unsafe class WindowsPlatform : IPlatform
{
    public WindowsPlatform(PlatformOptions options)
    {
    }

    public IWindow CreateWindow(WindowOptions options)
    {
        // 1) Win32 window + DC
        var (windowHandle, deviceContext) = Win32.CreateWindow(options.Width, options.Height, options.Title);
        //var win = Win32.CreateWindow(1280, 720, "OpenGL 3.3 Triangle (C# WGL)");

        // 2) Choose and set a pixel format (legacy path works fine)
        //Win32.SetupPixelFormat(win.Hdc);
        Win32.SetupPixelFormat(deviceContext);

        // 3) Dummy legacy context just to load WGL extensions
        //IntPtr dummyRC = Wgl.wglCreateContext(win.Hdc);
        IntPtr dummyRC = Wgl.wglCreateContext(deviceContext);
        if (dummyRC == IntPtr.Zero)
            throw new Exception("Failed to create dummy WGL context.");

        //if (!Wgl.wglMakeCurrent(win.Hdc, dummyRC))
        if (!Wgl.wglMakeCurrent(deviceContext, dummyRC))
            throw new Exception("Failed to make current dummy WGL context.");

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
                realRC = wglCreateContextAttribsARB(deviceContext, IntPtr.Zero, p);
            }
        }
        if (realRC == IntPtr.Zero)
            throw new Exception("Failed to create OpenGL 3.3 Core context.");

        // Switch to real context and delete dummy
        Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        Wgl.wglDeleteContext(dummyRC);
        if (!Wgl.wglMakeCurrent(deviceContext, realRC))
            throw new Exception("Failed to make current real GL context.");

        // 5) Load GL entry points
        Gpu.InitPlatformGL();

        var gl = new GL();
        gl.Load(Wgl.GetProcAddressWithFallback);

        Win32.GetClientSize(windowHandle, out int cw, out int ch);
        Console.WriteLine($"Setting initial viewport: {cw} {ch}");
        gl.Viewport(0, 0, cw, ch);

        //return new Window(realRC, win.Hdc);
        return new Window(windowHandle, deviceContext, realRC, gl);
    }

    public void Dispose()
    {
    }

    private class Window : IWindow
    {
        private readonly nint _windowHandle;
        private readonly nint _deviceContext;
        private readonly nint _openGlContext;

        public GL Gl { get; }

        public (int, int) Size => throw new NotImplementedException();

        public Window(IntPtr windowHandle, IntPtr deviceContext, IntPtr openGlContext, GL gl)
        {
            _windowHandle = windowHandle;
            _deviceContext = deviceContext;
            _openGlContext = openGlContext;
            Gl = gl;
        }

        public bool ProcessEvents()
        {
            var running = true;

            while (Win32.PeekMessage(out var msg))
            {
                if (msg.message == Win32.User32.WM_QUIT)
                {
                    running = false;
                }
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }

            if (Win32.TryDequeueResize(out int w, out int h))
            {
                Console.WriteLine($"Resizing viewport to: {w} {h}");
                Gl.Viewport(0, 0, Math.Max(1, w), Math.Max(1, h));
            }

            return running;
        }

        public void SwapBuffers()
        {
            Win32.SwapBuffers(_deviceContext);
        }

        public void Dispose()
        {
            Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Wgl.wglDeleteContext(_openGlContext);
            Win32.DestroyWindow(_windowHandle);
        }
    }
}

public unsafe class LinuxXorgPlatform : IPlatform
{
    public LinuxXorgPlatform(PlatformOptions options)
    {
    }

    public IWindow CreateWindow(WindowOptions options)
    {
        // 1) X11 Display öffnen
        var _display = X11.XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
            throw new Exception("XOpenDisplay failed. Läuft ein X-Server/XWayland?");

        int screen = X11.XDefaultScreen(_display);
        IntPtr root = X11.XRootWindow(_display, screen);

        // 2) GLX FBConfig wählen (Core 3.3 Ziel)
        int[] visualAttribs = new int[]
        {
            GLX.GLX_X_RENDERABLE, 1,
            GLX.GLX_DRAWABLE_TYPE, GLX.GLX_WINDOW_BIT,
            GLX.GLX_RENDER_TYPE,   GLX.GLX_RGBA_BIT,
            GLX.GLX_X_VISUAL_TYPE, GLX.GLX_TRUE_COLOR,
            GLX.GLX_RED_SIZE,   8,
            GLX.GLX_GREEN_SIZE, 8,
            GLX.GLX_BLUE_SIZE,  8,
            GLX.GLX_ALPHA_SIZE, 8,
            GLX.GLX_DEPTH_SIZE, 24,
            GLX.GLX_STENCIL_SIZE, 8,
            GLX.GLX_DOUBLEBUFFER, 1,
            0
        };

        int nelements;
        IntPtr fbConfigs = GLX.glXChooseFBConfig(_display, screen, visualAttribs, out nelements);
        if (fbConfigs == IntPtr.Zero || nelements == 0)
            throw new Exception("glXChooseFBConfig failed.");

        // Nimm das erste FBConfig
        IntPtr fbConfig = Marshal.ReadIntPtr(fbConfigs);

        // 3) VisualInfo besorgen
        IntPtr visInfoPtr = GLX.glXGetVisualFromFBConfig(_display, fbConfig);
        if (visInfoPtr == IntPtr.Zero)
            throw new Exception("glXGetVisualFromFBConfig failed.");
        var vis = Marshal.PtrToStructure<X11.XVisualInfo>(visInfoPtr);

        // 4) Colormap & Fenster erzeugen
        IntPtr cmap = X11.XCreateColormap(_display, root, vis.visual, 0 /*AllocNone*/);
        var swa = new X11.XSetWindowAttributes
        {
            colormap = cmap,
            event_mask = X11.ExposureMask | X11.KeyPressMask | X11.StructureNotifyMask
        };

        var _window = X11.XCreateWindow(
            _display, root,
            0, 0, options.Width, options.Height, 0,
            vis.depth, 1 /*InputOutput*/, vis.visual,
            (nuint)(X11.CWColormap | X11.CWEventMask),
            ref swa);
        if (_window == IntPtr.Zero)
            throw new Exception("XCreateWindow failed.");

        X11.XStoreName(_display, _window, options.Title);
        X11.XMapWindow(_display, _window);

        // damit die Map-Request auch wirklich verarbeitet ist:
        X11.XSync(_display, false);

        // GLXWindow aus *demselben* FBConfig erzeugen
        IntPtr _glxWindow = GLX.glXCreateWindow(_display, fbConfig, _window, IntPtr.Zero);
        if (_glxWindow == IntPtr.Zero) throw new Exception("glXCreateWindow failed");

        // 5) GLX 1.3+ prüfen und glXCreateContextAttribsARB laden
        var pCreateCtx = GLX.glXGetProcAddress("glXCreateContextAttribsARB");
        if (pCreateCtx == IntPtr.Zero)
            throw new Exception("glXCreateContextAttribsARB not available. Treiber/GLX zu alt?");
        var glXCreateContextAttribsARB = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, int, int*, IntPtr>)pCreateCtx;

        int[] ctxAttribs = new int[]
        {
            GLX.GLX_CONTEXT_MAJOR_VERSION_ARB, 3,
            GLX.GLX_CONTEXT_MINOR_VERSION_ARB, 3,
            GLX.GLX_CONTEXT_PROFILE_MASK_ARB,  GLX.GLX_CONTEXT_CORE_PROFILE_BIT_ARB,
            0
        };

        IntPtr _glxContext;
        unsafe
        {
            fixed (int* p = ctxAttribs)
            {
                _glxContext = glXCreateContextAttribsARB(_display, fbConfig, IntPtr.Zero, 1, p);
            }
        }
        if (_glxContext == IntPtr.Zero)
            throw new Exception("Failed to create GL 3.3 Core context.");

        //if (!GLX.glXMakeCurrent(_display, _window, _glxContext))
        //    throw new Exception("glXMakeCurrent failed.");
        //if (!GLX.glXMakeCurrent(_display, _glxWindow, _glxContext))
        //    throw new Exception("glXMakeCurrent failed.");

        if (!GLX.glXMakeContextCurrent(_display, _glxWindow, _glxWindow, _glxContext))
            throw new Exception("glXMakeContextCurrent failed");


        // 6) GL-Funktionen laden
        Gpu.InitPlatformGL();
        var gl = new GL();
        gl.Load(GLX.GetProcAddressWithFallback);

        GLX.Load();

        // 0 = VSync aus
        // 1 = VSync an
        GLX.glXSwapIntervalEXT(_display, _glxWindow, options.SwapInterval);

        Console.WriteLine($"Setting initial viewport: {options.Width} {options.Height}");
        gl.Viewport(0, 0, options.Width, options.Height);

        return new Window(_display, _window, _glxWindow, _glxContext, gl);
    }

    public void Dispose()
    {
    }

    private class Window : IWindow
    {
        private readonly nint _display;
        private readonly nint _x11Window;
        private readonly nint _glxWindow;
        private readonly nint _openGlContext;

        public GL Gl { get; }

        public (int, int) Size => throw new NotImplementedException();

        public Window(IntPtr display, IntPtr x11Window, IntPtr glxWindow, IntPtr openGlContext, GL gl)
        {
            _display = display;
            _x11Window = x11Window;
            _glxWindow = glxWindow;
            _openGlContext = openGlContext;
            Gl = gl;
        }

        public bool ProcessEvents()
        {
            var running = true;

            while (X11.XPending(_display) > 0)
            {
                X11.XNextEvent(_display, out var ev);

                switch (ev.type)
                {
                    case X11.ClientMessage:
                        running = false;
                        break;

                    case X11.ConfigureNotify:
                        Console.WriteLine($"ConfigureNotify");

                        GLX.glXQueryDrawable(_display, _glxWindow, GLX.GLX_WIDTH, out uint gw);
                        GLX.glXQueryDrawable(_display, _glxWindow, GLX.GLX_HEIGHT, out uint gh);
                        Console.WriteLine($"GLX drawable size: {gw} x {gh}");

                        //GLX.GetWindowSize(_display, _window, out int w, out int h);
                        //gl.Viewport(0, 0, Math.Max(1, w), Math.Max(1, h));

                        Gl.Viewport(0, 0, Math.Max(1, (int)gw), Math.Max(1, (int)gh));
                        break;
                }
            }

            return running;
        }

        public void SwapBuffers()
        {
            GLX.glXSwapBuffers(_display, _glxWindow);
        }

        public void Dispose()
        {
            GLX.glXMakeCurrent(_display, IntPtr.Zero, IntPtr.Zero);

            GLX.glXDestroyContext(_display, _openGlContext);
            GLX.glXDestroyWindow(_display, _glxWindow);
            X11.XDestroyWindow(_display, _x11Window);
            X11.XCloseDisplay(_display);
        }
    }
}

public class LinuxWaylandPlatform : IPlatform
{
    public LinuxWaylandPlatform(PlatformOptions options)
    {
    }

    public IWindow CreateWindow(WindowOptions options)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}

internal unsafe static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // init platform

        var platformOptions = new PlatformOptions();
        var windowOptions = new WindowOptions(1280, 720, "OpenGL 3.3 Triangle (C#)");

        using var platform = PlatformFactory.CreatePlatform(platformOptions);
        using var window = platform.CreateWindow(windowOptions);

        var gl = window.Gl;

        // init content

        uint vao = 0, vbo = 0;
        {
            gl.GenVertexArrays(1, &vao);
            gl.CheckError();

            gl.BindVertexArray(vao);
            gl.CheckError();

            gl.GenBuffers(1, &vbo);
            gl.CheckError();

            gl.BindBuffer(GL.GL_ARRAY_BUFFER, vbo);
            gl.CheckError();

            ReadOnlySpan<float> verts = [-0.6f, -0.5f, 0.6f, -0.5f, 0.0f, 0.6f,];
            fixed (float* pv = verts)
            {
                gl.BufferData(GL.GL_ARRAY_BUFFER, verts.Length * sizeof(float), (nint)pv, GL.GL_STATIC_DRAW);
                gl.CheckError();
            }

            gl.EnableVertexAttribArray(0);
            gl.CheckError();

            gl.VertexAttribPointer(0, 2, GL.GL_FLOAT, (byte)GL.GL_FALSE, 2 * sizeof(float), IntPtr.Zero);
            gl.CheckError();
        }

        string vs = "#version 330 core\n"
                  + "layout(location=0) in vec2 aPos;\n"
                  + "void main(){ gl_Position = vec4(aPos, 0.0, 1.0); }\n";

        string fs = "#version 330 core\n"
                  + "out vec4 FragColor;\n"
                  + "void main(){ FragColor = vec4(0.95, 0.4, 0.2, 1.0); }\n";

        uint prog = GLHelpers.CompileProgram(gl, vs, fs);
        gl.UseProgram(prog);
        gl.CheckError();

        // main loop

        var sw = Stopwatch.StartNew();
        var frameCount = 0;

        while (window.ProcessEvents())
        {
            gl.ClearColor(0.07f, 0.08f, 0.12f, 1f);
            gl.Clear(GL.GL_COLOR_BUFFER_BIT);
            gl.DrawArrays(GL.GL_TRIANGLES, 0, 3);

            window.SwapBuffers();

            frameCount++;
            if (sw.ElapsedMilliseconds >= 2500)
            {
                double fps = frameCount / sw.Elapsed.TotalSeconds;
                Console.WriteLine($"FPS: {fps:F2}");
                sw.Restart();
                frameCount = 0;
            }
        }

        // cleanup

        gl.DeleteProgram(prog);
        gl.DeleteBuffers(1, &vbo);
        gl.DeleteVertexArrays(1, &vao);
    }
}
