#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Application.HomeCommands;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Diagnostics;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages;

internal sealed class HomePage : PageWithExclusiveOptions
{
    public const string OptionNew = HomeCommandKeys.New;
    public const string OptionRen = HomeCommandKeys.Rename;
    public const string OptionDel = HomeCommandKeys.Delete;
    public const string OptionExit = HomeCommandKeys.Exit;
    public const string OptionRefresh = HomeCommandKeys.Refresh;
    public const string OptionUpdateApp = HomeCommandKeys.UpdateApp;
    public const string OptionSearch = HomeCommandKeys.Search;
    public const string OptionTasks = HomeCommandKeys.Tasks;
    internal const string OptionTasksAlias = HomeCommandKeys.TasksAlias;

    private readonly FocusAppContext _appContext;
    private readonly HomeWorkflow _workflow;
    private Dictionary<int, FileInfo> _fileSelection = new();
    private readonly CommandHelpVisibilityState _commandHelpVisibility = new();

    public HomePage(FocusAppContext appContext)
    {
        _appContext = appContext;
        _workflow = new HomeWorkflow(appContext);
    }

    public override void Show()
    {
        var shouldExit = false;

        while (!shouldExit)
        {
            try
            {
                AppConsole.Current.SetTitle(_appContext.StartupSyncNotificationState.BuildTitle(ApplicationInfo.DefaultConsoleTitle));
                _fileSelection = _workflow.GetFileSelection();
                _appContext.StartupSyncNotificationState.AcknowledgeHomePageRefresh();

                RenderScreen();

                var input = GetCommand("", (keyInfo, currentText) => HandlePreviewKey(keyInfo, currentText));

                if (string.IsNullOrWhiteSpace(input.InputString))
                    continue;

                var result = _workflow.Execute(input, _fileSelection);
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    ShowMessage(result.Message, result.IsError);
                }

                shouldExit = result.ShouldExit;
            }
            catch (Exception ex)
            {
                ShowMessage(ExceptionDiagnostics.LogException(ex, "executing home page command"), isError: true);
            }
        }
    }

    private void RenderScreen()
    {
        AppConsole.Current.Clear();
        ColorfulConsole.WriteLine(GetHeaderRibbonString("Welcome"));
        ColorfulConsole.Write(_workflow.BuildHomePageText(_fileSelection, _commandHelpVisibility.ShowCommands));
    }

    private bool HandlePreviewKey(ConsoleKeyInfo keyInfo, string currentText)
    {
        if (!_commandHelpVisibility.TryHandlePreviewKey(keyInfo, currentText))
            return false;

        RenderScreen();
        ColorfulConsole.WriteLine(string.Empty);
        return true;
    }

    protected override string[] GetCommandOptions() =>
        _workflow.GetCommandOptions(_fileSelection);

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index) =>
        _workflow.GetSuggestions(_fileSelection);

    private static string GetHeaderRibbonString(string title)
    {
        var ribbonLength = Math.Max(0, (AppConsole.Current.WindowWidth - title.Length) / 2);
        var ribbon = new string('-', ribbonLength);
        return string.Format("\n{0}{1}{0}\n", ribbon, title);
    }

    private static void ShowMessage(string message, bool isError)
    {
        var prefix = isError ? ":!" : ":i";
        ColorfulConsole.WriteLine($"{Environment.NewLine}{prefix} {message}{Environment.NewLine}{Environment.NewLine}Press any key to continue");
        AppConsole.Current.ReadKey();
    }
}
