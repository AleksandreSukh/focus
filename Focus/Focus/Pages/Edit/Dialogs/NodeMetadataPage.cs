#nullable enable

using System;
using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal sealed class NodeMetadataPage : Page
{
    private readonly Node _node;
    private readonly string _title;

    public NodeMetadataPage(Node node, string title)
    {
        _node = node;
        _title = title;
    }

    public override void Show()
    {
        AppConsole.Current.Clear();
        ColorfulConsole.WriteLine(BuildScreen());
        AppConsole.Current.ReadKey();
    }

    private string BuildScreen()
    {
        var builder = new StringBuilder();
        var metadata = _node.Metadata;

        builder.AppendLine();
        builder.AppendLineCentered($"*** {_title} ***");
        builder.AppendLine();
        builder.AppendLine($"Node: {NodeDisplayHelper.GetSingleLinePreview(_node.Name)}");

        if (metadata == null)
        {
            builder.AppendLine("Metadata: unavailable");
        }
        else
        {
            builder.AppendLine($"CreatedAtUtc: {metadata.CreatedAtUtc:O}");
            builder.AppendLine($"UpdatedAtUtc: {metadata.UpdatedAtUtc:O}");
            builder.AppendLine($"Source: {metadata.Source ?? "(none)"}");
            builder.AppendLine($"Device: {metadata.Device ?? "(none)"}");
            builder.AppendLine($"Attachments: {metadata.Attachments.Count}");
        }

        builder.AppendLine();
        builder.AppendLineCentered("Press any key to continue");
        builder.AppendLine();
        return builder.ToString();
    }
}
