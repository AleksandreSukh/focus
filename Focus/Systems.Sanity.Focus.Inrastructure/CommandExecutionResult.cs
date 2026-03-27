#nullable enable

namespace Systems.Sanity.Focus.Infrastructure;

public sealed class CommandExecutionResult
{
    public bool ShouldExit { get; private init; }

    public bool ShouldPersist { get; private init; }

    public bool IsSuccess { get; private init; }

    public string? ErrorString { get; private init; }

    public string? Message { get; private init; }

    public string? SyncCommitMessage { get; private init; }

    public static CommandExecutionResult ExitCommand { get; } = new() { ShouldExit = true };

    public static CommandExecutionResult Error(string errorString) => new() { ErrorString = errorString };

    public static CommandExecutionResult Success(string? message = null) =>
        new() { IsSuccess = true, Message = message };

    public static CommandExecutionResult SuccessAndPersist(string? message = null, string? syncCommitMessage = null) =>
        new() { IsSuccess = true, ShouldPersist = true, Message = message, SyncCommitMessage = syncCommitMessage };
}
