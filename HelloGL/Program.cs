using HelloGL.Engine;
using HelloGL.Platforms;
using HelloGL.Scenes.Catris;
using HelloGL.Scenes.Menu;
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
        var windowOptions = new WindowOptions(600, 1000, "game999", SwapInterval: 1);
        
	    using var platform = PlatformFactory.CreatePlatform(platformOptions);
        using var window = platform.CreateWindow(windowOptions);

        var gl = window.GL;

        // init assetmanager

        var assetBaseDir = AssetManager.FindBaseDirectory();

        var useAssetHotReloading = true;

        AssetManager assetManager = useAssetHotReloading
            ? new AssetManagerWithHotReload(assetBaseDir, gl)
            : new AssetManager(assetBaseDir, gl);

        // init scenes

        using SceneManager sceneManager = new SceneManager(new SceneContext()
        {
            AssetManager = assetManager,
        });

	    sceneManager.RegisterScene("menu", c => new MenuScene(c));
        sceneManager.RegisterScene("classic", c => new CatrisGameScene(c));
        sceneManager.RegisterScene("test", c => new MyGameScene(c));

        sceneManager.SetCurrentScene("menu");

        // main loop

        var sw = Stopwatch.StartNew();
        var frameCount = 0;

        var lastTime = DateTime.Now;

        while (window.ProcessEvents() && !sceneManager.ExitRequested)
        {
            //
            // update
            //

            var now = DateTime.Now;
            float dt = (float)(now - lastTime).TotalSeconds;
            lastTime = now;

            (assetManager as AssetManagerWithHotReload)?.ProcessChanges();

            sceneManager.Update(new UpdateContext()
            {
                DeltaTime = dt,
                WindowSize = window.Size,
                Input = window.Input,
                SceneController = sceneManager,
            });

            //
            // render
            //

            var (windowWidth, windowHeight) = window.Size;

            gl.Viewport(0, 0, windowWidth, windowHeight);

            gl.ClearColor(0.07f, 0.08f, 0.12f, 1f);
            gl.Clear(GL.ClearBufferMask.COLOR_BUFFER_BIT);

            sceneManager.Render(new RenderContext()
            {
                DeltaTime = dt,
                WindowSize = window.Size,
            });

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

        
    }
}
