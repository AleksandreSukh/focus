namespace Systems.Sanity.Focus.Infrastructure
{
    public sealed class CommandExecutionResult
    {
        public bool ShouldExit { get; private set; }
        public bool IsSuccess { get; private set; }
        public string ErrorString { get; private set; }

        private CommandExecutionResult(string errorString)
        {
            ErrorString = errorString;
        }

        private CommandExecutionResult() { }

        public static readonly CommandExecutionResult Success = new() { IsSuccess = true };

        public static readonly CommandExecutionResult ExitCommand = new() { ShouldExit = true };

        public static CommandExecutionResult Error(string errorString) => new(errorString);
    }
}