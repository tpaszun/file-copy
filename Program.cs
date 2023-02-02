using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace FileCopy;

class Program
{
    static async Task Main(string[] args)
    {
        var baseDest = @"C:\";

        var sources = new[]
        {
            @"\\remote-server\share\some_file",
            @"\\remote-server\share\some_directory"
        };

        foreach (var src in sources)
        {
            var dest = Path.Combine(baseDest, src.Replace(@"\\", ""));

            if (File.Exists(dest))
            {
                await CopyFile(src, dest);
            }
            else if (Directory.Exists(dest))
            {
                await CopyDirectory(src, dest);
            }
        }
    }

    private static async Task CopyDirectory(string src, string dst)
    {
        if (!Directory.Exists(src))
            return;

        if (!Directory.Exists(dst))
            Directory.CreateDirectory(dst);

        foreach (var fileName in Directory.EnumerateFiles(src))
            await CopyFile(fileName, Path.Combine(dst, Path.GetFileName(fileName)));

        foreach (var dirName in Directory.EnumerateDirectories(src))
            await CopyDirectory(dirName, Path.Combine(dst, Path.GetFileName(dirName)));
    }

    private static async Task CopyFile(string src, string dst)
    {
        if (!File.Exists(dst))
        {
            Console.WriteLine($"Creating file '{dst}");
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Create(dst).Dispose();
        }

        Console.WriteLine($"Copying file '{src}' to '{dst}");
        await using var sourceStream = File.OpenRead(src);

        await using var destinationStream = File.OpenWrite(dst);

        var fileSizeInGB = (double)sourceStream.Length / (1024 * 1024 * 1024);
        Console.WriteLine($"File size: {fileSizeInGB:F2} GB");

        //if (fileSize > 1)
        //{
        //    Console.WriteLine($"File greater than 1GB ({fileSize:F2} GB)");
        //    return;
        //}

        var initialDestinationSize = destinationStream.Length;

        sourceStream.Seek(destinationStream.Length, SeekOrigin.Begin);
        destinationStream.Seek(destinationStream.Length, SeekOrigin.Begin);
        Console.WriteLine($"Position in source stream set to byte {destinationStream.Length}");

        var buffer = new byte[16 * 1024 * 1024];
        var elapsed = Stopwatch.StartNew();
        var stopwatch = Stopwatch.StartNew();
        var lastPosition = destinationStream.Length;
        while (sourceStream.Position < sourceStream.Length - 1)
        {
            //if (destinationStream.Position > 15 * 1024 * 1024)
            //    break;

            var read = await sourceStream.ReadAsync(buffer, 0, 16 * 1024 * 1024);
            await destinationStream.WriteAsync(buffer, 0, read);
            destinationStream.Flush(true);

            if (stopwatch.Elapsed.TotalSeconds < 5) continue;

            var leftSeconds = (sourceStream.Length - destinationStream.Position) / ((destinationStream.Position - lastPosition) / stopwatch.Elapsed.TotalSeconds);
            var left = TimeSpan.FromSeconds(leftSeconds);
            Console.WriteLine(
                $"Elapsed: {elapsed.Elapsed}, Completed {destinationStream.Position / (1024 * 1024)}MB/{sourceStream.Length / (1024 * 1024)}MB  ({(double)destinationStream.Position * 100 / sourceStream.Length:F2}%), {(double)(destinationStream.Position - lastPosition) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024):F3} MB/s, {left} left");
            lastPosition = destinationStream.Position;
            stopwatch.Restart();
        }

        Console.WriteLine($"Done. Completed {(sourceStream.Length - initialDestinationSize) / (1024 * 1024)}MB in {elapsed.Elapsed}, average download speed: {(sourceStream.Length - initialDestinationSize) / (elapsed.Elapsed.TotalSeconds * 1024 * 1024):F3} MB/s");
    }
}