using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

internal static class Program
{
    [STAThread]
    private unsafe static void Main()
    {
        // 1) Win32 window + DC
        var win = Win32.CreateWindow(1280, 720, "OpenGL 3.3 Triangle (C# WGL)");

        // 2) Choose and set a pixel format (legacy path works fine)
        Win32.SetupPixelFormat(win.Hdc);

        // 3) Dummy legacy context just to load WGL extensions
        IntPtr dummyRC = Wgl.wglCreateContext(win.Hdc);
        if (dummyRC == IntPtr.Zero)
            throw new Exception("Failed to create dummy WGL context.");

        if (!Wgl.wglMakeCurrent(win.Hdc, dummyRC))
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
                realRC = wglCreateContextAttribsARB(win.Hdc, IntPtr.Zero, p);
            }
        }
        if (realRC == IntPtr.Zero)
            throw new Exception("Failed to create OpenGL 3.3 Core context.");

        // Switch to real context and delete dummy
        Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        Wgl.wglDeleteContext(dummyRC);
        if (!Wgl.wglMakeCurrent(win.Hdc, realRC))
            throw new Exception("Failed to make current real GL context.");

        // 5) Load GL entry points
        Gpu.InitPlatformGL();

        var gl = new GL();
        gl.Load(Wgl.GetProcAddressWithFallback);

        Console.WriteLine($"GL_VERSION: {gl.GetStringAnsi(GL.GL_VERSION)}");
        Console.WriteLine($"GL_VENDOR : {gl.GetStringAnsi(GL.GL_VENDOR)}");
        Console.WriteLine($"GL_RENDERER: {gl.GetStringAnsi(GL.GL_RENDERER)}");

        // 6) Create pipeline objects (VAO/VBO/Shader)
        uint vao = 0, vbo = 0;
        unsafe
        {
            gl.GenVertexArrays(1, &vao);
            gl.BindVertexArray(vao);
            gl.GenBuffers(1, &vbo);
            gl.BindBuffer(GL.GL_ARRAY_BUFFER, vbo);

            // simple triangle
            ReadOnlySpan<float> verts = new float[]
            {
                // pos(x,y)
                -0.6f, -0.5f,
                 0.6f, -0.5f,
                 0.0f,  0.6f,
            };
            fixed (float* pv = verts)
            {
                gl.BufferData(GL.GL_ARRAY_BUFFER, (nint)(verts.Length * sizeof(float)), (IntPtr)pv, GL.GL_STATIC_DRAW);
            }

            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, GL.GL_FLOAT, GL.GL_FALSE, 2 * sizeof(float), IntPtr.Zero);
        }

        // Vertex/Fragment shaders
        string vs = "#version 330 core\n"
                  + "layout(location=0) in vec2 aPos;\n"
                  + "void main(){ gl_Position = vec4(aPos, 0.0, 1.0); }\n";

        string fs = "#version 330 core\n"
                  + "out vec4 FragColor;\n"
                  + "void main(){ FragColor = vec4(0.95, 0.4, 0.2, 1.0); }\n";

        uint prog = GLHelpers.CompileProgram(gl, vs, fs);
        gl.UseProgram(prog);

        // Initial viewport
        Win32.GetClientSize(win.Hwnd, out int cw, out int ch);
        gl.Viewport(0, 0, (uint)cw, (uint)ch);

        // 7) Main loop
        bool running = true;
        while (running)
        {
            while (Win32.PeekMessage(out var msg))
            {
                if (msg.message == Win32.User32.WM_QUIT)
                    running = false;
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }

            // Handle resize (updated by WndProc)
            if (Win32.TryDequeueResize(out int w, out int h))
            {
                gl.Viewport(0, 0, (uint) Math.Max(1, w), (uint) Math.Max(1, h));
            }

            // Render
            gl.ClearColor(0.07f, 0.08f, 0.12f, 1f);
            gl.Clear(GL.GL_COLOR_BUFFER_BIT);
            gl.DrawArrays(GL.GL_TRIANGLES, 0, 3);

            Win32.SwapBuffers(win.Hdc);
        }

        // Cleanup
        unsafe
        {
            gl.DeleteProgram(prog);
            gl.DeleteBuffers(1, &vbo);
            gl.DeleteVertexArrays(1, &vao);
        }
        Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        Wgl.wglDeleteContext(realRC);
        Win32.DestroyWindow(win.Hwnd);
    }
}

internal static class GLHelpers
{
    public static unsafe uint CompileProgram(GL gl, string vsSource, string fsSource)
    {
        uint vs = gl.CreateShader(GL.GL_VERTEX_SHADER);
        GLSetSource(gl, vs, vsSource);
        gl.CompileShader(vs);
        CheckShader(gl, vs, "Vertex");

        uint fs = gl.CreateShader(GL.GL_FRAGMENT_SHADER);
        GLSetSource(gl, fs, fsSource);
        gl.CompileShader(fs);
        CheckShader(gl, fs, "Fragment");

        uint p = gl.CreateProgram();
        gl.AttachShader(p, vs);
        gl.AttachShader(p, fs);
        gl.LinkProgram(p);
        CheckProgram(gl, p);

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return p;
    }

    private static unsafe void GLSetSource(GL gl, uint shader, string src)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(src);
        fixed (byte* p = bytes)
        {
            byte*[] arr = new byte*[1] { p };
            int len = bytes.Length;
            fixed (byte** parr = arr)
            {
                gl.ShaderSource(shader, 1, parr, &len);
            }
        }
    }

    private static unsafe void CheckShader(GL gl, uint shader, string label)
    {
        int status = 0;
        gl.GetShaderiv(shader, GL.GL_COMPILE_STATUS, &status);
        if (status == 0)
        {
            int len = 0;
            gl.GetShaderiv(shader, GL.GL_INFO_LOG_LENGTH, &len);
            var sb = new byte[Math.Max(len, 1024)];
            fixed (byte* p = sb)
            {
                gl.GetShaderInfoLog(shader, sb.Length, IntPtr.Zero, (IntPtr)p);
                throw new Exception($"{label} compile error: {Encoding.ASCII.GetString(sb)}");
            }
        }
    }

    private static unsafe void CheckProgram(GL gl, uint prog)
    {
        int status = 0;
        gl.GetProgramiv(prog, GL.GL_LINK_STATUS, &status);
        if (status == 0)
        {
            int len = 0;
            gl.GetProgramiv(prog, GL.GL_INFO_LOG_LENGTH, &len);
            var sb = new byte[Math.Max(len, 1024)];
            fixed (byte* p = sb)
            {
                gl.GetProgramInfoLog(prog, sb.Length, IntPtr.Zero, (IntPtr)p);
                throw new Exception($"Link error: {Encoding.ASCII.GetString(sb)}");
            }
        }
    }
}

