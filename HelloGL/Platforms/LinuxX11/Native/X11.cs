using System.Reflection;
using System.Runtime.InteropServices;

namespace HelloGL.Platforms.LinuxX11.Native;

internal static class X11
{
    public const long ExposureMask = 1L << 15;
    public const long KeyPressMask = 1L << 0;
    public const long KeyReleaseMask = 1L << 1;
    public const long StructureNotifyMask = 1L << 17;

    public const long CWColormap = 1 << 13;
    public const long CWEventMask = 1 << 11;

    public const int ClientMessage = 33;
    public const int ConfigureNotify = 22;
    public const int KeyPress = 2;
    public const int KeyRelease = 3;



    [StructLayout(LayoutKind.Sequential)]
    public struct XVisualInfo
    {
        public nint visual;
        public nint visualid;
        public int screen;
        public int depth;
        public int c_class;
        public long red_mask;
        public long green_mask;
        public long blue_mask;
        public int colormap_size;
        public int bits_per_rgb;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XSetWindowAttributes
    {
        public nint background_pixmap;
        public long background_pixel;
        public long border_pixmap;
        public long border_pixel;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public long backing_planes;
        public long backing_pixel;
        public bool save_under;
        public long event_mask;
        public long do_not_propagate_mask;
        public bool override_redirect;
        public nint colormap;
        public nint cursor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
    {
        public nint x, y; public int width, height; public int border_width; public int depth;
        public nint visual; public nint root; public int c_class;
        public int bit_gravity, win_gravity; public int backing_store; public long backing_planes; public long backing_pixel;
        public bool save_under; public nint colormap; public bool map_installed; public int map_state;
        public long all_event_masks; public long your_event_mask; public long do_not_propagate_mask; public bool override_redirect;
        public nint screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XEvent
    {
        public int type;
        // Padding auf Maxgröße ist hier weggelassen – wir verwenden nur 'type'
        public long pad1, pad2, pad3, pad4, pad5, pad6, pad7, pad8, pad9, pad10, pad11, pad12, pad13, pad14, pad15, pad16, pad17, pad18, pad19, pad20, pad21, pad22, pad23, pad24;
    }

    [DllImport("libX11.so.6")] public static extern nint XOpenDisplay(nint display);
    [DllImport("libX11.so.6")] public static extern int XDefaultScreen(nint display);
    [DllImport("libX11.so.6")] public static extern nint XRootWindow(nint display, int screen);
    [DllImport("libX11.so.6")] public static extern nint XCreateColormap(nint display, nint window, nint visual, int alloc);

    [DllImport("libX11.so.6")]
    public static extern nint XCreateWindow(
        nint display, nint parent, int x, int y, int width, int height, int border_width,
        int depth, int c_class, nint visual, nuint valuemask, ref XSetWindowAttributes attributes);

    [DllImport("libX11.so.6")] public static extern void XStoreName(nint display, nint window, string window_name);
    [DllImport("libX11.so.6")] public static extern void XMapWindow(nint display, nint window);
    [DllImport("libX11.so.6")] public static extern void XDestroyWindow(nint display, nint window);
    [DllImport("libX11.so.6")] public static extern void XCloseDisplay(nint display);

    [DllImport("libX11.so.6")] public static extern int XPending(nint display);
    [DllImport("libX11.so.6")] public static extern void XNextEvent(nint display, out XEvent xevent);

    [DllImport("libX11.so.6")] public static extern int XGetWindowAttributes(nint display, nint w, out XWindowAttributes attr);

    [DllImport("libX11.so.6")] public static extern int XSync(nint display, bool discard);
}
