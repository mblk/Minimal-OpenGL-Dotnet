using System.Numerics;
using System.Runtime.InteropServices;

namespace HelloGL.Engine;

public unsafe class DynamicGeometryRenderer2D : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPC
    {
        public Vector2 Position;
        public Vector3 Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPCT
    {
        public Vector2 Position;
        public Vector3 Color;
        public Vector2 UV;
    }

    private const int _initialVertexBufferSize = 1024;


    private readonly Shader _shader;
    private readonly Shader _textureShader;

    private Texture _texture1 = null!;
    private Texture _texture2 = null!;
    private Texture _texture3 = null!;

    private Font _font1 = null!;

    private readonly BufferObject _vertexBufferObject;
    private readonly VertexArrayObject<VertexPC> _vertexArrayObject;
    private readonly List<VertexPC> _vertexBuffer = new(_initialVertexBufferSize);

    private readonly BufferObject _textureVertexBufferObject;
    private readonly VertexArrayObject<VertexPCT> _textureVertexArrayObject;
    private readonly List<VertexPCT> _textureVertexBuffer = new(_initialVertexBufferSize);

    public DynamicGeometryRenderer2D(AssetManager assetManager)
    {
        _shader = assetManager.LoadShader("triangle");
        _textureShader = assetManager.LoadShader("texture");

        _texture1 = assetManager.LoadTexture("1");
        _texture2 = assetManager.LoadTexture("2");
        _texture3 = assetManager.LoadTexture("font1");

        _font1 = assetManager.LoadFont("font1");

        _vertexBufferObject = new BufferObject(assetManager.GL);
        _vertexBufferObject.SetSizeAndUsage(sizeof(VertexPC) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
        _vertexArrayObject = new VertexArrayObject<VertexPC>(assetManager.GL, _vertexBufferObject);

        _textureVertexBufferObject = new BufferObject(assetManager.GL);
        _textureVertexBufferObject.SetSizeAndUsage(sizeof(VertexPCT) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
        _textureVertexArrayObject = new VertexArrayObject<VertexPCT>(assetManager.GL, _textureVertexBufferObject);
    }

    public void AddTriangle(Vector2 p1, Vector3 c1, Vector2 p2, Vector3 c2, Vector2 p3, Vector3 c3)
    {
        _vertexBuffer.Add(new VertexPC() { Position = p1, Color = c1 });
        _vertexBuffer.Add(new VertexPC() { Position = p2, Color = c2 });
        _vertexBuffer.Add(new VertexPC() { Position = p3, Color = c3 });
    }

    public void AddRectangle(Vector2 center, Vector2 size, Vector3 color)
    {
        Vector2 halfSize = size * 0.5f;

        Vector2 bl = new(center.X - halfSize.X, center.Y - halfSize.Y);
        Vector2 br = new(center.X + halfSize.X, center.Y - halfSize.Y);
        Vector2 tr = new(center.X + halfSize.X, center.Y + halfSize.Y);
        Vector2 tl = new(center.X - halfSize.X, center.Y + halfSize.Y);

        _vertexBuffer.Add(new VertexPC { Position = bl, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = br, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tr, Color = color });

        _vertexBuffer.Add(new VertexPC { Position = bl, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tr, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tl, Color = color });
    }

    public void AddRectangleWithTexture(Vector2 center, Vector2 size, Vector3 color, Vector2 uvMin, Vector2 uvMax)
    {
        Vector2 halfSize = size * 0.5f;

        Vector2 bl = new(center.X - halfSize.X, center.Y - halfSize.Y);
        Vector2 br = new(center.X + halfSize.X, center.Y - halfSize.Y);
        Vector2 tr = new(center.X + halfSize.X, center.Y + halfSize.Y);
        Vector2 tl = new(center.X - halfSize.X, center.Y + halfSize.Y);

        _textureVertexBuffer.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = br, Color = color, UV = new(uvMax.X, uvMax.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = tl, Color = color, UV = new(uvMin.X, uvMin.Y) });
    }

    public void Render(Matrix4x4 mvp)
    {
        if (_vertexBuffer.Count > 0)
        {
            ReadOnlySpan<VertexPC> span = CollectionsMarshal.AsSpan(_vertexBuffer);

            _vertexBufferObject.SetData(span, GL.BufferUsage.STREAM_DRAW);

            _shader.Use();
            _shader.SetUniform("uMVP", mvp);

            _vertexArrayObject.Bind();
            _vertexArrayObject.Draw(GL.PrimitiveType.TRIANGLES, 0, (uint)_vertexBuffer.Count);
            _vertexArrayObject.Unbind();

            _shader.Unuse();

            _vertexBuffer.Clear();
        }

        if (_textureVertexBuffer.Count > 0)
        {
            ReadOnlySpan<VertexPCT> span = CollectionsMarshal.AsSpan(_textureVertexBuffer);

            _textureVertexBufferObject.SetData(span, GL.BufferUsage.STREAM_DRAW);

            _texture3.Bind(0);
            {
                _textureShader.Use();
                _textureShader.SetUniform("uMVP", mvp);
                _textureShader.SetUniform("uTex", 0);
                {
                    _textureVertexArrayObject.Bind();
                    _textureVertexArrayObject.Draw(GL.PrimitiveType.TRIANGLES, 0, (uint)_textureVertexBuffer.Count);
                    _textureVertexArrayObject.Unbind();
                }
                _textureShader.Unuse();
            }
            _texture3.Unbind(0);

            _textureVertexBuffer.Clear();
        }
    }

    public void Dispose()
    {
        _vertexArrayObject.Dispose();
        _vertexBufferObject.Dispose();

        _textureVertexArrayObject.Dispose();
        _textureVertexBufferObject.Dispose();

        _texture1.Dispose();
        _texture2.Dispose();
        _texture3.Dispose();

        _font1.Dispose();

        _shader.Dispose();
        _textureShader.Dispose();
    }
}