using Systems.Sanity.Focus.Infrastructure.Diagnostics;

namespace Systems.Sanity.Focus.Tests;

public class ExceptionDiagnosticsTests
{
    [Fact]
    public void LogException_AppendsTimestampedEntriesWithActionAndFullExceptionText()
    {
        using var diagnosticsScope = new ExceptionDiagnosticsScope();

        ExceptionDiagnostics.LogException(
            new InvalidOperationException("first failure"),
            "starting application");
        ExceptionDiagnostics.LogException(
            new ArgumentException("second failure"),
            "loading configuration");

        var log = diagnosticsScope.ReadLog();

        Assert.Contains("Timestamp: ", log);
        Assert.Contains("Action: starting application", log);
        Assert.Contains("Action: loading configuration", log);
        Assert.Contains("System.InvalidOperationException: first failure", log);
        Assert.Contains("System.ArgumentException: second failure", log);
        Assert.True(
            log.IndexOf("Action: starting application", StringComparison.InvariantCulture) <
            log.IndexOf("Action: loading configuration", StringComparison.InvariantCulture));
    }
}
