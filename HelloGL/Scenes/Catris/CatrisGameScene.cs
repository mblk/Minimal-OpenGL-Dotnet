using HelloGL.Engine;
using HelloGL.Platforms;
using HelloGL.Utils;
using System.Numerics;

namespace HelloGL.Scenes.Catris;

internal class CatrisGameScene : Scene
{
    private DynamicGeometryRenderer2D _renderer = null!;

    private readonly CatrisGame _game = new();

    private float _downTick = 0;
    private bool _downButtonNeedsRelease = false;
    private float _leftTick = 0;
    private float _rightTick = 0;

    private int _highscore = 0;
    private int _score = 0;
    private int _kills = 0;
    private int _speed = 0;

    private const int CellSize = 30;

    private const int PlaceScoreWithoutKill = 5;
    private const int PlaceScoreWithKill = 100;

    private const float LeftRightRate = 10.0f; // Hz
    private const float MaxTickLeftRight = 1.0f / LeftRightRate;

    private const float DownRateSlow = 1.0f;
    private const float DownRateFast = 15.0f; // Hz
    private const float DownRateSuperFast = 30.0f; // Hz

    public CatrisGameScene(SceneContext context)
        : base(context)
    {
        ResetScore();
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
            context.SceneController.RequestSceneChange("menu");
        }

#if DEBUG
        if (kb.WasPressed(Key.D1))
        {
            IncreaseKills(-5);
        }
        if (kb.WasPressed(Key.D2))
        {
            IncreaseKills(5);
        }
#endif

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
            if (_leftTick > MaxTickLeftRight)
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
            if (_rightTick > MaxTickLeftRight)
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

        var downRate = GetDownRateFromSpeed(_speed);

        if (kb.Get(Key.S))
        {
            if (!_downButtonNeedsRelease && downRate < DownRateSuperFast)
            {
                downRate = DownRateSuperFast;
            }
        }
        else
        {
            _downButtonNeedsRelease = false;
        }

        var maxDownTick = 1.0f / downRate;

        _downTick += dt;
        if (_downTick >= maxDownTick)
        {
            _downTick = 0.0f;

            var moveResult = _game.MovePieceDown(out int killCount);

            switch (moveResult)
            {
                case CatrisGame.MovePieceDownResult.Moved:
                    break;

                case CatrisGame.MovePieceDownResult.Placed:
                    if (killCount > 0)
                    {
                        IncreaseScore(GetRowKillScore(killCount));
                        IncreaseKills(killCount);
                    }
                    else
                    {
                        IncreaseScore(PlaceScoreWithoutKill);
                    }
                    break;

                case CatrisGame.MovePieceDownResult.GameOver:
                    ResetScore();
                    break;

                default: throw new Exception("unknown result");
            }

            _downButtonNeedsRelease = moveResult != CatrisGame.MovePieceDownResult.Moved;
        }
    }

    private void ResetScore()
    {
        _score = 0;
        _kills = 0;
        _speed = 0;
    }

    private void IncreaseScore(int increment)
    {
        _score += increment;

        if (_score > _highscore)
            _highscore = _score;
    }

    private void IncreaseKills(int increment)
    {
        _kills += increment;
        if (_kills < 0) _kills = 0;

        _speed = _kills / 10;
    }

    private static int GetRowKillScore(int killCount)
    {
        int s = (killCount - 1) * 2;

        if (s < 0) s = 0;
        if (s > 10) s = 10;

        return (PlaceScoreWithKill << s);

        // 1: 100
        // 2: 400
        // 3: 1600
        // 4: 6400
    }

    private static float GetDownRateFromSpeed(int speed)
    {
        return MathUtils.Lerp(DownRateSlow, DownRateFast, (float)speed / 10.0f);
    }

    public override void Render(RenderContext context)
    {
        var (width, height) = context.WindowSize;

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;
        var mvp = mModel * mView * mOrthoProj;

        RenderBorder();
        RenderBoard();
        RenderCurrentPiece();
        RenderUI(height);

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
            250 + y * CellSize
        );
    }

    private void RenderUI(int height)
    {
        _renderer.AddText(new Vector2(20, 0), 1.0f, $"Score: {_score}");
        _renderer.AddText(new Vector2(20, 64), 0.666f, $"Highscore: {_highscore}");
        _renderer.AddText(new Vector2(20, 96), 0.666f, $"Speed: {_speed}");
        _renderer.AddText(new Vector2(20, 128), 0.666f, $"Kills: {_kills}");
        _renderer.AddText(new Vector2(50, height - 50), 0.666f, $"Move: A/S/D        Rotate: Q/W/E");
    }
}