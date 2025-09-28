using HelloGL.Engine;
using HelloGL.Platforms;
using HelloGL.Utils;
using System.Diagnostics;

namespace HelloGL;

internal unsafe static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        SystemInfo.PrintSystemAndBuildInfo();

        // init platform

        var platformOptions = new PlatformOptions();
        var windowOptions = new WindowOptions(1280, 720, "OpenGL 3.3 Triangle (C#)");

        using var platform = PlatformFactory.CreatePlatform(platformOptions);
        using var window = platform.CreateWindow(windowOptions);

        var gl = window.GL;

        // init assetmanager

        var assetBaseDir = AssetManager.FindBaseDirectory();

        var useAssetHotReloading = true;

        AssetManager assetManager = useAssetHotReloading
            ? new AssetManagerWithHotReload(assetBaseDir, gl)
            : new AssetManager(assetBaseDir, gl);

        // init content

        uint vao = 0, vbo = 0;
        {
            gl.GenVertexArrays(1, &vao);
            gl.BindVertexArray(vao);

            gl.GenBuffers(1, &vbo);
            gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, vbo);

            ReadOnlySpan<float> verts = [-0.6f, -0.5f, 0.6f, -0.5f, 0.0f, 0.6f,];
            fixed (float* pv = verts)
            {
                gl.BufferData(GL.BufferTarget.ARRAY_BUFFER, verts.Length * sizeof(float), pv, GL.BufferUsage.STATIC_DRAW);
            }

            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, GL.VertexAttribPointerType.FLOAT, false, 2 * sizeof(float), 0);
        }

        var shader = assetManager.LoadShader("triangle");

        // main loop

        var sw = Stopwatch.StartNew();
        var frameCount = 0;

        while (window.ProcessEvents())
        {
            //xxx
            (assetManager as AssetManagerWithHotReload)?.ProcessChanges();
            //xxx

            gl.ClearColor(0.07f, 0.08f, 0.12f, 1f);
            gl.Clear(GL.ClearBufferMask.COLOR_BUFFER_BIT);

            shader.Use();
            gl.DrawArrays(GL.PrimitiveType.TRIANGLES, 0, 3);
            shader.Unuse();

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

        gl.DeleteBuffers(1, &vbo);
        gl.DeleteVertexArrays(1, &vao);
    }
}

