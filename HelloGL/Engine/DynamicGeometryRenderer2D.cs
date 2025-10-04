using System.Numerics;
using System.Runtime.InteropServices;

namespace HelloGL.Engine;

public unsafe class DynamicGeometryRenderer2D : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector2 Position;
        public Vector3 Color;
    }

    private const int _initialVertexBufferSize = 1024;


    private readonly Shader _shader;

    private readonly BufferObject _vertexBufferObject;
    private readonly VertexArrayObject<Vertex> _vertexArrayObject;
    private readonly List<Vertex> _vertexBuffer = new(_initialVertexBufferSize);

    public DynamicGeometryRenderer2D(AssetManager assetManager)
    {
        _shader = assetManager.LoadShader("triangle");

        _vertexBufferObject = new BufferObject(assetManager.GL);
        _vertexBufferObject.SetSizeAndUsage(sizeof(Vertex) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
        _vertexArrayObject = new VertexArrayObject<Vertex>(assetManager.GL, _vertexBufferObject);
    }

    public void AddTriangle(Vector2 p1, Vector3 c1, Vector2 p2, Vector3 c2, Vector2 p3, Vector3 c3)
    {
        _vertexBuffer.Add(new Vertex() { Position = p1, Color = c1 });
        _vertexBuffer.Add(new Vertex() { Position = p2, Color = c2 });
        _vertexBuffer.Add(new Vertex() { Position = p3, Color = c3 });
    }

    public void AddRectangle(Vector2 center, Vector2 size, Vector3 color)
    {
        Vector2 halfSize = size * 0.5f;

        Vector2 bl = new(center.X - halfSize.X, center.Y - halfSize.Y);
        Vector2 br = new(center.X + halfSize.X, center.Y - halfSize.Y);
        Vector2 tr = new(center.X + halfSize.X, center.Y + halfSize.Y);
        Vector2 tl = new(center.X - halfSize.X, center.Y + halfSize.Y);

        _vertexBuffer.Add(new Vertex { Position = bl, Color = color });
        _vertexBuffer.Add(new Vertex { Position = br, Color = color });
        _vertexBuffer.Add(new Vertex { Position = tr, Color = color });

        _vertexBuffer.Add(new Vertex { Position = bl, Color = color });
        _vertexBuffer.Add(new Vertex { Position = tr, Color = color });
        _vertexBuffer.Add(new Vertex { Position = tl, Color = color });
    }

    public void Render(Matrix4x4 mvp)
    {
        if (_vertexBuffer.Count == 0) return;

        ReadOnlySpan<Vertex> span = CollectionsMarshal.AsSpan(_vertexBuffer);

        _vertexBufferObject.SetData(span, GL.BufferUsage.STREAM_DRAW);

        _shader.Use();
        _shader.SetUniform("uMVP", mvp);

        _vertexArrayObject.Bind();
        _vertexArrayObject.Draw(GL.PrimitiveType.TRIANGLES, 0, (uint)_vertexBuffer.Count);
        _vertexArrayObject.Unbind();

        _shader.Unuse();

        _vertexBuffer.Clear();
    }

    public void Dispose()
    {
        _vertexArrayObject.Dispose();
        _vertexBufferObject.Dispose();

        _shader.Dispose();
    }
}