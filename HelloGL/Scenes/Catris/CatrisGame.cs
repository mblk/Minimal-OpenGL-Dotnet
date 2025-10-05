using HelloGL.Utils;
using System.Collections.Frozen;
using System.Diagnostics;

namespace HelloGL.Scenes.Catris;

internal class CatrisGame // Quick and dirty, should clean this up
{
    public enum PieceType
    {
        I, O, T, S, Z, J, L
    }

    public enum Rotation
    {
        Deg0, Deg90, Deg180, Deg270
    }

    public class Piece
    {
        public PieceType Type;
        public Rotation Rotation;

        // Position of top left corner
        public int X;
        public int Y;
    }

    private static readonly IReadOnlyDictionary<PieceType, bool[,]> _pieceShapes = new Dictionary<PieceType, bool[,]>()
    {
        { PieceType.I, new bool[,] { { true, true, true, true } } },
        { PieceType.O, new bool[,] { { true, true }, { true, true } } },
        { PieceType.T, new bool[,] { { false, true, false }, { true, true, true } } },
        { PieceType.S, new bool[,] { { false, true, true }, { true, true, false } } },
        { PieceType.Z, new bool[,] { { true, true, false }, { false, true, true } } },
        { PieceType.J, new bool[,] { { true, false, false }, { true, true, true } } },
        { PieceType.L, new bool[,] { { false, false, true }, { true, true, true } } }
    }.ToFrozenDictionary();

    private static readonly IReadOnlyDictionary<(PieceType, Rotation), bool[,]> _rotatedShapes;

    static CatrisGame()
    {
        var rotatedShapes = new Dictionary<(PieceType, Rotation), bool[,]>();

        foreach (var (type, shape) in _pieceShapes)
        {
            foreach (var rotation in Enum.GetValues<Rotation>())
            {
                rotatedShapes.Add((type, rotation), RotateShape(shape, rotation));
            }
        }

        _rotatedShapes = rotatedShapes.ToFrozenDictionary();
    }

    private static bool[,] RotateShape(bool[,] shape, Rotation rotation)
    {
        var height = shape.GetLength(0);
        var width = shape.GetLength(1);

        switch (rotation)
        {
            default:
            case Rotation.Deg0:
                return shape;

            case Rotation.Deg180:
            {
                var result = new bool[height, width];
                for (int y = 0; y < height; y++) // source index
                    for (int x = 0; x < width; x++) // source index
                        result[height - 1 - y, width - 1 - x] = shape[y, x];
                return result;
            }

            case Rotation.Deg90:
            {
                var result = new bool[width, height];

                for (int y = 0; y < height; y++) // source index
                    for (int x = 0; x < width; x++) // source index
                        result[x, height - 1 - y] = shape[y, x];
                return result;
            }

            case Rotation.Deg270:
            {
                var result = new bool[width, height];
                for (int y = 0; y < height; y++) // source index
                    for (int x = 0; x < width; x++) // source index
                        result[width - 1 - x, y] = shape[y, x];
                return result;
            }
        }
    }



    public struct Cell
    {
        public bool IsOccupied;
    }

    public const int Width = 10;
    public const int Height = 20;

    public readonly Cell[,] Cells = new Cell[Height, Width];


    public Piece CurrentPiece = null!;

    private readonly Random _random = new();

    public CatrisGame()
    {
        Reset();
    }

    public void Reset()
    {
        Array.Clear(Cells, 0, Cells.Length);
        NextPiece();
    }

    public void NextPiece()
    {
        var allTypes = Enum.GetValues<PieceType>();
        var randomIndex = _random.Next() % allTypes.Length;
        var randomType = allTypes[randomIndex];

        CurrentPiece = new()
        {
            Type = randomType,
            Rotation = Rotation.Deg0,
            X = Width / 2,
            Y = 0
        };
    }

    public bool[,] GetCurrentPieceRotatedShape()
    {
        return _rotatedShapes[(CurrentPiece.Type, CurrentPiece.Rotation)];
    }

    private static bool[,] GetShape(PieceType type, Rotation rotation)
    {
        return _rotatedShapes[(type, rotation)];
    }

    private bool SimulateMove(int dx, int dy, int rot = 0)
    {
        var newRotation = CurrentPiece.Rotation.Next(rot);

        bool[,] shape = GetShape(CurrentPiece.Type, newRotation);

        var newPosX = CurrentPiece.X + dx;
        var newPosY = CurrentPiece.Y + dy;

        var isValidMove = true;

        for (var y = 0; y < shape.GetLength(0); y++)
        {
            for (var x = 0; x < shape.GetLength(1); x++)
            {
                int blockX = newPosX + x;
                int blockY = newPosY + y;

                if (shape[y, x])
                {
                    if (!IsCellFree(blockX, blockY))
                    {
                        isValidMove = false;
                    }
                }
            }
        }

        return isValidMove;
    }

    private bool IsCellFree(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        if (Cells[y, x].IsOccupied)
            return false;

        return true;
    }

    private void PlacePiece()
    {
        bool[,] shape = GetCurrentPieceRotatedShape();

        for (var y = 0; y < shape.GetLength(0); y++)
        {
            for (var x = 0; x < shape.GetLength(1); x++)
            {
                int blockX = CurrentPiece.X + x;
                int blockY = CurrentPiece.Y + y;

                if (shape[y, x])
                {
                    Debug.Assert(Cells[blockY, blockX].IsOccupied == false);
                    Cells[blockY, blockX].IsOccupied = true;
                }
            }
        }
    }

    private void KillFullLines()
    {
        for (int y = Height - 1; y >= 0; )
        {
            bool lineIsFull = true;
            for (int x = 0; x < Width; x++)
            {
                if (!Cells[y, x].IsOccupied)
                {
                    lineIsFull = false;
                    break;
                }
            }

            if (!lineIsFull)
            {
                y--;
                continue;
            }

            // fall down one step

            for (int y2 = y; y2 > 0; y2--)
            {
                for (int x = 0; x < Width; x++)
                {
                    Cells[y2, x] = Cells[y2 - 1, x];
                }
            }

            // clear top line

            for (int x = 0; x < Width; x++)
            {
                Cells[0, x].IsOccupied = false;
            }
        }
    }

    public void MovePieceLeft()
    {
        if (SimulateMove(-1, 0))
        {
            CurrentPiece.X--;
        }
    }

    public void MovePieceRight()
    {
        if (SimulateMove(1, 0))
        {
            CurrentPiece.X++;
        }
    }

    public bool MovePieceDown()
    {
        if (SimulateMove(0, 1))
        {
            CurrentPiece.Y++;

            return false;
        }
        else
        {
            PlacePiece();
            KillFullLines();
            NextPiece();
            if (!SimulateMove(0, 0))
            {
                Console.WriteLine("Game over");
                Reset();
            }

            return true;
        }
    }

    public void RotatePiece(bool cw)
    {
        var oldRotation = CurrentPiece.Rotation;
        var newRotation = CurrentPiece.Rotation.Next(cw ? 1 : -1);

        var oldShape = GetShape(CurrentPiece.Type, oldRotation);
        var newShape = GetShape(CurrentPiece.Type, newRotation);

        var widthChange = newShape.GetLength(1) - oldShape.GetLength(1);
        var heightChange = newShape.GetLength(0) - oldShape.GetLength(0);

        // rotate around center of mass
        var moveX = -(widthChange / 2);
        var moveY = -(heightChange / 2);

        var newPosX = CurrentPiece.X + moveX;
        var newPosY = CurrentPiece.Y + moveY;

        // check bounds
        int newShapeHeight = newShape.GetLength(0);
        int newShapeWidth = newShape.GetLength(1);

        if (newPosX < 0) moveX += -newPosX;
        if (newPosY < 0) moveY += -newPosY;
        if (newPosX + newShapeWidth > Width) moveX -= newPosX + newShapeWidth - Width;
        if (newPosY + newShapeHeight > Height) moveY -= newPosY + newShapeHeight - Height;

        if (!SimulateMove(moveX, moveY, cw ? 1 : -1))
        {
            Console.WriteLine($"Rotation blocked");
            return;
        }

        CurrentPiece.X += moveX;
        CurrentPiece.Y += moveY;
        CurrentPiece.Rotation = newRotation;
    }
}
