#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal sealed class AttachmentSelectionPage : Page
{
    private readonly IReadOnlyList<NodeAttachment> _attachments;
    private readonly string _title;

    public AttachmentSelectionPage(IReadOnlyList<NodeAttachment> attachments, string title)
    {
        _attachments = attachments;
        _title = title;
    }

    public override void Show()
    {
        SelectAttachment();
    }

    public NodeAttachment? SelectAttachment()
    {
        while (true)
        {
            AppConsole.Current.Clear();
            ColorfulConsole.WriteLine(BuildScreen());

            var input = GetInput("Type attachment number to open or press Enter to cancel").InputString;
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (int.TryParse(input, out var attachmentNumber) &&
                attachmentNumber >= 1 &&
                attachmentNumber <= _attachments.Count)
            {
                return _attachments[attachmentNumber - 1];
            }
        }
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index) =>
        Enumerable.Range(1, _attachments.Count)
            .Select(number => number.ToString());

    private string BuildScreen()
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLineCentered($"*** {_title} ***");
        builder.AppendLine();

        for (var index = 0; index < _attachments.Count; index++)
        {
            var attachment = _attachments[index];
            builder.AppendLine(
                $"{index + 1}. {attachment.DisplayName} [{attachment.MediaType}] ({attachment.RelativePath})");
        }

        builder.AppendLine();
        return builder.ToString();
    }
}
