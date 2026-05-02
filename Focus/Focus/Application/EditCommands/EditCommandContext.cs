#nullable enable

using System;
using System.IO;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal sealed class EditCommandContext
{
    private readonly Func<MindMap> _getMap;
    private readonly Action<MindMap> _setMap;
    private readonly Func<string> _getFilePath;
    private readonly Action<string> _setFilePath;

    public EditCommandContext(
        FocusAppContext appContext,
        Func<MindMap> getMap,
        Action<MindMap> setMap,
        Func<string> getFilePath,
        Action<string> setFilePath)
    {
        AppContext = appContext;
        _getMap = getMap;
        _setMap = setMap;
        _getFilePath = getFilePath;
        _setFilePath = setFilePath;
    }

    public FocusAppContext AppContext { get; }

    public MindMap Map
    {
        get => _getMap();
        set => _setMap(value);
    }

    public string FilePath
    {
        get => _getFilePath();
        set => _setFilePath(value);
    }

    public string BuildMapCommitMessage(string action, string relation = "in")
    {
        var mapName = Path.GetFileNameWithoutExtension(FilePath);
        if (string.IsNullOrWhiteSpace(mapName))
            mapName = Path.GetFileName(FilePath);

        return $"{action} {relation} {mapName ?? "map"}";
    }

    public CommandExecutionResult PersistMapChange(string action, string relation = "in", string? message = null) =>
        CommandExecutionResult.SuccessAndPersist(message, BuildMapCommitMessage(action, relation));
}
