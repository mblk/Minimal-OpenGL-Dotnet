using System.Runtime.InteropServices;

namespace HelloGL;

internal unsafe static class GLX
{
    // --- Constants ---
    public const int GLX_X_RENDERABLE = 0x8012;
    public const int GLX_DRAWABLE_TYPE = 0x8010;
    public const int GLX_RENDER_TYPE = 0x8011;
    public const int GLX_X_VISUAL_TYPE = 0x22;
    public const int GLX_TRUE_COLOR = 0x8002;
    public const int GLX_WINDOW_BIT = 0x00000001;
    public const int GLX_RGBA_BIT = 0x00000001;
    public const int GLX_RED_SIZE = 8;
    public const int GLX_GREEN_SIZE = 9;
    public const int GLX_BLUE_SIZE = 10;
    public const int GLX_ALPHA_SIZE = 11;
    public const int GLX_DEPTH_SIZE = 12;
    public const int GLX_STENCIL_SIZE = 13;
    public const int GLX_DOUBLEBUFFER = 5;

    public const int GLX_CONTEXT_MAJOR_VERSION_ARB = 0x2091;
    public const int GLX_CONTEXT_MINOR_VERSION_ARB = 0x2092;
    public const int GLX_CONTEXT_PROFILE_MASK_ARB = 0x9126;
    public const int GLX_CONTEXT_CORE_PROFILE_BIT_ARB = 0x00000001;

    public const int GLX_WIDTH = 0x801D;
    public const int GLX_HEIGHT = 0x801E;

    [DllImport("libGL.so.1")] public static extern IntPtr glXChooseFBConfig(IntPtr display, int screen, int[] attrib_list, out int nelements);
    [DllImport("libGL.so.1")] public static extern IntPtr glXGetVisualFromFBConfig(IntPtr display, IntPtr fbconfig);
    [DllImport("libGL.so.1")] public static extern bool glXMakeCurrent(IntPtr display, IntPtr drawable, IntPtr ctx);
    [DllImport("libGL.so.1")] public static extern void glXSwapBuffers(IntPtr display, IntPtr drawable);
    [DllImport("libGL.so.1")] public static extern void glXDestroyContext(IntPtr display, IntPtr ctx);
    [DllImport("libGL.so.1")] public static extern IntPtr glXCreateWindow(IntPtr display, IntPtr fbconfig, IntPtr win, IntPtr attrib_list);
    [DllImport("libGL.so.1")] public static extern void glXDestroyWindow(IntPtr display, IntPtr glxwindow);

    [DllImport("libGL.so.1")] public static extern void glXQueryDrawable(IntPtr display, IntPtr drawable, int attribute, out uint value);

    [DllImport("libGL.so.1")] public static extern bool glXMakeContextCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr ctx);

    [DllImport("libGL.so.1")] public static extern void glXWaitGL();
    [DllImport("libGL.so.1")] public static extern void glXWaitX();

    // EXT-Variante (meist vorhanden)
    //[DllImport("libGL.so.1")] public static extern void glXSwapIntervalEXT(IntPtr dpy, IntPtr drawable, int interval);

    //[DllImport("libGL.so.1")] private static extern IntPtr glXGetProcAddressARB([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport("libGL.so.1")] public static extern IntPtr glXGetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);



    public static delegate* unmanaged<IntPtr, IntPtr, int, void> glXSwapIntervalEXT;



    public static void Load()
    {
        glXSwapIntervalEXT = (delegate* unmanaged<IntPtr, IntPtr, int, void>)GetProcAddressWithFallback("glXSwapIntervalEXT");
        if (glXSwapIntervalEXT == null)
        {
             Console.WriteLine("glXSwapIntervalEXT not found.");
        }
        else
        {
            Console.WriteLine("glXSwapIntervalEXT loaded.");
        }
    }



    //public static IntPtr glXGetProcAddress(string name) => glXGetProcAddressARB(name);

    public static IntPtr GetProcAddressWithFallback(string name)
    {
        var p = glXGetProcAddress(name);
        if (p != IntPtr.Zero) return p;
        return Gpu.GetExport(name);
    }

    public static void GetWindowSize(IntPtr display, IntPtr window, out int w, out int h)
    {
        X11.XWindowAttributes attr;
        X11.XGetWindowAttributes(display, window, out attr);
        w = attr.width; h = attr.height;
    }
}

internal static class X11
{
    public const long ExposureMask = 1L << 15;
    public const long KeyPressMask = 1L << 0;
    public const long StructureNotifyMask = 1L << 17;

    public const long CWColormap = 1 << 13;
    public const long CWEventMask = 1 << 11;

    public const int ClientMessage = 33;
    public const int ConfigureNotify = 22;


    [StructLayout(LayoutKind.Sequential)]
    public struct XVisualInfo
    {
        public IntPtr visual; public IntPtr visualid; public int screen; public int depth;
        public int c_class; public long red_mask; public long green_mask; public long blue_mask;
        public int colormap_size; public int bits_per_rgb;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XSetWindowAttributes
    {
        public IntPtr background_pixmap; public long background_pixel; public long border_pixmap; public long border_pixel;
        public int bit_gravity; public int win_gravity; public int backing_store; public long backing_planes; public long backing_pixel;
        public bool save_under; public long event_mask; public long do_not_propagate_mask; public bool override_redirect;
        public IntPtr colormap; public IntPtr cursor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
    {
        public IntPtr x, y; public int width, height; public int border_width; public int depth;
        public IntPtr visual; public IntPtr root; public int c_class;
        public int bit_gravity, win_gravity; public int backing_store; public long backing_planes; public long backing_pixel;
        public bool save_under; public IntPtr colormap; public bool map_installed; public int map_state;
        public long all_event_masks; public long your_event_mask; public long do_not_propagate_mask; public bool override_redirect;
        public IntPtr screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XEvent
    {
        public int type;
        // Padding auf Maxgröße ist hier weggelassen – wir verwenden nur 'type'
        public long pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22, pad23, pad24;
    }

    [DllImport("libX11.so.6")] public static extern IntPtr XOpenDisplay(IntPtr display);
    [DllImport("libX11.so.6")] public static extern int XDefaultScreen(IntPtr display);
    [DllImport("libX11.so.6")] public static extern IntPtr XRootWindow(IntPtr display, int screen);
    [DllImport("libX11.so.6")] public static extern IntPtr XCreateColormap(IntPtr display, IntPtr window, IntPtr visual, int alloc);

    [DllImport("libX11.so.6")]
    public static extern IntPtr XCreateWindow(
        IntPtr display, IntPtr parent, int x, int y, int width, int height, int border_width,
        int depth, int c_class, IntPtr visual, nuint valuemask, ref XSetWindowAttributes attributes);

    [DllImport("libX11.so.6")] public static extern void XStoreName(IntPtr display, IntPtr window, string window_name);
    [DllImport("libX11.so.6")] public static extern void XMapWindow(IntPtr display, IntPtr window);
    [DllImport("libX11.so.6")] public static extern void XDestroyWindow(IntPtr display, IntPtr window);
    [DllImport("libX11.so.6")] public static extern void XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")] public static extern int XPending(IntPtr display);
    [DllImport("libX11.so.6")] public static extern void XNextEvent(IntPtr display, out XEvent xevent);

    [DllImport("libX11.so.6")] public static extern int XGetWindowAttributes(IntPtr display, IntPtr w, out XWindowAttributes attr);

    [DllImport("libX11.so.6")] public static extern int XSync(IntPtr display, bool discard);
}
