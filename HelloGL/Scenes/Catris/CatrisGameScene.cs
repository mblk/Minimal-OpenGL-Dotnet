using HelloGL.Engine;
using HelloGL.Platforms;
using System.Numerics;

namespace HelloGL.Scenes.Catris;

internal class CatrisGameScene : Scene
{
    private DynamicGeometryRenderer2D _renderer = null!;

    private readonly CatrisGame _game = new();

    private float _downTick = 0;
    private float _leftTick = 0;
    private float _rightTick = 0;

    private const int CellSize = 30;

    public CatrisGameScene(AssetManager assetManager)
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

        if (kb.WasPressed(Key.Q))
        {
            _game.RotatePiece(false);
        }

        if (kb.WasPressed(Key.E) || kb.WasPressed(Key.W))
        {
            _game.RotatePiece(true);
        }

        //
        // left / right
        //

        const float maxTickLeftRight = 0.2f;

        if (kb.WasPressed(Key.A))
        {
            _game.MovePieceLeft();
        }

        if (kb.WasPressed(Key.D))
        {
            _game.MovePieceRight();
        }

        if (kb.Get(Key.A))
        {
            _leftTick += dt;
            if (_leftTick > maxTickLeftRight)
            {
                _leftTick = 0;
                _game.MovePieceLeft();
            }
        }
        else
        {
            _leftTick = 0;
        }

        if (kb.Get(Key.D))
        {
            _rightTick += dt;
            if (_rightTick > maxTickLeftRight)
            {
                _rightTick = 0;
                _game.MovePieceRight();
            }
        }
        else
        {
            _rightTick = 0;
        }

        //
        // down
        //

        var maxDownTick = 1.0f;

        if (kb.Get(Key.S))
        {
            maxDownTick = 0.04f;
        }

        _downTick += dt;
        if (_downTick >= maxDownTick)
        {
            _downTick = 0.0f;
            _game.MovePieceDown();
        }
    }

    public override void Render(float dt, (int, int) windowSize)
    {
        var (width, height) = windowSize;

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;
        var mvp = mModel * mView * mOrthoProj;

        RenderBorder();
        RenderBoard();
        RenderCurrentPiece();

        _renderer.Render(mvp);
    }

    private void RenderBorder()
    {
        var color = new Vector3(0.3f);

        for (int y = 0; y < CatrisGame.Height; y++)
        {
            RenderBlock(-1, y, color);
            RenderBlock(CatrisGame.Width, y, color);
        }

        for (int x = -1; x <= CatrisGame.Width; x++)
        {
            RenderBlock(x, CatrisGame.Height, color);
        }
    }

    private void RenderBoard()
    {
        for (int y = 0; y < CatrisGame.Height; y++)
        {
            for (int x = 0; x < CatrisGame.Width; x++)
            {
                if (_game.Cells[y, x].IsOccupied)
                {
                    RenderBlock(x, y, new Vector3(0.5f, 0.5f, 0.5f));
                }
            }
        }
    }

    private void RenderCurrentPiece()
    {
        bool[,] shape = _game.GetCurrentPieceRotatedShape();
        int shapeHeight = shape.GetLength(0);
        int shapeWidth = shape.GetLength(1);

        for (int y = 0; y < shapeHeight; y++)
        {
            for (int x = 0; x < shapeWidth; x++)
            {
                if (shape[y, x])
                {
                    int boardX = _game.CurrentPiece.X + x;
                    int boardY = _game.CurrentPiece.Y + y;

                    RenderBlock(boardX, boardY, new Vector3(1.0f, 0.0f, 0.0f));
                }
            }
        }
    }

    private void RenderBlock(int x, int y, Vector3 color)
    {
        Vector2 cellPos = GetCellPosition(x, y);
        Vector2 cellSize = new Vector2(CellSize, CellSize);

        _renderer.AddRectangle(cellPos, cellSize, color * 0.8f);
        _renderer.AddRectangle(cellPos, cellSize * 0.5f, color);
    }

    private static Vector2 GetCellPosition(int x, int y)
    {
        return new Vector2(
            150 + x * CellSize,
            150 + y * CellSize
        );
    }
}