using System;
using System.Collections.Generic;
using System.IO;

namespace Systems.Sanity.Focus.Application;

internal sealed class StartupSyncNotificationState
{
    private readonly object _lockObject = new();
    private bool _hasRepositoryUpdates;
    private string _currentOpenFilePath = string.Empty;
    private string _changedOpenFilePath = string.Empty;
    private bool _changedOpenFileWarningPending;

    public bool HasRepositoryUpdates
    {
        get
        {
            lock (_lockObject)
            {
                return _hasRepositoryUpdates;
            }
        }
    }

    public void SetCurrentOpenFile(string filePath)
    {
        lock (_lockObject)
        {
            _currentOpenFilePath = NormalizePath(filePath);
        }
    }

    public void ClearCurrentOpenFile(string filePath)
    {
        lock (_lockObject)
        {
            if (IsSamePath(filePath, _currentOpenFilePath))
            {
                _currentOpenFilePath = string.Empty;
            }
        }
    }

    public void ApplyRepositoryUpdates(IReadOnlyCollection<string> changedFiles)
    {
        if (changedFiles.Count == 0)
            return;

        lock (_lockObject)
        {
            _hasRepositoryUpdates = true;

            foreach (var changedFile in changedFiles)
            {
                if (IsSamePath(changedFile, _currentOpenFilePath))
                {
                    _changedOpenFilePath = _currentOpenFilePath;
                    _changedOpenFileWarningPending = true;
                    break;
                }
            }
        }
    }

    public string BuildTitle(string defaultTitle, string filePath = "")
    {
        lock (_lockObject)
        {
            if (!_hasRepositoryUpdates)
                return defaultTitle;

            if (!string.IsNullOrWhiteSpace(filePath) &&
                _changedOpenFileWarningPending &&
                IsSamePath(filePath, _changedOpenFilePath))
            {
                return $"{defaultTitle} (update required)";
            }

            return $"{defaultTitle} (updates available)";
        }
    }

    public string GetCurrentTitle()
    {
        lock (_lockObject)
        {
            var defaultTitle = string.IsNullOrWhiteSpace(_currentOpenFilePath)
                ? "Welcome"
                : Path.GetFileName(_currentOpenFilePath) ?? "Focus";

            if (!_hasRepositoryUpdates)
                return defaultTitle;

            if (_changedOpenFileWarningPending && IsSamePath(_currentOpenFilePath, _changedOpenFilePath))
                return $"{defaultTitle} (update required)";

            return $"{defaultTitle} (updates available)";
        }
    }

    public bool TryConsumeCurrentFileUpdateWarning(string filePath, out string warningMessage)
    {
        lock (_lockObject)
        {
            if (!_changedOpenFileWarningPending || !IsSamePath(filePath, _changedOpenFilePath))
            {
                warningMessage = string.Empty;
                return false;
            }

            _changedOpenFileWarningPending = false;
            warningMessage =
                "This file was updated by background sync. Return to HomePage and reopen it? Unsaved changes in this editor will be discarded.";
            return true;
        }
    }

    public void AcknowledgeHomePageRefresh()
    {
        lock (_lockObject)
        {
            _hasRepositoryUpdates = false;
            _changedOpenFilePath = string.Empty;
            _changedOpenFileWarningPending = false;
        }
    }

    private static bool IsSamePath(string leftPath, string rightPath)
    {
        if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
            return false;

        return string.Equals(NormalizePath(leftPath), NormalizePath(rightPath), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string filePath)
    {
        return Path.GetFullPath(filePath);
    }
}
