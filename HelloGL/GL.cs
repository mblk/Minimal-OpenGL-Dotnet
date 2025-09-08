using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HelloGL;

public unsafe class GL
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

    public const uint GL_ARRAY_BUFFER_BINDING = 0x8894;
    public const uint GL_VERTEX_ARRAY_BINDING = 0x85B5;
    public const uint GL_CURRENT_PROGRAM = 0x8B8D;

    public const uint GL_VERTEX_ATTRIB_ARRAY_ENABLED = 0x8622;
    public const uint GL_VERTEX_ATTRIB_ARRAY_SIZE = 0x8623;
    public const uint GL_VERTEX_ATTRIB_ARRAY_STRIDE = 0x8624;
    public const uint GL_VERTEX_ATTRIB_ARRAY_TYPE = 0x8625;
    public const uint GL_VERTEX_ATTRIB_ARRAY_NORMALIZED = 0x886A;
    public const uint GL_VERTEX_ATTRIB_ARRAY_BUFFER_BINDING = 0x889F;

    // Program query
    public const uint GL_ACTIVE_ATTRIBUTES = 0x8B89;
    public const uint GL_ACTIVE_UNIFORMS = 0x8B86;
    public const uint GL_ACTIVE_VARYINGS = 0x8C83; // nur wenn Transform Feedback genutzt wird

    // Polygon mode (zum Debuggen als Wireframe)
    public const uint GL_POLYGON_MODE = 0x0B40;
    public const uint GL_FRONT_AND_BACK = 0x0408;
    public const uint GL_LINE = 0x1B01;
    public const uint GL_FILL = 0x1B02;

    // Capabilities zum Ein/Aus-Schalten
    public const uint GL_CULL_FACE = 0x0B44;
    public const uint GL_DEPTH_TEST = 0x0B71;

    public const uint GL_BACK = 0x0405;
    public const uint GL_DRAW_BUFFER = 0x0C01;
    public const uint GL_READ_BUFFER = 0x0C02;

    public const uint GL_RGBA = 0x1908;
    public const uint GL_UNSIGNED_BYTE = 0x1401;

    // Function pointers
    public delegate* unmanaged[Cdecl]<uint> GetError;

    public delegate* unmanaged[Cdecl]<float, float, float, float, void> ClearColor;
    public delegate* unmanaged[Cdecl]<uint, void> Clear;
    public delegate* unmanaged[Cdecl]<int, int, int, int, void> Viewport;

    public delegate* unmanaged[Cdecl]<uint, IntPtr> GetString;
    public delegate* unmanaged[Cdecl]<uint /*pname*/, int* /*data*/, void> GetIntegerv;
    
    public delegate* unmanaged[Cdecl]<uint /*face*/, uint /*mode*/, void> PolygonMode;
    public delegate* unmanaged[Cdecl]<uint /*cap*/, void> Enable;
    public delegate* unmanaged[Cdecl]<uint /*cap*/, void> Disable;

    public delegate* unmanaged[Cdecl]<void> Finish;
    public delegate* unmanaged[Cdecl]<void> Flush;

    public delegate* unmanaged[Cdecl]<uint /*buf*/, void> DrawBuffer;
    public delegate* unmanaged[Cdecl]<uint /*src*/, void> ReadBuffer;

    public unsafe delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, IntPtr, void> ReadPixels;

    public delegate* unmanaged[Cdecl]<uint, uint> CreateShader;
    public delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void> ShaderSource;
    public delegate* unmanaged[Cdecl]<uint, void> CompileShader;
    public delegate* unmanaged[Cdecl]<uint, uint, int*, void> GetShaderiv;
    public delegate* unmanaged[Cdecl]<uint, int, IntPtr, IntPtr, void> GetShaderInfoLog;
    public delegate* unmanaged[Cdecl]<uint> CreateProgram;
    public delegate* unmanaged[Cdecl]<uint, uint, void> AttachShader;
    public delegate* unmanaged[Cdecl]<uint, void> LinkProgram;
    public delegate* unmanaged[Cdecl]<uint, uint, int*, void> GetProgramiv;
    public delegate* unmanaged[Cdecl]<uint, int, IntPtr, IntPtr, void> GetProgramInfoLog;
    public delegate* unmanaged[Cdecl]<uint, void> UseProgram;
    public delegate* unmanaged[Cdecl]<int, uint*, void> GenVertexArrays;
    public delegate* unmanaged[Cdecl]<uint, void> BindVertexArray;
    public delegate* unmanaged[Cdecl]<int, uint*, void> GenBuffers;
    public delegate* unmanaged[Cdecl]<uint, uint, void> BindBuffer;
    public delegate* unmanaged[Cdecl]<uint, nint, IntPtr, uint, void> BufferData;
    //public delegate* unmanaged[Cdecl]<uint, int, uint, uint, int, IntPtr, void> VertexAttribPointer;

    public delegate* unmanaged[Cdecl]
        <uint  /*index*/,
          int   /*size*/,
          uint  /*type*/,
          byte  /*normalized (GLboolean: 0/1)*/,
          int   /*stride*/,
          IntPtr /*pointer*/,
          void> VertexAttribPointer;

    public delegate* unmanaged[Cdecl]<uint /*program*/, IntPtr /*name*/, int> GetAttribLocation;

    public delegate* unmanaged[Cdecl]<uint /*index*/, uint /*pname*/, int* /*params*/, void> GetVertexAttribiv;

    public delegate* unmanaged[Cdecl]<uint, void> EnableVertexAttribArray;
    public delegate* unmanaged[Cdecl]<uint, void> DisableVertexAttribArray;
    public delegate* unmanaged[Cdecl]<uint, int, uint, void> DrawArrays;
    public delegate* unmanaged[Cdecl]<uint, void> DeleteShader;
    public delegate* unmanaged[Cdecl]<uint, void> DeleteProgram;
    public delegate* unmanaged[Cdecl]<int, uint*, void> DeleteBuffers;
    public delegate* unmanaged[Cdecl]<int, uint*, void> DeleteVertexArrays;

    public void Load(Func<string, IntPtr> getProc)
    {
        GetError = (delegate* unmanaged[Cdecl]<uint>)Load(getProc, "glGetError");

        ClearColor = (delegate* unmanaged[Cdecl]<float, float, float, float, void>)Load(getProc, "glClearColor");
        Clear = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glClear");
        Viewport = (delegate* unmanaged[Cdecl]<int, int, int, int, void>)Load(getProc, "glViewport");

        GetString = (delegate* unmanaged[Cdecl]<uint, IntPtr>)Load(getProc, "glGetString");
        GetIntegerv = (delegate* unmanaged[Cdecl]<uint, int*, void>)Load(getProc, "glGetIntegerv");

        PolygonMode = (delegate* unmanaged[Cdecl]<uint, uint, void>)Load(getProc, "glPolygonMode");
        Enable = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glEnable");
        Disable = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glDisable");

        Finish = (delegate* unmanaged[Cdecl]<void>)Load(getProc, "glFinish");
        Flush = (delegate* unmanaged[Cdecl]<void>)Load(getProc, "glFlush");

        DrawBuffer = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glDrawBuffer");
        ReadBuffer = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glReadBuffer");

        ReadPixels = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, IntPtr, void>)Load(getProc, "glReadPixels");

        CreateShader = (delegate* unmanaged[Cdecl]<uint, uint>)Load(getProc, "glCreateShader");
        ShaderSource = (delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void>)Load(getProc, "glShaderSource");
        CompileShader = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glCompileShader");
        GetShaderiv = (delegate* unmanaged[Cdecl]<uint, uint, int*, void>)Load(getProc, "glGetShaderiv");
        GetShaderInfoLog = (delegate* unmanaged[Cdecl]<uint, int, IntPtr, IntPtr, void>)Load(getProc, "glGetShaderInfoLog");

        CreateProgram = (delegate* unmanaged[Cdecl]<uint>)Load(getProc, "glCreateProgram");
        AttachShader = (delegate* unmanaged[Cdecl]<uint, uint, void>)Load(getProc, "glAttachShader");
        LinkProgram = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glLinkProgram");
        GetProgramiv = (delegate* unmanaged[Cdecl]<uint, uint, int*, void>)Load(getProc, "glGetProgramiv");
        GetProgramInfoLog = (delegate* unmanaged[Cdecl]<uint, int, IntPtr, IntPtr, void>)Load(getProc, "glGetProgramInfoLog");
        UseProgram = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glUseProgram");

        GenVertexArrays = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load(getProc, "glGenVertexArrays");
        BindVertexArray = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glBindVertexArray");
        GenBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load(getProc, "glGenBuffers");
        BindBuffer = (delegate* unmanaged[Cdecl]<uint, uint, void>)Load(getProc, "glBindBuffer");
        BufferData = (delegate* unmanaged[Cdecl]<uint, nint, IntPtr, uint, void>)Load(getProc, "glBufferData");

        VertexAttribPointer = (delegate* unmanaged[Cdecl]<uint, int, uint, byte, int, IntPtr, void>)Load(getProc, "glVertexAttribPointer");

        GetAttribLocation = (delegate* unmanaged[Cdecl]<uint, IntPtr, int>)Load(getProc, "glGetAttribLocation");
        GetVertexAttribiv = (delegate* unmanaged[Cdecl]<uint, uint, int*, void>)Load(getProc, "glGetVertexAttribiv");

        EnableVertexAttribArray = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glEnableVertexAttribArray");
        DisableVertexAttribArray = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glDisableVertexAttribArray");
        DrawArrays = (delegate* unmanaged[Cdecl]<uint, int, uint, void>)Load(getProc, "glDrawArrays");

        DeleteShader = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glDeleteShader");
        DeleteProgram = (delegate* unmanaged[Cdecl]<uint, void>)Load(getProc, "glDeleteProgram");
        DeleteBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load(getProc, "glDeleteBuffers");
        DeleteVertexArrays = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load(getProc, "glDeleteVertexArrays");

        // ---

        Console.WriteLine("OpenGL initialized:");
        Console.WriteLine($"GL_VERSION : {GetStringAnsi(GL_VERSION)}");
        Console.WriteLine($"GL_VENDOR  : {GetStringAnsi(GL_VENDOR)}");
        Console.WriteLine($"GL_RENDERER: {GetStringAnsi(GL_RENDERER)}");
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

    public unsafe int GetAttribLocationUtf8(uint prog, string name)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(name + "\0");
        fixed (byte* p = bytes)
        {
            return GetAttribLocation(prog, (IntPtr)p);
        }
    }

    public void CheckError(string? msg = null, [CallerFilePath] string? file = "", [CallerLineNumber] int line = 0)
    {
        var err = GetError();
        if (err != 0)
        {
            var s = $"[{file}:{line}] GL Error: 0x{err:X}";
            if (msg != null)
                s += $" ({msg})";
            throw new Exception(s);
        }
    }
}

public static class Gpu
{
    private static IntPtr _libGL = IntPtr.Zero;

    public static void InitPlatformGL()
    {
        if (OperatingSystem.IsLinux())
        {
            NativeLibrary.TryLoad("libGL.so.1", out _libGL);
        }
        else if (OperatingSystem.IsWindows())
        {
            // opengl32.dll for fallback (core functions <= 1.1)
            NativeLibrary.TryLoad("opengl32.dll", out _libGL);
        }
        else
        {
            throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
        }
    }

    public static IntPtr GetExport(string name)
    {
        if (_libGL != IntPtr.Zero && NativeLibrary.TryGetExport(_libGL, name, out var p))
            return p;
        return IntPtr.Zero;
    }
}

public static class Wgl
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
