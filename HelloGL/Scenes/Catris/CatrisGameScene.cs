using HelloGL.Engine;
using HelloGL.Platforms;
using HelloGL.Platforms.LinuxX11.Native;
using HelloGL.Utils;
using System.Numerics;

namespace HelloGL.Scenes.Catris;

internal class CatrisGameScene : Scene
{
    private DynamicGeometryRenderer2D _renderer = null!;

    private IReadOnlyList<PieceDef> _pieces = null!;

    private CatrisGame _game = null!;



    // TODO simplify state
    private float _downProgress = 0;
    private bool _downButtonNeedsRelease = false;

    private float _sideBlocked = 0;
    private float _sideEaseT = 0;

    private float _rotateEaseT = 0; // -1..0..+1

    private (int,int) _prevShapeSize;
    
    private Vector2 _rotateMove = default; // How much the piece was moved when it was rotated

    private int _landingSpotDy = 0;

    private int _highscore = 0;
    private int _score = 0;
    private int _kills = 0;
    private int _speed = 0;




    private const float WorldWidth = CatrisGame.Height + 5f;
    private const float WorldHeight = CatrisGame.Height + 5f;

    private const int PlaceScoreWithoutKill = 5;
    private const int PlaceScoreWithKill = 100;

    private const float LeftRightRate = 10.0f; // Hz
    private const float MaxTickLeftRight = 1.0f / LeftRightRate;

    private const float DownRateSlow = 1.0f; // Hz
    private const float DownRateFast = 15.0f; // Hz
    private const float DownRateSuperFast = 25.0f; // Hz

    private const float RotateCooldown = 0.15f; // s



    public CatrisGameScene(SceneContext context)
        : base(context)
    {
        ResetSceneState();
    }

    public override void Load()
    {
        _renderer = new DynamicGeometryRenderer2D(AssetManager, [ "block" ]);

        _pieces = new PiecesLoader(AssetManager).Load().ToArray();

        _game = new CatrisGame(_pieces);
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

        if (_rotateEaseT != 0f)
        {
            if (_rotateEaseT < 0f)
            {
                _rotateEaseT += dt * (1f / RotateCooldown);
                if (_rotateEaseT > 0f)
                    _rotateEaseT = 0f;
            }
            else
            {
                _rotateEaseT -= dt * (1f / RotateCooldown);
                if (_rotateEaseT < 0f)
                    _rotateEaseT = 0f;
            }
        }

        if (_rotateEaseT == 0f)
        {
            bool ccw = kb.Get(Key.E);
            bool cw = kb.Get(Key.Q);

            bool[,] prevShape = _game.GetCurrentPieceRotatedShape();
            
            if (cw != ccw && _game.RotatePiece(cw: cw, out Vector2 rotMove))
            {
                _rotateEaseT = cw ? 1f : -1f;
                _rotateMove = rotMove;
                _prevShapeSize = (prevShape.GetLength(1), prevShape.GetLength(0));
            }
        }

        //
        // left / right
        //

        if (_sideEaseT != 0f) // TODO maybe combine blocked+ease ?
        {
            if (_sideEaseT < 0f)
            {
                _sideEaseT += LeftRightRate * dt;
                if (_sideEaseT >= 0f)
                    _sideEaseT = 0f;
            }
            else
            {
                _sideEaseT -= LeftRightRate * dt;
                if (_sideEaseT <= 0f)
                    _sideEaseT = 0f;
            }
        }

        if (_sideBlocked == 0f)
        {
            bool left = kb.Get(Key.A);
            bool right = kb.Get(Key.D);

            if (left && !right && _game.MovePieceLeft())
            {
                _sideEaseT = 1.0f;
                _sideBlocked = MaxTickLeftRight;
            }
            if (!left && right && _game.MovePieceRight())
            {
                _sideEaseT = -1.0f;
                _sideBlocked = MaxTickLeftRight;
            }    
        }
        else
        {
            _sideBlocked -= dt;
            if (_sideBlocked < 0f)
                _sideBlocked = 0f;
        }

        //
        // down
        //

        if (kb.WasPressed(Key.Space))
        {
            MovePieceDown(true);
            _downProgress = 0f;
        }
        else
        {
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

            _downProgress += downRate * dt;

            if (_downProgress >= 1f)
            {
                _downProgress -= 1f;
                MovePieceDown(false);
            }
        }

        // TODO call only if game state changed
        _landingSpotDy = _game.SimulateLandingSpot();
    }

    private void MovePieceDown(bool allTheWay)
    {
        var moveResult = _game.MovePieceDown(allTheWay, out int killCount);

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
                ResetSceneState();
                break;

            default: throw new Exception("unknown result");
        }

        _downButtonNeedsRelease = moveResult != CatrisGame.MovePieceDownResult.Moved;
    }

    private void ResetSceneState()
    {
        _downProgress = 0;
        _downButtonNeedsRelease = false;
        _sideBlocked = 0;
        _sideEaseT = 0;
        _rotateEaseT = 0;
        _prevShapeSize = default;
        _rotateMove = default;
        _landingSpotDy = 0;

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

        return PlaceScoreWithKill << s;

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

        RenderBorder();
        RenderBoard();
        RenderLandingSpot();
        RenderCurrentPiece();
        RenderNextPieces();
        RenderUI();

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

    private void RenderLandingSpot()
    {
        bool[,] shape = _game.GetCurrentPieceRotatedShape();
        int shapeHeight = shape.GetLength(0);
        int shapeWidth = shape.GetLength(1);
        
        Vector3 color = new Vector3(0.2f, 0.1f, 0.1f);

        for (int y = 0; y < shapeHeight; y++)
        {
            for (int x = 0; x < shapeWidth; x++)
            {
                if (shape[y, x])
                {
                    Vector2 cellPos = new Vector2(
                        _game.CurrentPiece.X + x,
                        _game.CurrentPiece.Y + y + _landingSpotDy);

                    if (shape[y, x])
                    {
                        Vector2 worldPos = GetWorldPosition(cellPos);
                        Vector2 worldSize = new Vector2(1f, 1f);

                        _renderer.AddRectangle(worldPos, worldSize, color);
                    }
                }
            }
        }
    }

    private void RenderCurrentPiece()
    {
        bool[,] shape = _game.GetCurrentPieceRotatedShape();
        int shapeHeight = shape.GetLength(0);
        int shapeWidth = shape.GetLength(1);
        int prevWidth = _prevShapeSize.Item1;
        int prevHeight = _prevShapeSize.Item2;

        float angle = _rotateEaseT * MathF.PI * 0.5f;

        Vector3 color = new Vector3(1f, 0f, 0f);

        // all positions are in map coordinates
        Vector2 rotCenter = new Vector2(
            _game.CurrentPiece.X - 0.5f + shapeWidth * 0.5f,
            _game.CurrentPiece.Y - 0.5f + shapeHeight * 0.5f);

        Vector2 prevCenter = new Vector2(prevWidth * 0.5f, prevHeight * 0.5f);
        Vector2 currCenter = new Vector2(shapeWidth * 0.5f, shapeHeight * 0.5f);
        Vector2 centerDiff = currCenter - prevCenter + _rotateMove;

        for (int y = 0; y < shapeHeight; y++)
        {
            for (int x = 0; x < shapeWidth; x++)
            {
                if (shape[y, x])
                {
                    Vector2 cellPos = new Vector2(
                        _game.CurrentPiece.X + x,
                        _game.CurrentPiece.Y + y);

                    cellPos = cellPos.RotateAround(rotCenter, angle);

                    cellPos -= centerDiff * MathF.Abs(_rotateEaseT);

                    cellPos.X += Ease.Mirror(_sideEaseT, Ease.InOutQuad);
                    cellPos.Y += Ease.InOutExpo(_downProgress) - 1f;

                    if (shape[y, x])
                    {
                        Vector2 worldPos = GetWorldPosition(cellPos);
                        Vector2 worldSize = new Vector2(1f, 1f);

                        _renderer.AddRotatedRectangle(worldPos, worldSize, angle, color * 0.8f);
                        _renderer.AddRotatedRectangle(worldPos, worldSize * 0.5f, angle, color);
                    }
                }
            }
        }
    }

    private void RenderBlock(int x, int y, Vector3 color)
    {
        Vector2 cellPos = new Vector2(x, y);
        Vector2 worldPos = GetWorldPosition(cellPos);
        Vector2 worldSize = new Vector2(1f, 1f);

        _renderer.AddRectangle(worldPos, worldSize, color * 0.8f);
        _renderer.AddRectangle(worldPos, worldSize * 0.5f, color);
    }

    private static Vector2 GetWorldPosition(Vector2 p)
    {
        const float xoff = WorldWidth * 0.5f - CatrisGame.Width * 0.5f + 0.5f;
        const float yoff = WorldHeight * 0.5f - CatrisGame.Height * 0.5f + 0.5f;

        return p + new Vector2(xoff, yoff);
    }

    private void RenderNextPieces()
    {
        const float scale = 1f / 64f;

        _renderer.AddText(new Vector2(WorldWidth - 6, 1), scale, "Next:");

        float y = 3f;

        foreach (var next in _game.NextPieces)
        {
            var p = new Vector2(WorldWidth - 5, y);

            y += RenderNextPiece(p, next);
        }
    }

    private float RenderNextPiece(Vector2 position, PieceDef pieceDef)
    {
        var s = new Vector2(0.5f, 0.5f);
        var c = new Vector3(0.5f, 0.5f, 0.5f);

        for (int y=0; y<pieceDef.Height; y++)
        {
            for (int x=0; x<pieceDef.Width; x++)
            {
                if (!pieceDef.Data[y, x])
                    continue;

                var p = position + new Vector2(x * s.X, y * s.Y);
                
                _renderer.AddRectangle(p, s, c);
            }
        }

        return (pieceDef.Height + 1) * s.Y;
    }

    private void RenderUI()
    {
        const float scale = 1f / 64f;

        _renderer.AddText(new Vector2(0, 0), scale, $"Score: {_score}");
        _renderer.AddText(new Vector2(0, 1), scale, $"Highscore: {_highscore}");
        _renderer.AddText(new Vector2(0, 2), scale, $"Speed: {_speed}");
        _renderer.AddText(new Vector2(0, 3), scale, $"Kills: {_kills}");
        _renderer.AddText(new Vector2(0, WorldHeight - 3), scale, $"Move: A/S/D");
        _renderer.AddText(new Vector2(0, WorldHeight - 2), scale, $"Place: Space");
        _renderer.AddText(new Vector2(0, WorldHeight - 1), scale, $"Rotate: Q/E");
    }
}