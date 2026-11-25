using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
//using System.Security.Cryptography.X509Certificates;

namespace WowRgbBridge;

public sealed class ScreenPixelReader
{
    private const int ScanWidth = 251;
    private const int ScanHeight = 251;
    private const int SquareCount = 17;
    private const int MaximumSquareSize = 10;
    private const uint DibRgbColors = 0;
    private const uint BiRgb = 0;
    private const uint SourceCopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000;

    private readonly RgbColor[] _markerColors;
    private readonly int _colorTolerance;

    public ScreenPixelReader(IReadOnlyList<string> markerColors, int colorTolerance)
    {
        ArgumentNullException.ThrowIfNull(markerColors);
        if (markerColors.Count != 4)
        {
            throw new ArgumentException("Exactly four marker colors are required.", nameof(markerColors));
        }

        if (colorTolerance is < 0 or > 127)
        {
            throw new ArgumentOutOfRangeException(nameof(colorTolerance), "Color tolerance must be between 0 and 127.");
        }

        _markerColors = markerColors.Select(ParseColor).ToArray();
        _colorTolerance = colorTolerance;

    }

    public ScreenSquare[]? FindSquares(string? debugImagePath = null)
    // Finds the middle row of 16 adjacent squares on the screen that match the expected marker colors.
    // Squares should be above 1px in size to avoid anti-aliasing issues, and the size of the squares should be consistent across the row.
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Screen pixel reading is supported only on Windows.");
        }

        if (debugImagePath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(debugImagePath);
        }

        var pixels = CaptureScreenRegion();
        if (debugImagePath is not null)
        {
            SaveBitmap(pixels, debugImagePath);
        }

        var matches = new List<ScreenSquare[]>();

        for (var y = 0; y < ScanHeight; y++)
        {
            for (var x = 0; x < ScanWidth; x++)
            {
                if (!ColorsMatch(pixels[x, y], _markerColors[0]))
                {

                    continue;
                }

                //Console.WriteLine($"Found first marker color at ({x}, {y}) with color {pixels[x, y]}.");
                for (var size = 1; size <= MaximumSquareSize; size++)
                {
                    if (x + (SquareCount * size) > ScanWidth || y + size > ScanHeight)
                    {
                        continue;
                    }
                    if (TryReadSignatureSequence(pixels, x, y, size, out var squares))
                    {
                        matches.Add(squares);
                    }
                }
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        var sizeGroups = matches
            .GroupBy(match => match[0].Size)
            .ToArray();
        var largestSizeOccurrence = sizeGroups.Max(group => group.Count());
        var mostCommonSizes = sizeGroups
            .Where(group => group.Count() == largestSizeOccurrence)
            .Select(group => group.Key)
            .ToArray();
        var yValues = matches
            .Where(match => mostCommonSizes.Contains(match[0].Size))
            .Select(match => match[0].Y)
            .Distinct()
            .OrderBy(y => y)
            .ToArray();
        var middle = yValues.Length / 2;
        var medianY = yValues.Length % 2 == 0
            ? (int)((yValues[middle - 1] + yValues[middle]) / 2.0)
            : yValues[middle];

        // Console.WriteLine(
        //     $"Median Y for the most common square size(s) " +
        //     $"({string.Join(", ", mostCommonSizes)}): {medianY}");

        var finalMatches = matches
            .Where(match =>
                mostCommonSizes.Contains(match[0].Size)
                && match[0].Y == medianY)
            .SelectMany(match => match)
            .GroupBy(square => square.Index)
            .Select(group => group.OrderBy(square => square.X).First())
            .OrderBy(square => square.Index)
            .ToArray();

        //Console.WriteLine($"Final matches at median Y {medianY}: {finalMatches.Length}");

        if (finalMatches.Length == 0)
        {
            throw new InvalidOperationException(
                $"No matching squares were found at the median Y value {medianY}.");
        }

        return finalMatches switch
        {
            { Length: 0 } => throw new InvalidOperationException("No matching squares were found."),
            _ => finalMatches
        };
    }

    public ScreenSquare[]? ReadSquares(ScreenSquare[] in_squares)
    // Read squares from known location on the screen
    {
        ArgumentNullException.ThrowIfNull(in_squares);
        if (in_squares.Length < 6)
        {
            throw new ArgumentException("At least six squares are required.", nameof(in_squares));
        }

        var out_squares = new ScreenSquare[in_squares.Length];
        var topLeftX = in_squares.Min(square => square.X);
        var topLeftY = in_squares.Min(square => square.Y);
        var bottomRightX = in_squares.Max(square => square.X) + 1;
        var bottomRightY = in_squares.Max(square => square.Y) + 1;
        var size = in_squares[0].Size;

        var pixels = CaptureScreenRegion(topLeftX,topLeftY, bottomRightX, bottomRightY);

        var index = 0;
        for (var x = 0; x < pixels.GetLength(0); x += size)
        {
            out_squares[index] = new ScreenSquare(index, index, 0, 1, pixels[x, 0]);
            index++;
        }

        for(var i = 0; i < _markerColors.Length; i++)
        {
            if (!ColorsMatch(out_squares[i].Color, _markerColors[i])) {
                return null; // If the marker colors don't match, return null to indicate failure
            }
        }

        return out_squares;
    }

    private static void SaveBitmap(RgbColor[,] pixels, string path)
    {
        const int fileHeaderSize = 14;
        const int bitmapHeaderSize = 40;
        const int bytesPerPixel = 3;

        var rowSize = ((ScanWidth * bytesPerPixel + 3) / 4) * 4;
        var imageSize = rowSize * ScanHeight;
        var fileSize = fileHeaderSize + bitmapHeaderSize + imageSize;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0x4D42);
        writer.Write(fileSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(fileHeaderSize + bitmapHeaderSize);

        writer.Write(bitmapHeaderSize);
        writer.Write(ScanWidth);
        writer.Write(ScanHeight);
        writer.Write((ushort)1);
        writer.Write((ushort)(bytesPerPixel * 8));
        writer.Write(BiRgb);
        writer.Write(imageSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        var paddingBytes = rowSize - (ScanWidth * bytesPerPixel);
        for (var y = ScanHeight - 1; y >= 0; y--)
        {
            for (var x = 0; x < ScanWidth; x++)
            {
                var pixel = pixels[x, y];
                writer.Write(pixel.Blue);
                writer.Write(pixel.Green);
                writer.Write(pixel.Red);
            }

            for (var padding = 0; padding < paddingBytes; padding++)
            {
                writer.Write((byte)0);
            }
        }
    }

    public static RgbColor[,] CaptureScreenRegion(
        int left = 0,
        int top = 0,
        int right = ScanWidth - 1,
        int bottom = ScanHeight - 1)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Screen pixel capture is supported only on Windows.");
        }

        if (right < left || bottom < top)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "Capture bounds must have right >= left and bottom >= top.");
        }

        var width = checked(right - left + 1);
        var height = checked(bottom - top + 1);
        var pixelCount = checked(width * height);
        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to get the screen device context.");
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previousBitmap = IntPtr.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create a compatible screen context.");
            }

            var bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BiRgb,
                    SizeImage = checked((uint)(pixelCount * sizeof(int)))
                }
            };

            bitmap = CreateDIBSection(
                screenDc,
                ref bitmapInfo,
                DibRgbColors,
                out var pixelData,
                IntPtr.Zero,
                0);
            if (bitmap == IntPtr.Zero || pixelData == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to allocate the screen capture bitmap.");
            }

            previousBitmap = SelectObject(memoryDc, bitmap);
            if (previousBitmap == IntPtr.Zero || previousBitmap == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to select the screen capture bitmap.");
            }

            if (!BitBlt(
                    memoryDc,
                    0,
                    0,
                    width,
                    height,
                    screenDc,
                    left,
                    top,
                    SourceCopy | CaptureBlt))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to capture the screen region.");
            }

            var rawPixels = new int[pixelCount];
            Marshal.Copy(pixelData, rawPixels, 0, rawPixels.Length);
            var pixels = new RgbColor[width, height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = unchecked((uint)rawPixels[(y * width) + x]);
                    pixels[x, y] = new RgbColor(
                        (byte)((pixel >> 16) & 0xFF),
                        (byte)((pixel >> 8) & 0xFF),
                        (byte)(pixel & 0xFF));
                }
            }

            return pixels;
        }
        finally
        {
            if (previousBitmap != IntPtr.Zero && previousBitmap != new IntPtr(-1))
            {
                SelectObject(memoryDc, previousBitmap);
            }

            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private bool TryReadSignatureSequence(
        RgbColor[,] pixels,
        int x,
        int y,
        int size,
        out ScreenSquare[] squares)
    {
        squares = new ScreenSquare[SquareCount];

        var squaresMatched = 0;      

        if (ColorsMatch(pixels[x , y], _markerColors[0]))
        {
            squaresMatched++;
        }
        if (ColorsMatch(pixels[x + size, y], _markerColors[1]))
        {
            squaresMatched++;
        }
        if (ColorsMatch(pixels[x + (2 * size), y], _markerColors[2]))
        {
            squaresMatched++;
        }
        if (ColorsMatch(pixels[x + (3 * size), y], _markerColors[3]))
        {
            squaresMatched++;
        }
        if (squaresMatched < 4)
        {
            return false;
        }        

        // at this point we have matched all required markers comprising the data preamble
        if (!ColorsMatch(pixels[x + ((SquareCount - 1) * size), y], new RgbColor(255,255,255)))
        // If we found a matching preamble check for footer to verify size is correct.
        {
            return false;
        }

        // Find middle and re-verify the colors to ensure we have a valid sequence and size.
        // This removes false positives where the first few cells match multiple sizes
        if (size > 1)
        {
            if (!ColorsMatch(pixels[x + (size / 2), y + (size / 2)], _markerColors[0])
                || !ColorsMatch(pixels[x + size + (size / 2), y + (size / 2)], _markerColors[1])
                || !ColorsMatch(pixels[x + (2 * size) + (size / 2), y + (size / 2)], _markerColors[2])
                || !ColorsMatch(pixels[x + (3 * size) + (size / 2), y + (size / 2)], _markerColors[3]))
            {
                return false;
            }
        }

        //Console.WriteLine($"Signature bar found at ({x}, {y}), box size {size}");

        for (var index = 0; index < _markerColors.Length; index++)
        {
            var squareX = x + (index * size);
            var color = pixels[squareX + (size / 2), y + (size / 2)];
            //Console.WriteLine($"Square {index}: position=({squareX}, {y}), size={size}x{size}, RGB=({color.Red}, {color.Green}, {color.Blue})");
            squares[index] = new ScreenSquare(index, squareX, y, size, color);
        }

        for (var index = _markerColors.Length; index < SquareCount; index++)
        {
            var squareX = x + (index * size);
            var color = pixels[squareX + (size / 2), y + (size / 2)];
            //Console.WriteLine($"Square {index}: position=({squareX}, {y}), size={size}x{size}, RGB=({color.Red}, {color.Green}, {color.Blue})");
            squares[index] = new ScreenSquare(index, squareX, y, size, color);
        }

        return true;
    }

    private bool ColorsMatch(RgbColor actual, RgbColor expected)
    {
        return Math.Abs(actual.Red - expected.Red) <= _colorTolerance
            && Math.Abs(actual.Green - expected.Green) <= _colorTolerance
            && Math.Abs(actual.Blue - expected.Blue) <= _colorTolerance;
    }

    private static RgbColor ParseColor(string color)
    {
        if (color is null
            || color.Length != 7
            || color[0] != '#'
            || !byte.TryParse(color.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
            || !byte.TryParse(color.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
            || !byte.TryParse(color.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            throw new ArgumentException($"Invalid RGB marker color \"{color}\". Expected #RRGGBB.");
        }

        return new RgbColor(red, green, blue);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr gdiObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr gdiObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(
        IntPtr destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr source,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public RgbQuad Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RgbQuad
    {
        public byte Blue;
        public byte Green;
        public byte Red;
        public byte Reserved;
    }
}

public readonly record struct RgbColor(byte Red, byte Green, byte Blue);

public sealed record ScreenSquare(int Index, int X, int Y, int Size, RgbColor Color);