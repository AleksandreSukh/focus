#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Application;

internal sealed class MapSelectionService
{
    public const int DefaultSelectionSize = 100;

    private readonly IMapRepository _mapRepository;

    public MapSelectionService(IMapRepository mapRepository)
    {
        _mapRepository = mapRepository;
    }

    public Dictionary<int, FileInfo> GetTopSelection(int limit = DefaultSelectionSize)
    {
        var existingMaps = _mapRepository.GetTop(limit);
        var selection = new Dictionary<int, FileInfo>();

        for (var index = 0; index < existingMaps.Length; index++)
        {
            selection[index + 1] = existingMaps[index];
        }

        return selection;
    }

    public FileInfo? FindFile(IReadOnlyDictionary<int, FileInfo> selection, string fileIdentifier)
    {
        if (TryFindFileByNumber(selection, fileIdentifier, out var fileByNumber))
            return fileByNumber;

        if (TryFindFileByShortcut(selection, fileIdentifier, out var fileByShortcut))
            return fileByShortcut;

        if (TryFindFileByName(fileIdentifier, out var fileByName))
            return fileByName;

        if (TryFindLocalizedShortcut(selection, fileIdentifier, out var fileByLocalizedShortcut))
            return fileByLocalizedShortcut;

        return TryFindLocalizedFileByName(fileIdentifier, out var fileByLocalizedName)
            ? fileByLocalizedName
            : null;
    }

    private bool TryFindFileByName(string fileIdentifier, out FileInfo? file)
    {
        var fileNameIsValid = fileIdentifier.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        if (fileNameIsValid)
        {
            return (file = new FileInfo(Path.Combine(_mapRepository.UserMindMapsDirectory, fileIdentifier))).Exists
                || (file = new FileInfo(Path.Combine(_mapRepository.UserMindMapsDirectory, $"{fileIdentifier}.json"))).Exists;
        }

        file = null;
        return false;
    }

    private static bool TryFindFileByShortcut(
        IReadOnlyDictionary<int, FileInfo> selection,
        string fileIdentifier,
        out FileInfo? file)
    {
        var fileNumberFromShortcut = AccessibleKeyNumbering.GetNumberFor(fileIdentifier);
        if (fileNumberFromShortcut != 0 && selection.TryGetValue(fileNumberFromShortcut, out var selectedFile))
        {
            file = selectedFile;
            return true;
        }

        file = null;
        return false;
    }

    private static bool TryFindLocalizedShortcut(
        IReadOnlyDictionary<int, FileInfo> selection,
        string fileIdentifier,
        out FileInfo? file)
    {
        var normalizedIdentifier = fileIdentifier.ToCommandKey();
        if (string.Equals(normalizedIdentifier, fileIdentifier, StringComparison.Ordinal))
        {
            file = null;
            return false;
        }

        return TryFindFileByShortcut(selection, normalizedIdentifier, out file);
    }

    private bool TryFindLocalizedFileByName(string fileIdentifier, out FileInfo? file)
    {
        var normalizedIdentifier = fileIdentifier.ToCommandKey();
        if (string.Equals(normalizedIdentifier, fileIdentifier, StringComparison.Ordinal))
        {
            file = null;
            return false;
        }

        return TryFindFileByName(normalizedIdentifier, out file);
    }

    private static bool TryFindFileByNumber(
        IReadOnlyDictionary<int, FileInfo> selection,
        string fileIdentifier,
        out FileInfo? file)
    {
        if (int.TryParse(fileIdentifier, out var fileNumber) &&
            selection.TryGetValue(fileNumber, out var selectedFile))
        {
            file = selectedFile;
            return true;
        }

        file = null;
        return false;
    }
}
