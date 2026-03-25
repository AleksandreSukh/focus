using System;
using System.Collections.Generic;
using System.Text;
using Systems.Sanity.Focus;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal class ExportPage : Page
{
    private const string MarkdownOption = "md";
    private const string HtmlOption = "html";
    private const string FullOption = "full";
    private const string CollapsedOption = "collapsed";
    private const string NameOption = "name";
    private const string SaveOption = "save";
    private const string CancelOption = "cancel";

    private readonly string _defaultFileName;
    private string _fileName;
    private ExportFormat _format = ExportFormat.Markdown;
    private bool _skipCollapsedDescendants;
    private string _message;
    private bool _isError;
    private bool _cancelled;

    public ExportPage(string defaultFileName)
    {
        _defaultFileName = defaultFileName;
        _fileName = defaultFileName;
    }

    public ExportRequest? SelectedExport { get; private set; }

    public override void Show()
    {
        while (SelectedExport == null && !_cancelled)
        {
            Console.Clear();
            ColorfulConsole.WriteLine(BuildScreen());

            var input = GetInput("Choose export option").InputString;
            if (string.IsNullOrWhiteSpace(input))
                continue;

            HandleInput(new ConsoleInput(input));
        }
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index) =>
        new[]
        {
            MarkdownOption,
            HtmlOption,
            FullOption,
            CollapsedOption,
            $"{NameOption} {_fileName}",
            SaveOption,
            CancelOption
        };

    private void HandleInput(ConsoleInput input)
    {
        _message = null;
        _isError = false;

        switch (input.FirstWord.ToCommandLanguage())
        {
            case MarkdownOption:
                _format = ExportFormat.Markdown;
                _message = "Format set to Markdown";
                return;
            case HtmlOption:
                _format = ExportFormat.Html;
                _message = "Format set to HTML";
                return;
            case FullOption:
                _skipCollapsedDescendants = false;
                _message = "Scope set to full subtree";
                return;
            case CollapsedOption:
                _skipCollapsedDescendants = true;
                _message = "Collapsed descendants will be skipped";
                return;
            case NameOption:
                UpdateFileName(input.Parameters);
                return;
            case SaveOption:
                SelectedExport = new ExportRequest(_format, _fileName, _skipCollapsedDescendants);
                return;
            case CancelOption:
                _cancelled = true;
                return;
            default:
                _message = $"Unknown option \"{input.FirstWord}\"";
                _isError = true;
                return;
        }
    }

    private void UpdateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _message = "Provide a file name after \"name\"";
            _isError = true;
            return;
        }

        _fileName = fileName.Trim();
        _message = $"File name set to \"{_fileName}\"";
    }

    private string BuildScreen()
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLineCentered("*** Export ***");
        builder.AppendLine();
        builder.AppendLine($"Current file name: {_fileName}");
        builder.AppendLine($"Default file name: {_defaultFileName}");
        builder.AppendLine($"Format: {_format.ToDisplayString()} ({_format.GetFileExtension()})");
        builder.AppendLine($"Scope: {(_skipCollapsedDescendants ? "Skip descendants under collapsed nodes" : "Full subtree")}");
        builder.AppendLine();
        builder.AppendLine($"\"[{ConfigurationConstants.CommandColor}]{MarkdownOption}[!]\" - export as Markdown");
        builder.AppendLine($"\"[{ConfigurationConstants.CommandColor}]{HtmlOption}[!]\" - export as HTML");
        builder.AppendLine($"\"[{ConfigurationConstants.CommandColor}]{FullOption}[!]\" - include all descendants");
        builder.AppendLine($"\"[{ConfigurationConstants.CommandColor}]{CollapsedOption}[!]\" - skip descendants under collapsed nodes");
        builder.AppendLine($"\"[{ConfigurationConstants.CommandColor}]{NameOption} <file name>[!]\" - set exported file name");
        builder.AppendLine($"\"[{ConfigurationConstants.CommandColor}]{SaveOption}[!]\" - create exported file");
        builder.AppendLine($"\"[{ConfigurationConstants.CommandColor}]{CancelOption}[!]\" - return without exporting");

        if (!string.IsNullOrWhiteSpace(_message))
        {
            builder.AppendLine();
            var messagePrefix = _isError ? ":!" : ":i";
            builder.AppendLine($"{messagePrefix} {_message}");
        }

        builder.AppendLine();
        return builder.ToString();
    }
}
