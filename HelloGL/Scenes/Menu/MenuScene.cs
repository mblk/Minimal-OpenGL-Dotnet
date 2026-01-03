using HelloGL.Engine;
using HelloGL.Platforms;
using System.Numerics;

namespace HelloGL.Scenes.Menu;

internal class MenuScene : Scene
{
    private DynamicGeometryRenderer2D _renderer = null!;

    private float _time = 0f;

    private int _selected = 0;

    public MenuScene(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
        _renderer = new DynamicGeometryRenderer2D(AssetManager);
    }

    public override void Unload()
    {
        _renderer.Dispose();
    }

    public override void Update(UpdateContext context)
    {
        var dt = context.DeltaTime;
        var kb = context.Input.Keyboard;

        if (kb.WasPressed(Key.Escape))
        {
            context.SceneController.RequestExit();
        }

        if (kb.WasPressed(Key.W) || kb.WasPressed(Key.Up))
        {
            _selected = Math.Max(0, _selected - 1);
        }

        if (kb.WasPressed(Key.S) || kb.WasPressed(Key.Down))
        {
            _selected = Math.Min(2, _selected + 1);
        }

        if (kb.WasPressed(Key.Tab))
        {
            _selected = (_selected + 1) % 3;
        }

        if (kb.WasPressed(Key.Enter))
        {
            switch (_selected)
            {
                case 0: context.SceneController.RequestSceneChange("classic"); break;
                case 1: context.SceneController.RequestSceneChange("test"); break;
                case 2: context.SceneController.RequestExit(); break;
            }
        }

        if (kb.WasReleased(Key.D1))
        {
            context.SceneController.RequestSceneChange("classic");
        }

        if (kb.WasReleased(Key.D2))
        {
            context.SceneController.RequestSceneChange("test");
        }

        if (kb.WasReleased(Key.D3))
        {
            context.SceneController.RequestExit();
        }

        _time += dt;
    }

    public override void Render(RenderContext context)
    {
        const float WorldWidth = 10f;
        const float WorldHeight = 10f;

        var (windowWidth, windowHeight) = context.WindowSize;

        float scale = MathF.Min(windowWidth / WorldWidth, windowHeight / WorldHeight);
        float viewportW = WorldWidth * scale;
        float viewportH = WorldHeight * scale;
        float viewportX = (windowWidth - viewportW) / 2f;
        float viewportY = (windowHeight - viewportH) / 2f;

        var gl = AssetManager.GL;
        gl.Viewport((int)viewportX, (int)viewportY, (int)viewportW, (int)viewportH); // TODO rounding issues?

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, WorldWidth, WorldHeight, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;
        var mvp = mModel * mView * mOrthoProj;

        _renderer.AddRectangle(
            new Vector2(WorldWidth * 0.5f, WorldHeight * 0.5f),
            new Vector2(WorldWidth, WorldHeight),
            new Vector3(0.1f, 0.1f, 0.1f));

        const float textScale = 1f / 64f;

        _renderer.AddText(new Vector2(1, 1), textScale, "Hello!");

        _renderer.AddText(new Vector2(1, 3), textScale, $"1: Classic {(_selected == 0 ? "<" : "")}");
        _renderer.AddText(new Vector2(1, 4), textScale, $"2: Test scene {(_selected == 1 ? "<" : "")}");
        _renderer.AddText(new Vector2(1, 5), textScale, $"3: Exit {(_selected == 2 ? "<" : "")}");

        _renderer.Render(mvp);
    }
}
