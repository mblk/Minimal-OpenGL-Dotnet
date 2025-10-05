using HelloGL.Engine;
using HelloGL.Platforms;
using HelloGL.Scenes.Catris;
using HelloGL.Scenes.MyGame;
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
        //var windowOptions = new WindowOptions(600, 1000, "Catris");
        var windowOptions = new WindowOptions(1600, 1000, "Catris");

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

        Scene currentScene = new MyGameScene(assetManager);
        //Scene currentScene = new CatrisGameScene(assetManager);

        currentScene.Load();

        // main loop

        var sw = Stopwatch.StartNew();
        var frameCount = 0;

        var lastTime = DateTime.Now;

        while (window.ProcessEvents())
        {
            //
            // update
            //

            var now = DateTime.Now;
            float dt = (float)(now - lastTime).TotalSeconds;
            lastTime = now;

            (assetManager as AssetManagerWithHotReload)?.ProcessChanges();

            if (window.Input.Keyboard.WasPressed(Key.Escape)) break;

            currentScene.Update(dt, window.Input);

            //
            // render
            //

            gl.ClearColor(0.07f, 0.08f, 0.12f, 1f);
            gl.Clear(GL.ClearBufferMask.COLOR_BUFFER_BIT);

            currentScene.Render(dt, window.Size);

            window.SwapBuffers();

            //
            //
            //

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

        currentScene.Unload();
    }
}
