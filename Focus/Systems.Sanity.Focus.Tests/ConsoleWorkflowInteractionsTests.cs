using Systems.Sanity.Focus.Application.WorkflowInteractions;

namespace Systems.Sanity.Focus.Tests;

public class ConsoleWorkflowInteractionsTests
{
    [Fact]
    public void FormatVoiceRecordingStatus_AtStart_ShowsEmptyProgress()
    {
        var status = ConsoleWorkflowInteractions.FormatVoiceRecordingStatus(
            TimeSpan.Zero,
            TimeSpan.FromMinutes(5),
            reachedLimit: false);

        Assert.Equal(
            "Recording voice note [------------------------] 00:00 / 05:00 0%. Press Enter to save or Esc to cancel.",
            status);
    }

    [Fact]
    public void FormatVoiceRecordingStatus_Halfway_ShowsHalfProgress()
    {
        var status = ConsoleWorkflowInteractions.FormatVoiceRecordingStatus(
            TimeSpan.FromMinutes(2.5),
            TimeSpan.FromMinutes(5),
            reachedLimit: false);

        Assert.Equal(
            "Recording voice note [############------------] 02:30 / 05:00 50%. Press Enter to save or Esc to cancel.",
            status);
    }

    [Fact]
    public void FormatVoiceRecordingStatus_PastLimit_ClampsToFullProgress()
    {
        var status = ConsoleWorkflowInteractions.FormatVoiceRecordingStatus(
            TimeSpan.FromMinutes(6),
            TimeSpan.FromMinutes(5),
            reachedLimit: false);

        Assert.Equal(
            "Recording voice note [########################] 05:00 / 05:00 100%. Press Enter to save or Esc to cancel.",
            status);
    }

    [Fact]
    public void FormatVoiceRecordingStatus_ReachedLimit_UsesSavingText()
    {
        var status = ConsoleWorkflowInteractions.FormatVoiceRecordingStatus(
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5),
            reachedLimit: true);

        Assert.Equal(
            "Recording voice note [########################] 05:00 / 05:00 100%. 5 minute limit reached. Saving...",
            status);
    }
}
