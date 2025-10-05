using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace HelloGL.Engine;

public class TextureLoader : AssetLoader<Texture>
{
    public TextureLoader(IAssetReader assetReader, GL gl)
        : base(assetReader, gl)
    {
    }

    public override AssetLoadResult<Texture> Load(string name)
    {
        var path = Reader.GetAssetPath(AssetType.Texture, $"{name}.png");
        byte[] data = Reader.ReadFileAsBytes(path);

        var imageLoader = new PngImageLoader();
        var imageLoadResult = imageLoader.Load(data);

        var texture = new Texture(GL, imageLoadResult.Width, imageLoadResult.Height, imageLoadResult.Data);

        var sourceFiles = new HashSet<string>() { path };

        return new AssetLoadResult<Texture>(texture, sourceFiles);
    }

    public override AssetLoadResult<Texture> Reload(Texture asset)
    {
        throw new NotImplementedException();
    }
}

public unsafe class Texture : Asset, IDisposable
{
    private readonly GL _gl;

    private readonly uint _id;

    public Texture(GL gl, int width, int height, ReadOnlySpan<byte> data)
    {
        _gl = gl;

        uint texId = 0;
        _gl.GenTextures(1, &texId);
        _id = texId;

        _gl.BindTexture(GL.TextureTarget.TEXTURE_2D, texId);

        _gl.TexParameterI(GL.TextureTarget.TEXTURE_2D, GL.TextureParameterName.TEXTURE_MIN_FILTER, (int)GL.TextureMinFilter.LINEAR);
        _gl.TexParameterI(GL.TextureTarget.TEXTURE_2D, GL.TextureParameterName.TEXTURE_MAG_FILTER, (int)GL.TextureMinFilter.LINEAR);
        _gl.TexParameterI(GL.TextureTarget.TEXTURE_2D, GL.TextureParameterName.TEXTURE_WRAP_S, (int)GL.TextureWrapMode.CLAMP_TO_EDGE);
        _gl.TexParameterI(GL.TextureTarget.TEXTURE_2D, GL.TextureParameterName.TEXTURE_WRAP_T, (int)GL.TextureWrapMode.CLAMP_TO_EDGE);

        fixed (void* pData = data)
        {
            _gl.TexImage2D(GL.TextureTarget.TEXTURE_2D, 0, GL.InternalFormat.RGBA8, width, height, 0, GL.PixelFormat.RGBA, GL.PixelType.UNSIGNED_BYTE, pData);
        }

        // generate mipmaps maybe
        //_gl.GenerateMipmap(GL.TextureTarget.TEXTURE_2D);

        _gl.BindTexture(GL.TextureTarget.TEXTURE_2D, 0);
    }

    public void Dispose()
    {
        uint texId = _id;
        _gl.DeleteTextures(1, &texId);
    }

    public void Bind(int unit)
    {
        _gl.ActiveTexture(GL.TextureUnit.TEXTURE0 + (uint)unit);
        _gl.BindTexture(GL.TextureTarget.TEXTURE_2D, _id);
    }

    public void Unbind(int unit)
    {
        _gl.ActiveTexture(GL.TextureUnit.TEXTURE0 + (uint)unit);
        _gl.BindTexture(GL.TextureTarget.TEXTURE_2D, 0);
    }
}

public abstract class ImageLoader
{
    public record ImageLoadResult(int Width, int Height, byte[] Data);

    public abstract ImageLoadResult Load(ReadOnlySpan<byte> data);
}

public class PngImageLoader : ImageLoader
{
    //
    // spec: https://www.w3.org/TR/png-3/
    //

    private static readonly byte[] _signature = [ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A ];

    private static readonly int _minimumChunkSize = 12;

    private static readonly int _minimumValidLength = _signature.Length + 3 * _minimumChunkSize;

    private readonly ref struct Chunk
    {
        public readonly uint Length;
        public readonly string Type;
        public readonly ReadOnlySpan<byte> Data;
        public readonly uint CRC;

        public Chunk(uint length, string type, ReadOnlySpan<byte> data, uint crc)
        {
            Length = length;
            Type = type;
            Data = data;
            CRC = crc;
        }
    }

    private class Header
    {
        public uint Width;
        public uint Height;
        public byte BitDepth;
        public byte ColorType;
        public byte Compression;
        public byte Filter;
        public byte Interlace;
    }

    public PngImageLoader()
    {
    }

    public override ImageLoadResult Load(ReadOnlySpan<byte> data)
    {
        if (data.Length < _minimumValidLength)
            throw new Exception($"Data too short");

        if (!data[0.._signature.Length].SequenceEqual(_signature))
            throw new Exception($"Invalid signature");

        ReadOnlySpan<byte> next = data[_signature.Length..];

        Header? header = null;
        bool foundEnd = false;

        List<byte> encodedImageData = new List<byte>(data.Length);

        while (next.Length >= _minimumChunkSize)
        {
            Chunk chunk = ReadNextChunk(ref next);

            switch (chunk.Type)
            {
                case "IHDR":
                {
                    if (header is not null) throw new Exception($"Found multiple headers");

                    header = new Header()
                    {
                        Width = BinaryPrimitives.ReadUInt32BigEndian(chunk.Data[0..4]),
                        Height = BinaryPrimitives.ReadUInt32BigEndian(chunk.Data[4..8]),
                        BitDepth = chunk.Data[8],
                        ColorType = chunk.Data[9],
                        Compression = chunk.Data[10],
                        Filter = chunk.Data[11],
                        Interlace = chunk.Data[12],
                    };
                    break;
                }

                case "IDAT":
                {
                    encodedImageData.AddRange(chunk.Data);
                    break;
                }

                case "IEND":
                {
                    if (foundEnd) throw new Exception($"Found multiple ends");

                    foundEnd = true;
                    break;
                }

                default:
                {
                    Console.WriteLine($"Ignoring Chunk: {chunk.Type}");
                    break;
                }
            }
        }

        if (header is null) throw new Exception($"Header not found");

        Console.WriteLine($"Header:");
        Console.WriteLine($"  Size:        {header.Width}x{header.Height}");
        Console.WriteLine($"  BitDepth:    {header.BitDepth}");
        Console.WriteLine($"  ColorType:   {header.ColorType}");
        Console.WriteLine($"  Compression: {header.Compression}");
        Console.WriteLine($"  Filter:      {header.Filter}");
        Console.WriteLine($"  Interlace:   {header.Interlace}");

        if (header.BitDepth != 8) throw new Exception($"Unsupported bit depth {header.BitDepth}, must be 8");
        if (header.ColorType != 6) throw new Exception($"Unsupported color type {header.ColorType}, must be 6 (RGBA)");
        if (header.Compression != 0) throw new Exception($"Unsupported compression method {header.Compression}, must be 0");
        if (header.Filter != 0) throw new Exception($"Unsupported filter method {header.Filter}, must be 0");
        if (header.Interlace != 0) throw new Exception($"Unsupported interlace method {header.Interlace}, must be 0");

        if (!foundEnd) throw new Exception($"Missing IEND chunk");

        // -----------------------------------

        byte[] decompressedData = DecompressData(CollectionsMarshal.AsSpan(encodedImageData));

        Console.WriteLine($"Decompressed image data from {encodedImageData.Count} to {decompressedData.Length} bytes");

        // -----------------------------------

        Span<byte> finalData = new byte[header.Width * header.Height * 4];

        ReconstructImage(decompressedData, finalData, (int)header.Width, (int)header.Height);

        // -----------------------------------

        return new ImageLoadResult((int)header.Width, (int)header.Height, finalData.ToArray());
    }

    private unsafe static byte[] DecompressData(ReadOnlySpan<byte> compressedData)
    {
        fixed (byte* pData = compressedData)
        {
            using var inputStream = new UnmanagedMemoryStream(pData, compressedData.Length);

            using var zlibStream = new ZLibStream(inputStream, CompressionMode.Decompress, false);

            using var outputStream = new MemoryStream();
            zlibStream.CopyTo(outputStream);

            return outputStream.ToArray();
        }
    }

    private static void ReconstructImage(ReadOnlySpan<byte> filteredDataFull, Span<byte> reconDataFull, int width, int height)
    {
        int bytesPerLine = width * 4;
        int bytesPerLineWithFilter = bytesPerLine + 1;

        ReadOnlySpan<byte> prevReconData = new byte[width * 4];

        for (int line = 0; line < height; line++)
        {
            ReadOnlySpan<byte> fullLineData = filteredDataFull[(line * bytesPerLineWithFilter)..((line + 1) * bytesPerLineWithFilter)];
            ReadOnlySpan<byte> lineData = fullLineData[1..];
            Span<byte> reconData = reconDataFull[(line * bytesPerLine)..((line + 1) * bytesPerLine)];

            byte filterType = fullLineData[0]; // https://www.w3.org/TR/png-3/#9Filter-types

            for (int i = 0; i < width * 4; i++)
            {
                byte x = lineData[i]; // Filt(x)
                byte b = prevReconData[i]; // Recon(b)
                byte a = (i >= 4) ? reconData[i - 4] : (byte)0; // Recon(a)
                byte c = (i >= 4) ? prevReconData[i - 4] : (byte)0; // Recon(c)

                reconData[i] = (byte)(filterType switch
                {
                    0 => x,
                    1 => x + a,
                    2 => x + b,
                    3 => x + ((a + b) >> 1),
                    4 => x + Paeth(a, b, c),
                    _ => throw new Exception($"Filter type {filterType} not supported"),
                });
            }
            prevReconData = reconData;
        }
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    private static Chunk ReadNextChunk(ref ReadOnlySpan<byte> next)
    {
        uint length = ReadUint32(ref next);
        string type = ReadString(ref next, 4);
        ReadOnlySpan<byte> chunkData = ReadBytes(ref next, (int)length);
        uint crc = ReadUint32(ref next);

        return new Chunk(length, type, chunkData, crc);
    }

    private static uint ReadUint32(ref ReadOnlySpan<byte> next)
    {
        // Big endian first
        uint value = ((uint)next[0] << 24) | ((uint)next[1] << 16) | ((uint)next[2] << 8) | (uint)next[3];
        next = next[4..];
        return value;
    }

    private static byte ReadByte(ref ReadOnlySpan<byte> next)
    {
        byte value = next[0];
        next = next[1..];
        return value;
    }

    private static ReadOnlySpan<byte> ReadBytes(ref ReadOnlySpan<byte> input, int length)
    {
        ReadOnlySpan<byte> result = input[0..length];
        input = input[length..];
        return result;
    }

    private static string ReadString(ref ReadOnlySpan<byte> next, int length)
    {
        string value = "";
        for (int i=0; i<length; i++)
            value += (char)next[i];
        next = next[length..];
        return value;
    }


}
