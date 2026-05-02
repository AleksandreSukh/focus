#nullable enable

using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application;

internal sealed class CreateMapWorkflow
{
    private readonly FocusAppContext _appContext;

    public CreateMapWorkflow(FocusAppContext appContext)
    {
        _appContext = appContext;
    }

    public string? Create(string requestedFileName, MindMap map)
    {
        var sanitizedFileName = MapFilePathHelper.SanitizeFileName(
            (requestedFileName ?? string.Empty).Trim().Trim('\0'));
        var filePath = _appContext.WorkflowInteractions.RequestAvailableFilePath(
            _appContext.MapRepository.UserMindMapsDirectory,
            sanitizedFileName,
            ConfigurationConstants.RequiredFileNameExtension);
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        _appContext.MapRepository.SaveMap(filePath, map);
        NormalizeCreatedMapIdentifiers(filePath);
        _appContext.RefreshLinkIndex();
        return filePath;
    }

    private void NormalizeCreatedMapIdentifiers(string createdFilePath)
    {
        var usedIdentifiers = new HashSet<Guid>();
        foreach (var file in _appContext.MapRepository.GetAll())
        {
            if (string.Equals(file.FullName, createdFilePath, StringComparison.OrdinalIgnoreCase))
                continue;

            _appContext.MapRepository.OpenMap(file.FullName, usedIdentifiers);
        }

        _appContext.MapRepository.OpenMap(createdFilePath, usedIdentifiers);
    }
}
