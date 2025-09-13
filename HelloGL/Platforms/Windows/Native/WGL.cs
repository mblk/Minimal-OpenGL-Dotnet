using System.Runtime.InteropServices;

namespace HelloGL.Platforms.Windows.Native;

internal unsafe static class WGL
{
    [DllImport("opengl32.dll", ExactSpelling = true)]
    public static extern nint wglCreateContext(nint hdc);

    [DllImport("opengl32.dll", ExactSpelling = true)]
    public static extern bool wglDeleteContext(nint hglrc);

    [DllImport("opengl32.dll", ExactSpelling = true)]
    public static extern bool wglMakeCurrent(nint hdc, nint hglrc);

    [DllImport("opengl32.dll", CharSet = CharSet.Ansi)]
    private static extern nint wglGetProcAddress(string name);

    private static IntPtr _libGL = IntPtr.Zero;

    private static delegate* unmanaged[Stdcall]<int, bool> wglSwapIntervalEXT;
    private static delegate* unmanaged[Stdcall]<int> wglGetSwapIntervalEXT;

    public static void LoadExtensions()
    {
        wglSwapIntervalEXT = (delegate* unmanaged[Stdcall]<int, bool>)GetProcAddress("wglSwapIntervalEXT");
        wglGetSwapIntervalEXT = (delegate* unmanaged[Stdcall]<int>)GetProcAddress("wglGetSwapIntervalEXT");
    }

    public static void SetVSync(int interval)
    {
        if (wglSwapIntervalEXT is null) return;

        wglSwapIntervalEXT(interval); // 1=an, 0=aus
    }

    public static int GetVSync()
    {
        if (wglGetSwapIntervalEXT is null) return 0;

        return wglGetSwapIntervalEXT();
    }

    public static nint GetProcAddress(string name)
    {
        nint p;

        // newer functions must be queried via wglGetProcAddress
        p = GetProcAddressFromWgl(name);
        if (p != IntPtr.Zero)
            return p;

        // older/core symbols may be in opengl32.dll
        p = GetProcAddressFromNativeLib(name);
        if (p != IntPtr.Zero)
            return p;

        Console.WriteLine($"GetProcAddress: {name} not found.");
        return IntPtr.Zero;
    }

    private static nint GetProcAddressFromWgl(string name)
    {
        var p = wglGetProcAddress(name);

        // Filter out bogus values sometimes returned
        if (p == IntPtr.Zero || p == new nint(1) || p == new nint(2) || p == new nint(3) || p == new nint(-1))
            return IntPtr.Zero;

        Console.WriteLine($"GetProcAddress (WGL): {name} = 0x{p:X}");
        return p;
    }

    private static IntPtr GetProcAddressFromNativeLib(string name)
    {
        if (_libGL == IntPtr.Zero)
        {
            // opengl32.dll for fallback (core functions <= 1.1)
            if (!NativeLibrary.TryLoad("opengl32.dll", out _libGL))
                throw new Exception("Failed to load opengl32.dll");
        }

        if (NativeLibrary.TryGetExport(_libGL, name, out var p))
        {
            Console.WriteLine($"GetProcAddress (fallback): {name} = 0x{p:X}");
            return p;
        }

        return IntPtr.Zero;
    }
}
