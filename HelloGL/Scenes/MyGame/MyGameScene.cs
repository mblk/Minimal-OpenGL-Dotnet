using HelloGL.Engine;
using HelloGL.Platforms;
using System.Numerics;

namespace HelloGL.Scenes.MyGame;

internal class MyGameScene : Scene
{
    private DynamicGeometryRenderer2D _renderer = null!;

    private Vector2 _playerPos = new Vector2(100, 100);

    public MyGameScene(AssetManager assetManager)
        : base(assetManager)
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

    public override void Update(float dt, IInput input)
    {
        var kb = input.Keyboard;

        if (kb.WasPressed(Key.Space))
        {
            Console.WriteLine("jump");
        }

        if (kb.Get(Key.A)) _playerPos.X -= 100f * dt;
        if (kb.Get(Key.D)) _playerPos.X += 100f * dt;
        if (kb.Get(Key.W)) _playerPos.Y -= 100f * dt;
        if (kb.Get(Key.S)) _playerPos.Y += 100f * dt;
    }

    public override void Render(float dt, (int,int) windowSize)
    {
        var (width, height) = windowSize;

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;

        var mvp = mModel * mView * mOrthoProj;

        _renderer.AddTriangle(
            _playerPos + new Vector2(0, 0), new Vector3(1.0f, 0.0f, 0.0f),
            _playerPos + new Vector2(50, 0), new Vector3(0.0f, 1.0f, 0.0f),
            _playerPos + new Vector2(50, 50), new Vector3(0.0f, 0.0f, 1.0f)
        );

        _renderer.AddTriangle(
            _playerPos + new Vector2(100, 0), new Vector3(1.0f, 0.0f, 0.0f),
            _playerPos + new Vector2(150, 0), new Vector3(0.0f, 1.0f, 0.0f),
            _playerPos + new Vector2(150, 50), new Vector3(0.0f, 0.0f, 1.0f)
        );

        _renderer.Render(mvp);
    }
}
