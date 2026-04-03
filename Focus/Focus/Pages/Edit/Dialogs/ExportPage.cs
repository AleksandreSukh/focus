#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;
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
    private const string BlackBackgroundOption = "blackbg";
    private const string LightBackgroundOption = "lightbg";
    private const string FullOption = "full";
    private const string CollapsedOption = "collapsed";
    private const string AttachmentsOption = "attachments";
    private const string NoAttachmentsOption = "noattachments";
    private const string NameOption = "name";
    private const string SaveOption = "save";
    private const string CancelOption = "cancel";

    private readonly string _defaultFileName;
    private string _fileName;
    private ExportFormat _format = ExportFormat.Markdown;
    private bool _skipCollapsedDescendants;
    private bool _useBlackBackground;
    private bool _includeAttachments;
    private string? _message;
    private bool _isError;
    private bool _cancelled;

    public ExportPage(string defaultFileName)
    {
        _defaultFileName = MapFileHelper.SanitizeFileName(defaultFileName, fallbackFileName: "export");
        _fileName = _defaultFileName;
    }

    public ExportRequest? SelectedExport { get; private set; }

    public override void Show()
    {
        while (SelectedExport == null && !_cancelled)
        {
            AppConsole.Current.Clear();
            ColorfulConsole.WriteLine(BuildScreen());

            var input = GetInput("Choose export option").InputString;
            if (string.IsNullOrWhiteSpace(input))
                continue;

            HandleInput(new ConsoleInput(input));
        }
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index) =>
        GetVisibleOptions().Select(option => option.Suggestion);

    private void HandleInput(ConsoleInput input)
    {
        _message = null;
        _isError = false;

        switch (input.FirstWord.ToCommandLanguage())
        {
            case MarkdownOption:
                SetFormat(ExportFormat.Markdown);
                return;
            case HtmlOption:
                SetFormat(ExportFormat.Html);
                return;
            case BlackBackgroundOption:
                SetBackground(useBlackBackground: true);
                return;
            case LightBackgroundOption:
                SetBackground(useBlackBackground: false);
                return;
            case FullOption:
                SetScope(skipCollapsedDescendants: false);
                return;
            case CollapsedOption:
                SetScope(skipCollapsedDescendants: true);
                return;
            case AttachmentsOption:
                SetAttachments(includeAttachments: true);
                return;
            case NoAttachmentsOption:
                SetAttachments(includeAttachments: false);
                return;
            case NameOption:
                UpdateFileName(input.Parameters);
                return;
            case SaveOption:
                SelectedExport = new ExportRequest(
                    _format,
                    _fileName,
                    _skipCollapsedDescendants,
                    _format == ExportFormat.Html && _useBlackBackground,
                    _includeAttachments);
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

        _fileName = MapFileHelper.SanitizeFileName(fileName.Trim(), fallbackFileName: "export");
        _message = $"File name set to \"{_fileName}\"";
    }

    private void SetFormat(ExportFormat format)
    {
        if (_format == format)
        {
            _message = $"Format already set to {format.ToDisplayString()}";
            return;
        }

        _format = format;
        _message = $"Format set to {format.ToDisplayString()}";
    }

    private void SetBackground(bool useBlackBackground)
    {
        if (_format != ExportFormat.Html)
        {
            _message = "Background option is only available for HTML export";
            _isError = true;
            return;
        }

        if (_useBlackBackground == useBlackBackground)
        {
            _message = useBlackBackground
                ? "Background already set to black"
                : "Background already set to light";
            return;
        }

        _useBlackBackground = useBlackBackground;
        _message = useBlackBackground
            ? "Background set to black"
            : "Background set to light";
    }

    private void SetScope(bool skipCollapsedDescendants)
    {
        if (_skipCollapsedDescendants == skipCollapsedDescendants)
        {
            _message = skipCollapsedDescendants
                ? "Scope already set to skip descendants under collapsed nodes"
                : "Scope already set to full subtree";
            return;
        }

        _skipCollapsedDescendants = skipCollapsedDescendants;
        _message = skipCollapsedDescendants
            ? "Collapsed descendants will be skipped"
            : "Scope set to full subtree";
    }

    private void SetAttachments(bool includeAttachments)
    {
        if (_includeAttachments == includeAttachments)
        {
            _message = includeAttachments
                ? "Attachments are already included"
                : "Attachments are already excluded";
            return;
        }

        _includeAttachments = includeAttachments;
        _message = includeAttachments
            ? "Attachments will be included"
            : "Attachments will be excluded";
    }

    private IEnumerable<ExportOption> GetVisibleOptions()
    {
        if (_format == ExportFormat.Markdown)
        {
            yield return new ExportOption(HtmlOption, HtmlOption, "export as HTML");
        }
        else
        {
            yield return new ExportOption(MarkdownOption, MarkdownOption, "export as Markdown");
        }

        if (_format == ExportFormat.Html)
        {
            if (_useBlackBackground)
            {
                yield return new ExportOption(LightBackgroundOption, LightBackgroundOption, "use default light page background");
            }
            else
            {
                yield return new ExportOption(BlackBackgroundOption, BlackBackgroundOption, "use black page background");
            }
        }

        if (_skipCollapsedDescendants)
        {
            yield return new ExportOption(FullOption, FullOption, "include all descendants");
        }
        else
        {
            yield return new ExportOption(CollapsedOption, CollapsedOption, "skip descendants under collapsed nodes");
        }

        if (_includeAttachments)
        {
            yield return new ExportOption(NoAttachmentsOption, NoAttachmentsOption, "exclude attachments from export");
        }
        else
        {
            yield return new ExportOption(AttachmentsOption, AttachmentsOption, "include attachments in export");
        }

        yield return new ExportOption($"{NameOption} <file name>", $"{NameOption} {_fileName}", "set exported file name");
        yield return new ExportOption(SaveOption, SaveOption, "create exported file");
        yield return new ExportOption(CancelOption, CancelOption, "return without exporting");
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
        if (_format == ExportFormat.Html)
            builder.AppendLine($"Background: {(_useBlackBackground ? "Black" : "Light")}");
        builder.AppendLine($"Scope: {(_skipCollapsedDescendants ? "Skip descendants under collapsed nodes" : "Full subtree")}");
        builder.AppendLine($"Attachments: {(_includeAttachments ? "Included" : "Excluded")}");
        builder.AppendLine();
        foreach (var option in GetVisibleOptions())
        {
            builder.AppendLine(
                $"\"[{ConfigurationConstants.CommandColor}]{option.DisplayCommand}[!]\" - {option.Description}");
        }

        if (!string.IsNullOrWhiteSpace(_message))
        {
            builder.AppendLine();
            var messagePrefix = _isError ? ":!" : ":i";
            builder.AppendLine($"{messagePrefix} {_message}");
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private sealed record ExportOption(
        string DisplayCommand,
        string Suggestion,
        string Description);
}
