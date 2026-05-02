#nullable enable

namespace Systems.Sanity.Focus.Application.EditCommands;

internal static class EditCommandHandlerRegistry
{
    public static EditCommandDispatcher CreateDefault() =>
        new(new IEditCommandFeatureHandler[]
        {
            new EditNavigationCommandHandler(),
            new EditNodeCommandHandler(),
            new EditTaskCommandHandler(),
            new EditLinkCommandHandler(),
            new EditSearchCommandHandler(),
            new EditAttachmentMetadataCommandHandler(),
            new EditCaptureCommandHandler(),
            new EditExportCommandHandler(),
            new EditSystemCommandHandler()
        });
}
