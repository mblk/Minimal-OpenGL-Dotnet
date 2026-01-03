using HelloGL.Engine;
using HelloGL.Utils;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Numerics;

namespace HelloGL.Scenes.Catris;

internal class PieceDef
{
    public required int Id { get; init; }

    public required bool[,] Data { get; init; }

    public int Height => Data.GetLength(0);
    public int Width => Data.GetLength(1);

    public override string ToString()
    {
        return $"PieceDef-{Id}";
    }

    public override bool Equals(object? obj)
    {
        return obj is PieceDef other && other.Id == this.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}

internal class PiecesLoader
{
    private readonly AssetManager _assetManager;

    public PiecesLoader(AssetManager assetManager)
    {
        _assetManager = assetManager;
    }

    public IEnumerable<PieceDef> Load()
    {
        string[] lines = _assetManager.LoadDataFile("pieces.txt");
        
        var pieces = new List<PieceDef>();
        var nextId = 1;

        var buffer = new List<bool[]>();

        foreach (var line in lines)
        {
            if (String.IsNullOrWhiteSpace(line))
            {
                createPiece();    
            }
            else
            {
                bool[] blocks = line.Trim()
                    .ToCharArray()
                    .Select(c => c == 'X')
                    .ToArray();

                buffer.Add(blocks);
            }
        }

        createPiece();

        if (pieces.Count == 0)
            throw new Exception("No pieces defined");

        return pieces;

        void createPiece()
        {
            if (buffer.Count == 0)
                return;
            
            int width = buffer[0].Length;
            int height = buffer.Count;

            if (buffer.Any(x => x.Length != width))
                throw new Exception($"pieces.txt contains piece which is not a rectangle");

            bool[,] pieceData = new bool[height, width];
            Debug.Assert(pieceData.GetLength(0) == height);
            Debug.Assert(pieceData.GetLength(1) == width);

            for (int y=0; y<height; y++)
            {
                for (int x = 0; x<width; x++)
                {
                    pieceData[y, x] = buffer[y][x];
                }
            }

            buffer.Clear();

            Console.WriteLine($"New piece: {width}x{height}");

            pieces.Add(new PieceDef()
            {
                Id = nextId++,
                Data = pieceData,
            });
        }
    }
}

internal class PieceGenerator
{
    private readonly IReadOnlyList<PieceDef> _allPieces;
    private readonly Random _random = new();
    private readonly Queue<PieceDef> _bag = [];

    public IEnumerable<PieceDef> Next => _bag;

    public PieceGenerator(IEnumerable<PieceDef> pieces)
    {
        _allPieces = pieces.ToArray();
    }

    public PieceDef GetNext()
    {
        if (_bag.TryDequeue(out PieceDef? pieceDef))
            return pieceDef;

        GenerateNewBag();
        Debug.Assert(_bag.Count > 0);
        return _bag.Dequeue();
    }

    private void GenerateNewBag()
    {
        Debug.Assert(_bag.Count == 0);

        var newBag = _allPieces.ToArray();
        _random.Shuffle(newBag);

        foreach (var x in newBag)
            _bag.Enqueue(x);

        Console.WriteLine($"new bag: {String.Join(",", newBag)}");
    }
}

internal class CatrisGame
{
    public enum Rotation
    {
        Deg0, Deg90, Deg180, Deg270
    }

    public class Piece
    {
        public required PieceDef Def;
        public required Rotation Rotation;

        // Position of top left corner
        public required int X;
        public required int Y;
    }

    public struct Cell
    {
        public bool IsOccupied;
    }

    public const int Width = 13;
    public const int Height = 26;


    private readonly IReadOnlyList<PieceDef> _pieceDefs;
    private readonly PieceGenerator _pieceGenerator;
    private readonly IReadOnlyDictionary<(PieceDef, Rotation), bool[,]> _rotatedShapes;


    public Cell[,] Cells { get; } = new Cell[Height, Width];

    public Piece CurrentPiece { get; private set; } = null!;

    public IEnumerable<PieceDef> NextPieces => _pieceGenerator.Next;


    public CatrisGame(IEnumerable<PieceDef> pieceDefs)
    {
        _pieceDefs = pieceDefs.ToArray();
        _pieceGenerator = new PieceGenerator(pieceDefs);

        var rotatedShapes = new Dictionary<(PieceDef, Rotation), bool[,]>();
        foreach (var pieceDef in pieceDefs)
        {
            foreach (var rotation in Enum.GetValues<Rotation>())
            {
                rotatedShapes.Add((pieceDef, rotation), RotateShape(pieceDef.Data, rotation));
            }
        }
        _rotatedShapes = rotatedShapes.ToFrozenDictionary();

        Reset();
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

            case Rotation.Deg270:
            {
                var result = new bool[width, height];

                for (int y = 0; y < height; y++) // source index
                    for (int x = 0; x < width; x++) // source index
                        result[x, height - 1 - y] = shape[y, x];
                return result;
            }

            case Rotation.Deg90:
            {
                var result = new bool[width, height];
                for (int y = 0; y < height; y++) // source index
                    for (int x = 0; x < width; x++) // source index
                        result[width - 1 - x, y] = shape[y, x];
                return result;
            }
        }
    }

    public void Reset()
    {
        Array.Clear(Cells, 0, Cells.Length);
        NextPiece();
    }

    public void NextPiece()
    {
        PieceDef randomDef = _pieceGenerator.GetNext();
        Rotation randomRotation = Rotation.Deg0; // XXX random ?

        var shape = GetShape(randomDef, randomRotation);
        var shapeWidth = shape.GetLength(1);

        CurrentPiece = new()
        {
            Def = randomDef,
            Rotation = randomRotation,
            X = Width / 2 - shapeWidth / 2,
            Y = 0
        };
    }

    public bool[,] GetCurrentPieceRotatedShape()
    {
        return GetShape(CurrentPiece.Def, CurrentPiece.Rotation);
    }

    private bool[,] GetShape(PieceDef def, Rotation rotation)
    {
        return _rotatedShapes[(def, rotation)];
    }

    private bool SimulateMove(int dx, int dy, int rot = 0)
    {
        var newRotation = CurrentPiece.Rotation.Next(rot);

        bool[,] shape = GetShape(CurrentPiece.Def, newRotation);

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

    private int KillFullLines()
    {
        int killCount = 0;

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

            killCount++;
        }

        return killCount;
    }

    public bool MovePieceLeft()
    {
        if (SimulateMove(-1, 0))
        {
            CurrentPiece.X--;
            return true;
        }

        return false;
    }

    public bool MovePieceRight()
    {
        if (SimulateMove(1, 0))
        {
            CurrentPiece.X++;
            return true;
        }

        return false;
    }

    public enum MovePieceDownResult
    {
        Moved,
        Placed,
        GameOver,
    }

    public MovePieceDownResult MovePieceDown(bool allTheWay, out int killCount)
    {
        killCount = 0;

        if (allTheWay)
        {
            CurrentPiece.Y += SimulateLandingSpot();
        }
        else if (SimulateMove(0, 1))
        {
            CurrentPiece.Y++;
            return MovePieceDownResult.Moved;
        }

        if (!SimulateMove(0, 0))
        {
            throw new Exception("kaputt");
        }

        PlacePiece();
        killCount = KillFullLines();
        NextPiece();

        if (!SimulateMove(0, 0))
        {
            Console.WriteLine("Game over");
            Reset();
            return MovePieceDownResult.GameOver;
        }

        return MovePieceDownResult.Placed;
    }

    public bool RotatePiece(bool cw, out Vector2 move)
    {
        var oldRotation = CurrentPiece.Rotation;
        var newRotation = CurrentPiece.Rotation.Next(cw ? 1 : -1);

        var oldShape = GetShape(CurrentPiece.Def, oldRotation);
        var newShape = GetShape(CurrentPiece.Def, newRotation);

        int widthChange = newShape.GetLength(1) - oldShape.GetLength(1);
        int heightChange = newShape.GetLength(0) - oldShape.GetLength(0);

        // rotate around center of mass
        int moveX = -(widthChange / 2);
        int moveY = -(heightChange / 2);

        int newPosX = CurrentPiece.X + moveX;
        int newPosY = CurrentPiece.Y + moveY;

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
            move = default;
            return false;
        }

        CurrentPiece.X += moveX;
        CurrentPiece.Y += moveY;
        CurrentPiece.Rotation = newRotation;

        move = new Vector2(moveX, moveY);
        return true;
    }

    public int SimulateLandingSpot()
    {
        int dy = 0;

        while (SimulateMove(0, dy + 1, 0))
        {
            dy++;
        }

        return dy;
    }
}
