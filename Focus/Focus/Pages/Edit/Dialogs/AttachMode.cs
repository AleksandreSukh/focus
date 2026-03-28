#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal sealed class AttachMode : PageWithSuggestedOptions
{
    public const string OptionExit = "exit";

    private readonly MindMap _map;
    private readonly FocusAppContext _appContext;
    private readonly string _targetMapFilePath;
    private Dictionary<int, FileInfo> _filesToChooseFrom = new();
    private string? _message;
    private bool _isError;
    private bool _shouldExit;

    public AttachMode(MindMap map, FocusAppContext appContext, string targetMapFilePath)
    {
        _map = map;
        _appContext = appContext;
        _targetMapFilePath = targetMapFilePath;
    }

    public bool DidAttachMap { get; private set; }

    public override void Show()
    {
        while (!_shouldExit)
        {
            _filesToChooseFrom = _appContext.MapSelectionService.GetTopSelection();
            AppConsole.Current.Clear();
            ColorfulConsole.WriteLine(BuildScreen());

            var input = GetCommand("Choose file identifier to attach or type \"exit\"");
            if (string.IsNullOrWhiteSpace(input.InputString))
                continue;

            HandleInput(input.FirstWord);
        }
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
    {
        return _filesToChooseFrom.Keys.Select(k => k.ToString())
            .Union(_filesToChooseFrom.Keys.Select(Infrastructure.Input.AccessibleKeyNumbering.GetStringFor))
            .Union(new[] { OptionExit });
    }

    private string BuildScreen()
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLineCentered("*** Attach Map ***");
        builder.AppendLine();

        foreach (var file in _filesToChooseFrom)
        {
            builder.AppendLine($"{file.Key} - {file.Value.NameWithoutExtension()}");
        }

        if (!string.IsNullOrWhiteSpace(_message))
        {
            builder.AppendLine();
            builder.AppendLine($"{(_isError ? ":!" : ":i")} {_message}");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private void HandleInput(string fileIdentifier)
    {
        _message = null;
        _isError = false;

        if (fileIdentifier.ToLowerInvariant() == OptionExit)
        {
            _shouldExit = true;
            return;
        }

        var file = _appContext.MapSelectionService.FindFile(_filesToChooseFrom, fileIdentifier);
        if (file == null)
        {
            _message = $"File \"{fileIdentifier}\" wasn't found. Try again.";
            _isError = true;
            return;
        }

        var map = _appContext.MapRepository.OpenMap(file.FullName);
        _appContext.MapsStorage.AttachmentStore.MoveReferencedAttachments(
            map.RootNode,
            file.FullName,
            _targetMapFilePath);
        _map.LoadAtCurrentNode(map);
        _appContext.MapRepository.DeleteMap(file);
        DidAttachMap = true;
        _shouldExit = true;
    }
}
