using HelloGL.Engine;
using HelloGL.Platforms;
using HelloGL.Utils;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HelloGL;

[StructLayout(LayoutKind.Sequential)]
public struct MyVertex1
{
    public Vector2 Position;
    public Vector3 Color;
}

[StructLayout(LayoutKind.Sequential)]
public struct MyVertex2
{
    public Vector3 Position;
    public Vector3 Color;
    public Vector2 TexCoord;
}

internal unsafe static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        SystemInfo.PrintSystemAndBuildInfo();

        // init platform

        var platformOptions = new PlatformOptions();
        var windowOptions = new WindowOptions(1280, 720, "HelloGL");

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

        ReadOnlySpan<MyVertex1> verts1 =
        [
            new MyVertex1()
            {
                Position = new Vector2(-0.6f, -0.5f),
                Color = new Vector3(1.0f, 0.0f, 0.0f),
            },
            new MyVertex1()
            {
                Position = new Vector2(0.6f, -0.5f),
                Color = new Vector3(0.0f, 1.0f, 0.0f),
            },
            new MyVertex1()
            {
                Position = new Vector2(0.0f, 0.0f),
                Color = new Vector3(0.0f, 0.0f, 1.0f),
            },
        ];

        using var vertexBuffer1 = new BufferObject(gl);
        vertexBuffer1.SetData(verts1, GL.BufferUsage.STATIC_DRAW);
        using var vertexArray1 = new VertexArrayObject<MyVertex1>(gl, vertexBuffer1);

        using var vertexBuffer2 = new BufferObject(gl, "vertex buffer 2");
        vertexBuffer2.SetSizeAndUsage(1000, GL.BufferUsage.STREAM_DRAW);
        using var vertexArray2 = new VertexArrayObject<MyVertex1>(gl, vertexBuffer2, "vertex array 2");

        var shader = assetManager.LoadShader("triangle");

        // main loop

        var sw = Stopwatch.StartNew();
        var frameCount = 0;

        var startTime = DateTime.Now;

        while (window.ProcessEvents())
        {
            float t = (float)(DateTime.Now - startTime).TotalSeconds;

            //xxx
            (assetManager as AssetManagerWithHotReload)?.ProcessChanges();
            //xxx

            gl.ClearColor(0.07f, 0.08f, 0.12f, 1f);
            gl.Clear(GL.ClearBufferMask.COLOR_BUFFER_BIT);




            ReadOnlySpan<MyVertex1> verts2 =
            [
                new MyVertex1()
                {
                    Position = new Vector2(-0.6f, 0.5f),
                    Color = new Vector3(1.0f, 0.0f, 0.0f),
                },
                new MyVertex1()
                {
                    Position = new Vector2(0.6f * MathF.Sin(t), 0.5f * MathF.Cos(t)),
                    Color = new Vector3(0.0f, 1.0f, 0.0f),
                },
                new MyVertex1()
                {
                    Position = new Vector2(0.0f, 0.0f),
                    Color = new Vector3(0.0f, 0.0f, 1.0f),
                },
            ];

            vertexBuffer2.SetData(verts2, GL.BufferUsage.STREAM_DRAW);


            vertexArray1.Bind();
            shader.Use();
            gl.DrawArrays(GL.PrimitiveType.TRIANGLES, 0, 3);
            shader.Unuse();
            vertexArray1.Unbind();

            vertexArray2.Bind();
            shader.Use();
            gl.DrawArrays(GL.PrimitiveType.TRIANGLES, 0, 3);
            shader.Unuse();
            vertexArray2.Unbind();

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


    }
}

