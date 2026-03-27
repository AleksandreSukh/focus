#nullable enable

using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Edit;

internal sealed class EditMapPage : PageWithSuggestedOptions
{
    private readonly FocusAppContext _appContext;
    private readonly string _filePath;
    private readonly EditWorkflow _workflow;

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
            if (ShouldReturnToHomePageForUpdatedFile())
                return;

            AppConsole.Current.SetTitle(
                _appContext.StartupSyncNotificationState.BuildTitle(_workflow.FileTitle, _filePath));
            AppConsole.Current.Clear();
            AppConsole.Current.ClearScrollback();
            ColorfulConsole.Write(_workflow.BuildScreen(message, isError));

            var commandResult = _workflow.Execute(GetCommand());
            shouldExit = commandResult.ShouldExit;
            if (shouldExit)
                continue;

            if (commandResult.IsSuccess)
            {
                if (commandResult.ShouldPersist)
                    _workflow.Save(commandResult.SyncCommitMessage
                        ?? throw new InvalidOperationException("Sync commit message is required for persisted commands."));

                message = commandResult.Message;
                isError = false;
            }
            else
            {
                message = commandResult.ErrorString;
                isError = true;
            }
        }
    }

    private bool ShouldReturnToHomePageForUpdatedFile()
    {
        if (!_appContext.StartupSyncNotificationState.TryConsumeCurrentFileUpdateWarning(_filePath, out var warningMessage))
            return false;

        return new Confirmation(warningMessage).Confirmed();
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
    {
        return _workflow.GetSuggestions();
    }
}
