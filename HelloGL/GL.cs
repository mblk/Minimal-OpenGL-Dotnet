using System.Runtime.InteropServices;

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
    [DllImport("opengl32.dll", ExactSpelling = true)]
    public static extern IntPtr wglCreateContext(IntPtr hdc);

    [DllImport("opengl32.dll", ExactSpelling = true)]
    public static extern bool wglDeleteContext(IntPtr hglrc);

    [DllImport("opengl32.dll", ExactSpelling = true)]
    public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

    [DllImport("opengl32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr wglGetProcAddress(string name);

    public static IntPtr GetProcAddress(string name)
    {
        var p = wglGetProcAddress(name);

        // Filter out bogus values sometimes returned
        if (p == IntPtr.Zero || p == new IntPtr(1) || p == new IntPtr(2) || p == new IntPtr(3) || p == new IntPtr(-1))
            return IntPtr.Zero;

        return p;
    }

    public static IntPtr GetProcAddressWithFallback(string name)
    {
        var p = GetProcAddress(name);
        if (p != IntPtr.Zero)
            return p;

        // core symbols may be in opengl32.dll
        p = Gpu.GetExport(name);

        return p;
    }
}
