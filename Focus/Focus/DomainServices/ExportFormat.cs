using System;
using Systems.Sanity.Focus;

namespace Systems.Sanity.Focus.DomainServices;

internal enum ExportFormat
{
    Markdown,
    Html
}

internal static class ExportFormatExtensions
{
    public static string ToDisplayString(this ExportFormat format) =>
        format switch
        {
            ExportFormat.Markdown => "Markdown",
            ExportFormat.Html => "HTML",
            _ => format.ToString()
        };

    public static string GetFileExtension(this ExportFormat format) =>
        format switch
        {
            ExportFormat.Markdown => ConfigurationConstants.MarkdownFileNameExtension,
            ExportFormat.Html => ConfigurationConstants.HtmlFileNameExtension,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    public static string ToExportVerb(this ExportFormat format) =>
        format switch
        {
            ExportFormat.Markdown => "markdown",
            ExportFormat.Html => "HTML",
            _ => format.ToString()
        };
}
