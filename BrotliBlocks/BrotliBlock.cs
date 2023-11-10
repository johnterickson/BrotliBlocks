// BitArray 

using System.Collections;
using BrotliSharpLib;

class BrotliBlock
{
    
    public static void Main(string[] args)
    {
        var input = new MemoryStream();
        Console.OpenStandardInput().CopyTo(input);
        input.Position = 0;

        if (args[0] == "-c")
        {
            byte[] compressed = Brotli.CompressBuffer(input.ToArray(), 0, (int)input.Length);
            Console.OpenStandardOutput().Write(compressed, 0, compressed.Length);
        }
        else if (args[0] == "-d")
        {
            byte[] decompressed = Brotli.DecompressBuffer(input.ToArray(), 0, (int)input.Length);
            Console.OpenStandardOutput().Write(decompressed, 0, decompressed.Length);
        }
        else if (args[0] == "-cb")
        {
            byte[] compressed = Brotli.CompressBuffer(input.ToArray(), 0, (int)input.Length);
            (byte[] _, byte[] bareBlock) = ExtractRawMetaBlock(compressed);
            Console.OpenStandardOutput().Write(bareBlock, 0, bareBlock.Length);
        }
        else if (args[0] == "-db")
        {
            var ms = new MemoryStream();
            ms.Write(BrotliBlock.StartBlock);
            input.CopyTo(ms);
            ms.Write(BrotliBlock.EndBlock);
            ms.Position = 0;

            var decompressed = Brotli.DecompressBuffer(ms.ToArray(), 0, (int)ms.Length, null);
            Console.OpenStandardOutput().Write(decompressed, 0, decompressed.Length);
        }
        else if (args[0] == "-s")
        {
            (byte[] _, byte[] bareBlock) = ExtractRawMetaBlock(input.ToArray());
            Console.OpenStandardOutput().Write(bareBlock, 0, bareBlock.Length);
        }
        else
        {
            throw new ArgumentException(args[0]);
        }
    }

    public static readonly byte[] StartBlock = new byte[] { 0x6b, 0x00};
    public static readonly byte[] EndBlock = new byte[] { 0x03 };

    public static (byte[] Decompressed, byte[] BareBlock) ExtractRawMetaBlock(byte[] buffer)
    {
        byte[] decompressed = Brotli.DecompressBuffer(buffer, 0, buffer.Length, out (int Start, int End) lastMetaBlockBitRanges);
        int headerBitsToSkip = HeaderBitLength(buffer[0]);

        var bits = new BitArray(buffer);
        bits.LeftShift(headerBitsToSkip);
        bits.Length = lastMetaBlockBitRanges.End - headerBitsToSkip;

        PadMetaBlockToByteBoundary(bits);

        var bareBlockBytes = new byte[bits.Length / 8];
        bits.CopyTo(bareBlockBytes, 0);

        return (decompressed, bareBlockBytes);
    }

    public static void PadMetaBlockToByteBoundary(BitArray bits)
    {
        if (bits.Length % 8 == 0)
        {
            return;
        }

        int blockLengthByteCount = (bits.Length + 6 + 7) / 8;
        int endOfBlock = bits.Length;
        bits.Length = blockLengthByteCount * 8;

        // write out '6' in 6 bits: LSB 011000 MSB
        int bitIndex = endOfBlock;
        for(int i = 0; i < 6; i++)
        {
            bits[bitIndex + i] = ((0x6 >> i) & 0x1) != 0 ? true : false;
        }
        bitIndex += 6;

        while (bitIndex < bits.Length)
        {
            bits[bitIndex] = false;
            bitIndex += 1;
        }
    }

    public static int HeaderBitLength(byte b)
    {
/*
      1..7 bits: WBITS, a value in the range 10..24, encoded with the
                 following variable-length code (as it appears in the
                 compressed data, where the bits are parsed from right
                 to left):

                      Value    Bit Pattern
                      -----    -----------
                         10        0100001
                         11        0110001
                         12        1000001
                         13        1010001
                         14        1100001
                         15        1110001
                         16              0
                         17        0000001
                         18           0011
                         19           0101
                         20           0111
                         21           1001
                         22           1011
                         23           1101
                         24           1111
*/
        if ((b & 0x1) == 0)
        {
            return 1;
        }
        else
        {
            if ((b & 0x3) == 0x3)
            {
                return 4;
            }
            else if ((b & 0xF) == 0x1 && (b & 0x7F) != 0x11)
            {
                return 7;
            }
            else
            {
                throw new ArgumentException($"Unexpected window byte: {b}");
            }
        }
    }
}