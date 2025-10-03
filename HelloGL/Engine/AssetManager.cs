using System.Diagnostics;

namespace HelloGL.Engine;

public interface IAssetReader
{
    string GetAssetPath(AssetType assetType, string assetName);

    string ReadFileAsString(string assetPath);

    byte[] ReadFileAsBytes(string assetPath);
}

public abstract class Asset
{
    // hmm
}

public class AssetManager : IDisposable
{
    private readonly IAssetReader _reader;
    private readonly ShaderLoader _shaderLoader;

    private readonly Dictionary<string, Shader> _shaders = [];

    public static DirectoryInfo FindBaseDirectory()
    {
        var startDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        var currentDir = startDir;
        while (currentDir != null)
        {
            var assetsDir = currentDir.GetDirectories("Assets").SingleOrDefault();
            if (assetsDir is not null)
            {
                Console.WriteLine($"Found Assets directory: {assetsDir.FullName}");
                return assetsDir;
            }
            currentDir = currentDir.Parent;
        }

        throw new Exception($"Could not find asset base directory. Started search at: {startDir.FullName}");
    }

    public GL GL { get; }

    public AssetManager(DirectoryInfo baseDir, GL gl)
    {
        GL = gl;

        _reader = new FileSystemAssetReader(baseDir);
        _shaderLoader = new ShaderLoader(_reader, gl);
    }

    public Shader LoadShader(string name)
    {
        if (_shaders.TryGetValue(name, out var cachedShader))
            return cachedShader;

        var loadedShader = _shaderLoader.Load(name);

        var shader = loadedShader.Asset;
        var sourceFiles = loadedShader.SourceFiles;

        RegisterAssetDependencies(shader, sourceFiles);

        _shaders.Add(name, shader);

        return shader;
    }

    public bool ReloadAsset(Asset asset)
    {
        try
        {
            switch (asset)
            {
                case Shader shader:
                {
                    Debug.Assert(_shaders.ContainsKey(shader.Name));
                    var reloadedShader = _shaderLoader.Reload(shader);
                    RegisterAssetDependencies(shader, reloadedShader.SourceFiles);
                    break;
                }

                default:
                    Console.WriteLine($"Error: Unknown asset type to reload: {asset.GetType().Name}");
                    break;
            }

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error reloading asset: {e.Message}");
            return false;
        }
    }

    public virtual void Dispose()
    {
        foreach (var shader in _shaders.Values)
        {
            shader.Dispose();
        }
        _shaders.Clear();
    }

    protected virtual void RegisterAssetDependencies(Asset asset, IReadOnlySet<string> files)
    {
        // Override in derived classes to track file dependencies for reloading.
    }

    private static string GetAssetSubDirectoryName(AssetType type) => type switch
    {
        AssetType.Shader => "Shaders",
        AssetType.Texture => "Textures",
        AssetType.Model => "Models",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private class FileSystemAssetReader : IAssetReader
    {
        protected readonly DirectoryInfo _baseDir;

        public FileSystemAssetReader(DirectoryInfo baseDir)
        {
            if (!baseDir.Exists) 
                throw new DirectoryNotFoundException($"Asset base directory does not exist: {baseDir.FullName}");

            _baseDir = baseDir;
        }

        public string GetAssetPath(AssetType assetType, string assetName)
        {
            return Path.Combine(_baseDir.FullName, GetAssetSubDirectoryName(assetType), assetName);
        }

        public string ReadFileAsString(string assetPath)
        {
            var fileInfo = new FileInfo(assetPath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException($"Asset file does not exist: {fileInfo.FullName}");

            return File.ReadAllText(fileInfo.FullName);
        }

        public byte[] ReadFileAsBytes(string assetPath)
        {
            var fileInfo = new FileInfo(assetPath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException($"Asset file does not exist: {fileInfo.FullName}");

            return File.ReadAllBytes(fileInfo.FullName);
        }
    }

    private class ArchiveAssetReader : IAssetReader
    {
        public ArchiveAssetReader(FileInfo archiveFile)
        {
            //
        }

        public string GetAssetPath(AssetType assetType, string assetName)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadFileAsBytes(string assetPath)
        {
            throw new NotImplementedException();
        }

        public string ReadFileAsString(string assetPath)
        {
            throw new NotImplementedException();
        }
    }
}