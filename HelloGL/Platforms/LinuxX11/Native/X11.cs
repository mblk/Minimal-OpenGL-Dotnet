using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HelloGL.Platforms.LinuxX11.Native;

internal unsafe static class X11
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
        public nint x, y;
        public int width, height;
        public int border_width;
        public int depth;
        public nint visual;
        public nint root;
        public int c_class;
        public int bit_gravity, win_gravity;
        public int backing_store;
        public long backing_planes;
        public long backing_pixel;
        public bool save_under;
        public nint colormap;
        public bool map_installed;
        public int map_state;
        public long all_event_masks;
        public long your_event_mask;
        public long do_not_propagate_mask;
        public bool override_redirect;
        public nint screen;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct XEventAny
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(8)] public uint serial;
        [FieldOffset(16)] public int send_event; // bool
        [FieldOffset(24)] public nint display; // pointer
        [FieldOffset(32)] public int window; // id
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct XKeyEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(8)] public uint serial;
        [FieldOffset(16)] public int send_event; // bool
        [FieldOffset(24)] public nint display; // pointer
        [FieldOffset(32)] public int window; // id
        [FieldOffset(40)] public int root;
        [FieldOffset(48)] public int subwindow;
        [FieldOffset(56)] public int time;
        [FieldOffset(64)] public int x, y;
        [FieldOffset(72)] public int x_root, y_root;
        [FieldOffset(80)] public uint state;
        [FieldOffset(84)] public uint keycode;
        [FieldOffset(88)] public int same_screen; // bool 
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
    //[DllImport("libX11.so.6")] public static extern void XNextEvent(nint display, out XEvent xevent);

    [DllImport("libX11.so.6")] public static extern void XNextEvent(nint display, void* data);

    [DllImport("libX11.so.6")] public static extern int XGetWindowAttributes(nint display, nint w, out XWindowAttributes attr);

    [DllImport("libX11.so.6")] public static extern int XSync(nint display, bool discard);
}
