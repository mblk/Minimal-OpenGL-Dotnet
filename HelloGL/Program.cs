using HelloGL.Platforms;
using System.Diagnostics;

namespace HelloGL;

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

            gl.BindVertexArray(vao);

            gl.GenBuffers(1, &vbo);

            gl.BindBuffer(GL.GL_ARRAY_BUFFER, vbo);

            ReadOnlySpan<float> verts = [-0.6f, -0.5f, 0.6f, -0.5f, 0.0f, 0.6f,];
            fixed (float* pv = verts)
            {
                gl.BufferData(GL.GL_ARRAY_BUFFER, verts.Length * sizeof(float), (nint)pv, GL.GL_STATIC_DRAW);
            }

            gl.EnableVertexAttribArray(0);

            gl.VertexAttribPointer(0, 2, GL.GL_FLOAT, (byte)GL.GL_FALSE, 2 * sizeof(float), IntPtr.Zero);
        }

        string vs = "#version 330 core\n"
                  + "layout(location=0) in vec2 aPos;\n"
                  + "void main(){ gl_Position = vec4(aPos, 0.0, 1.0); }\n";

        string fs = "#version 330 core\n"
                  + "out vec4 FragColor;\n"
                  + "void main(){ FragColor = vec4(0.95, 0.4, 0.2, 1.0); }\n";

        uint prog = GLHelpers.CompileProgram(gl, vs, fs);
        gl.UseProgram(prog);

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
