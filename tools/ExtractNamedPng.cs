using System;
using System.IO;
using System.Text;

internal static class ExtractNamedPng
{
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private static int Find(byte[] haystack, byte[] needle, int start, int end)
    {
        end = Math.Min(end, haystack.Length - needle.Length);
        for (int i = Math.Max(0, start); i <= end; i++)
        {
            bool matched = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { matched = false; break; }
            }
            if (matched) return i;
        }
        return -1;
    }

    private static int ReadInt32BigEndian(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) |
               (bytes[offset + 2] << 8) | bytes[offset + 3];
    }

    public static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: ExtractNamedPng <exe> <asset-name-prefix> <output.png>");
            return 2;
        }

        byte[] bytes = File.ReadAllBytes(args[0]);
        byte[] marker = Encoding.UTF8.GetBytes("/assets/" + args[1]);
        int markerOffset = Find(bytes, marker, 0, bytes.Length);
        if (markerOffset < 0)
        {
            Console.Error.WriteLine("Asset marker not found: " + args[1]);
            return 3;
        }

        int pngOffset = Find(bytes, PngSignature, markerOffset + marker.Length, markerOffset + marker.Length + 4096);
        if (pngOffset < 0)
        {
            Console.Error.WriteLine("PNG payload was not found after the asset marker.");
            return 4;
        }

        int cursor = pngOffset + PngSignature.Length;
        int end = -1;
        while (cursor <= bytes.Length - 12)
        {
            int length = ReadInt32BigEndian(bytes, cursor);
            if (length < 0 || length > 268435456 || (long)cursor + 12 + length > bytes.Length) break;
            string type = Encoding.ASCII.GetString(bytes, cursor + 4, 4);
            cursor += 12 + length;
            if (type == "IEND") { end = cursor; break; }
        }
        if (end < 0)
        {
            Console.Error.WriteLine("PNG payload was incomplete.");
            return 5;
        }

        string parent = Path.GetDirectoryName(Path.GetFullPath(args[2]));
        Directory.CreateDirectory(parent);
        using (FileStream output = File.Create(args[2])) output.Write(bytes, pngOffset, end - pngOffset);

        int width = ReadInt32BigEndian(bytes, pngOffset + 16);
        int height = ReadInt32BigEndian(bytes, pngOffset + 20);
        Console.WriteLine("Extracted {0} ({1}x{2}) to {3}", args[1], width, height, args[2]);
        return 0;
    }
}
