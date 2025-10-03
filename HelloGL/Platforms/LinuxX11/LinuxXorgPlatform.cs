using HelloGL.Engine;
using HelloGL.Platforms.LinuxX11.Native;
using System.Runtime.InteropServices;

namespace HelloGL.Platforms.LinuxX11;

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

        var screen = X11.XDefaultScreen(_display);
        var root = X11.XRootWindow(_display, screen);

        // 2) GLX FBConfig wählen (Core 3.3 Ziel)
        var visualAttribs = new int[]
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
        var fbConfigs = GLX.glXChooseFBConfig(_display, screen, visualAttribs, out nelements);
        if (fbConfigs == IntPtr.Zero || nelements == 0)
            throw new Exception("glXChooseFBConfig failed.");

        // Nimm das erste FBConfig
        var fbConfig = Marshal.ReadIntPtr(fbConfigs);

        // 3) VisualInfo besorgen
        var visInfoPtr = GLX.glXGetVisualFromFBConfig(_display, fbConfig);
        if (visInfoPtr == IntPtr.Zero)
            throw new Exception("glXGetVisualFromFBConfig failed.");
        var vis = Marshal.PtrToStructure<X11.XVisualInfo>(visInfoPtr);

        // 4) Colormap & Fenster erzeugen
        var cmap = X11.XCreateColormap(_display, root, vis.visual, 0 /*AllocNone*/);
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
        var _glxWindow = GLX.glXCreateWindow(_display, fbConfig, _window, IntPtr.Zero);
        if (_glxWindow == IntPtr.Zero) throw new Exception("glXCreateWindow failed");

        // 5) GLX 1.3+ prüfen und glXCreateContextAttribsARB laden
        var pCreateCtx = GLX.glXGetProcAddress("glXCreateContextAttribsARB");
        if (pCreateCtx == IntPtr.Zero)
            throw new Exception("glXCreateContextAttribsARB not available. Treiber/GLX zu alt?");
        var glXCreateContextAttribsARB = (delegate* unmanaged<nint, nint, nint, int, int*, nint>)pCreateCtx;

        var ctxAttribs = new int[]
        {
            GLX.GLX_CONTEXT_MAJOR_VERSION_ARB, 3,
            GLX.GLX_CONTEXT_MINOR_VERSION_ARB, 3,
            GLX.GLX_CONTEXT_PROFILE_MASK_ARB,  GLX.GLX_CONTEXT_CORE_PROFILE_BIT_ARB,
            0
        };

        nint _glxContext;
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
        var gl = new GL(GLX.GetProcAddressWithFallback);
        GLX.LoadExtensions();

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

        public GL GL { get; }

        public (int, int) Size => throw new NotImplementedException();

        public IInput Input => throw new NotImplementedException();

        public Window(nint display, nint x11Window, nint glxWindow, nint openGlContext, GL gl)
        {
            _display = display;
            _x11Window = x11Window;
            _glxWindow = glxWindow;
            _openGlContext = openGlContext;
            GL = gl;
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

                        GLX.glXQueryDrawable(_display, _glxWindow, GLX.GLX_WIDTH, out var gw);
                        GLX.glXQueryDrawable(_display, _glxWindow, GLX.GLX_HEIGHT, out var gh);
                        Console.WriteLine($"GLX drawable size: {gw} x {gh}");

                        //GLX.GetWindowSize(_display, _window, out int w, out int h);
                        //gl.Viewport(0, 0, Math.Max(1, w), Math.Max(1, h));

                        GL.Viewport(0, 0, Math.Max(1, (int)gw), Math.Max(1, (int)gh));
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
