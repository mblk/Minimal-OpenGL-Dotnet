using HelloGL.Platforms;

namespace HelloGL.Engine;

public abstract class Scene
{
    protected AssetManager AssetManager { get; }

    public Scene(AssetManager assetManager)
    {
        AssetManager = assetManager;
    }

    public abstract void Load();
    public abstract void Unload();

    public abstract void Update(float dt, IInput input);
    public abstract void Render(float dt, (int, int) windowSize);
}
