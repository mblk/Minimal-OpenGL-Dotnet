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
        nint display = X11.XOpenDisplay(nint.Zero);
        if (display == nint.Zero)
            throw new Exception("XOpenDisplay failed");

        int screen = X11.XDefaultScreen(display);
        nint root = X11.XRootWindow(display, screen);

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

        //int nelements;
        var fbConfigs = GLX.glXChooseFBConfig(display, screen, visualAttribs, out int nelements);
        if (fbConfigs == nint.Zero || nelements == 0)
            throw new Exception("glXChooseFBConfig failed.");

        Console.WriteLine($"glXChooseFBConfig returned {nelements} matching FBConfigs.");

        // Nimm das erste FBConfig
        nint fbConfig = Marshal.ReadIntPtr(fbConfigs);

        // 3) VisualInfo besorgen
        var visInfoPtr = GLX.glXGetVisualFromFBConfig(display, fbConfig);
        if (visInfoPtr == nint.Zero)
            throw new Exception("glXGetVisualFromFBConfig failed.");
        var vis = Marshal.PtrToStructure<X11.XVisualInfo>(visInfoPtr);

        // 4) Colormap & Fenster erzeugen
        var cmap = X11.XCreateColormap(display, root, vis.visual, 0 /*AllocNone*/);
        
        var swa = new X11.XSetWindowAttributes
        {
            colormap = cmap,
            event_mask = X11.ExposureMask |
                         X11.KeyPressMask |
                         X11.KeyReleaseMask |
                         X11.StructureNotifyMask
        };

        var window = X11.XCreateWindow(
            display,
            root,
            0,
            0,
            options.Width,
            options.Height,
            0,
            vis.depth,
            1 /*InputOutput*/,
            vis.visual,
            (nuint)(X11.CWColormap | X11.CWEventMask),
            ref swa);
        if (window == nint.Zero)
            throw new Exception("XCreateWindow failed.");

        X11.XStoreName(display, window, options.Title);
        X11.XMapWindow(display, window);

        // damit die Map-Request auch wirklich verarbeitet ist:
        X11.XSync(display, false);

        // GLXWindow aus *demselben* FBConfig erzeugen
        var glxWindow = GLX.glXCreateWindow(display, fbConfig, window, nint.Zero);
        if (glxWindow == nint.Zero)
            throw new Exception("glXCreateWindow failed");

        // 5) GLX 1.3+ prüfen und glXCreateContextAttribsARB laden
        var pCreateCtx = GLX.glXGetProcAddress("glXCreateContextAttribsARB");
        if (pCreateCtx == nint.Zero)
            throw new Exception("glXCreateContextAttribsARB not available. Treiber/GLX zu alt?");
        
        var glXCreateContextAttribsARB = (delegate* unmanaged<nint, nint, nint, int, int*, nint>)pCreateCtx;

        int* ctxAttribs = stackalloc int[]
        {
            GLX.GLX_CONTEXT_MAJOR_VERSION_ARB, GL.MajorVersion,
            GLX.GLX_CONTEXT_MINOR_VERSION_ARB, GL.MinorVersion,
            GLX.GLX_CONTEXT_PROFILE_MASK_ARB,  GLX.GLX_CONTEXT_CORE_PROFILE_BIT_ARB,
            0
        };

        nint glxContext = glXCreateContextAttribsARB(display, fbConfig, nint.Zero, 1, ctxAttribs);        
        if (glxContext == IntPtr.Zero)
            throw new Exception($"Failed to create GL {GL.MajorVersion}.{GL.MinorVersion} core context.");

        //if (!GLX.glXMakeCurrent(_display, _window, _glxContext))
        //    throw new Exception("glXMakeCurrent failed.");
        //if (!GLX.glXMakeCurrent(_display, _glxWindow, _glxContext))
        //    throw new Exception("glXMakeCurrent failed.");

        if (!GLX.glXMakeContextCurrent(display, glxWindow, glxWindow, glxContext))
            throw new Exception("glXMakeContextCurrent failed");

        // 6) GL-Funktionen laden
        var glx = new GLX();
        var gl = new GL(glx.GetProcAddressWithFallback);

        // 0 = VSync aus
        // 1 = VSync an
        glx.SwapIntervalEXT(display, glxWindow, options.SwapInterval);

        Console.WriteLine($"Setting initial viewport: {options.Width} {options.Height}");
        gl.Viewport(0, 0, options.Width, options.Height);

        return new Window(display, window, glxWindow, glxContext, gl);
    }

    public void Dispose()
    {
    }

    private class Input : IInput
    {
        public required IKeyboard Keyboard { get; init; }
        public required IMouse Mouse { get; init; }
    }

    private class Keyboard : IKeyboard
    {
        public bool Get(Key key)
        {
            return false;
        }

        public bool WasPressed(Key key)
        {
            return false;
        }

        public bool WasReleased(Key key)
        {
            return false;
        }
    }

    private class Mouse : IMouse
    {
    }

    private class Window : IWindow
    {
        private readonly nint _display;
        private readonly nint _x11Window;
        private readonly nint _glxWindow;
        private readonly nint _openGlContext;

        private readonly Keyboard _keyboard = new();
        private readonly Mouse _mouse = new();

        public GL GL { get; }

        public (int, int) Size { get; private set; }

        public IInput Input { get; }

        public Window(nint display, nint x11Window, nint glxWindow, nint openGlContext, GL gl)
        {
            _display = display;
            _x11Window = x11Window;
            _glxWindow = glxWindow;
            _openGlContext = openGlContext;

            Input = new Input
            {
                Keyboard = _keyboard,
                Mouse = _mouse
            };

            GL = gl;
        }

        public bool ProcessEvents()
        {
            var running = true;

            while (X11.XPending(_display) > 0)
            {
                X11.XNextEvent(_display, out X11.XEvent ev);

                switch (ev.type)
                {
                    case X11.ClientMessage:
                    {
                        running = false;
                        break;                            
                    }

                    case X11.ConfigureNotify:
                    {
                        Console.WriteLine($"ConfigureNotify");

                        GLX.glXQueryDrawable(_display, _glxWindow, GLX.GLX_WIDTH, out var gw);
                        GLX.glXQueryDrawable(_display, _glxWindow, GLX.GLX_HEIGHT, out var gh);
                        Console.WriteLine($"GLX drawable size: {gw} x {gh}");

                        GL.Viewport(0, 0, Math.Max(1, (int)gw), Math.Max(1, (int)gh));

                        Size = ((int)gw, (int)gh);
                        break;
                    }
                    
                    case X11.KeyPress:
                    {
                        //var keyEvent = Marshal.PtrToStructure<X11.XKeyEvent>(ev.lparam);
                        //Console.WriteLine($"KeyPress: keycode={keyEvent.keycode}");
                        Console.WriteLine("KeyPress event");
                        break;
                    }
                    
                    case X11.KeyRelease:
                    {
                        Console.WriteLine("KeyRelease event");
                        break;
                    }
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
