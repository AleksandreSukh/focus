using System;
using System.IO;

namespace Systems.Sanity.Focus.E2E.Tests;

internal static class TestDirectoryCleaner
{
    public static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(directoryPath, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
