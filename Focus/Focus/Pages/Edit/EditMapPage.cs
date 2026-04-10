#nullable enable

using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Edit;

internal sealed class EditMapPage : PageWithSuggestedOptions
{
    private readonly FocusAppContext _appContext;
    private readonly string _filePath;
    private readonly EditWorkflow _workflow;
    private bool _showCommands = true;

    public EditMapPage(string filePath, FocusAppContext appContext, Guid? initialNodeIdentifier = null)
    {
        _appContext = appContext;
        _filePath = filePath;
        _workflow = new EditWorkflow(filePath, appContext, initialNodeIdentifier);
    }

    public override void Show()
    {
        string? message = null;
        var isError = false;
        var shouldExit = false;

        while (!shouldExit)
        {
            try
            {
                if (ShouldReturnToHomePageForUpdatedFile())
                    return;

                RenderScreen(message, isError);

                var commandResult = _workflow.Execute(ReadCommand(() => RenderScreen(message, isError)));
                shouldExit = commandResult.ShouldExit;
                if (shouldExit)
                    continue;

                if (commandResult.IsSuccess)
                {
                    if (commandResult.ShouldPersist)
                    {
                        try
                        {
                            _workflow.Save(commandResult.SyncCommitMessage
                                ?? throw new InvalidOperationException("Sync commit message is required for persisted commands."));
                        }
                        catch (Exception ex)
                        {
                            message = ExceptionDiagnostics.LogException(ex, "saving map changes");
                            isError = true;
                            continue;
                        }
                    }

                    message = commandResult.Message;
                    isError = false;
                }
                else
                {
                    message = commandResult.ErrorString;
                    isError = true;
                }
            }
            catch (Exception ex)
            {
                message = ExceptionDiagnostics.LogException(ex, "executing map command");
                isError = true;
            }
        }
    }

    private void RenderScreen(string? message, bool isError)
    {
        AppConsole.Current.SetTitle(
            _appContext.StartupSyncNotificationState.BuildTitle(_workflow.FileTitle, _workflow.FilePath));
        AppConsole.Current.Clear();
        AppConsole.Current.ClearScrollback();
        ColorfulConsole.Write(_workflow.BuildScreen(message, isError, _showCommands));
    }

    private ConsoleInput ReadCommand(Action rerender)
    {
        ColorfulConsole.WriteLine(string.Empty);
        return new ConsoleInput(AppConsole.Current.CommandLineEditor
            .Read(
                string.Empty,
                string.Empty,
                BeforeEachAutoComplete,
                AfterEachAutoComplete,
                (keyInfo, currentText) => HandlePreviewKey(keyInfo, currentText, rerender))
            .Trim());
    }

    private bool HandlePreviewKey(ConsoleKeyInfo keyInfo, string currentText, Action rerender)
    {
        if (!string.IsNullOrEmpty(currentText) || keyInfo.KeyChar != '~')
            return false;

        _showCommands = !_showCommands;
        rerender();
        ColorfulConsole.WriteLine(string.Empty);
        return true;
    }

    private bool ShouldReturnToHomePageForUpdatedFile()
    {
        if (!_appContext.StartupSyncNotificationState.TryConsumeCurrentFileUpdateWarning(_workflow.FilePath, out var warningMessage))
            return false;

        return new Confirmation(warningMessage).Confirmed();
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
    {
        return _workflow.GetSuggestions();
    }
}
