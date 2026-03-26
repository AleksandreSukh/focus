#nullable enable

using System;
using System.Collections.Generic;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit;

internal sealed class EditMapPage : PageWithSuggestedOptions
{
    private readonly EditWorkflow _workflow;

    public EditMapPage(string filePath, FocusAppContext appContext, Guid? initialNodeIdentifier = null)
    {
        _workflow = new EditWorkflow(filePath, appContext, initialNodeIdentifier);
    }

    public override void Show()
    {
        AppConsole.Current.SetTitle(_workflow.FileTitle);

        string? message = null;
        var isError = false;
        var shouldExit = false;

        while (!shouldExit)
        {
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
                    _workflow.Save();

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

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
    {
        return _workflow.GetSuggestions();
    }
}
