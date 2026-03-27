#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages;

internal sealed class HomePage : PageWithExclusiveOptions
{
    public const string OptionNew = "new";
    public const string OptionRen = "ren";
    public const string OptionDel = "del";
    public const string OptionExit = "exit";
    public const string OptionRefresh = "ls";
    public const string OptionUpdateApp = "update";
    public const string OptionSearch = "search";

    private static readonly string[] FileOptions = { OptionRen, OptionDel };

    private readonly HomeWorkflow _workflow;
    private readonly bool _initialBannerIsError;
    private Dictionary<int, FileInfo> _fileSelection = new();
    private string _initialBannerMessage;

    public HomePage(FocusAppContext appContext, string initialBannerMessage = "", bool initialBannerIsError = false)
    {
        _workflow = new HomeWorkflow(appContext);
        _initialBannerMessage = initialBannerMessage;
        _initialBannerIsError = initialBannerIsError;
    }

    public override void Show()
    {
        var shouldExit = false;

        while (!shouldExit)
        {
            AppConsole.Current.Clear();
            AppConsole.Current.SetTitle("Welcome");
            ColorfulConsole.WriteLine(GetHeaderRibbonString("Welcome"));

            _fileSelection = _workflow.GetFileSelection();
            var initialBanner = ConsumeInitialBannerText();
            if (!string.IsNullOrWhiteSpace(initialBanner))
            {
                ColorfulConsole.Write(initialBanner);
            }

            var input = GetCommand(_workflow.BuildHomePageText(_fileSelection));

            if (string.IsNullOrWhiteSpace(input.InputString))
                continue;

            var result = _workflow.Execute(input, _fileSelection);
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                ShowMessage(result.Message, result.IsError);
            }

            shouldExit = result.ShouldExit;
        }
    }

    internal string ConsumeInitialBannerText()
    {
        if (string.IsNullOrWhiteSpace(_initialBannerMessage))
            return string.Empty;

        var prefix = _initialBannerIsError ? ":!" : ":i";
        var bannerText = $"{prefix} {_initialBannerMessage}{Environment.NewLine}{Environment.NewLine}";
        _initialBannerMessage = string.Empty;
        return bannerText;
    }

    protected override string[] GetCommandOptions()
    {
        var optionsWhenFileExists = new[] { OptionNew, OptionRen, OptionDel, OptionRefresh, OptionSearch, OptionExit, OptionUpdateApp };
        var optionsWhenNoFileExists = new[] { OptionNew, OptionRefresh, OptionSearch, OptionExit, OptionUpdateApp };

        return _fileSelection.Any()
            ? _fileSelection.Keys.Select(k => k.ToString())
                .Union(_fileSelection.Keys.Select(AccessibleKeyNumbering.GetStringFor))
                .Union(optionsWhenFileExists)
                .ToArray()
            : optionsWhenNoFileExists;
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
    {
        if (!_fileSelection.Any())
            return GetCommandOptions();

        return GetCommandOptions()
            .Union(_fileSelection.Values.Select(file => $"{OptionSearch} {file.NameWithoutExtension()}"))
            .Union(FileOptions.SelectMany(option => _fileSelection.Keys.Select(key => $"{option} {key}")))
            .Union(FileOptions.SelectMany(option => _fileSelection.Keys.Select(key => $"{option} {AccessibleKeyNumbering.GetStringFor(key)}")));
    }

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
