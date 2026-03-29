using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Infrastructure.Input;
using Systems.Sanity.Focus.Pages;

namespace Systems.Sanity.Focus.Tests;

public class HomePageTests
{
    [Fact]
    public void Show_LocalizedFileNameInput_OpensSelectedMap()
    {
        var navigator = new RecordingPageNavigator();
        using var workspace = new TestWorkspace(navigator);
        using var translationScope = TranslationTestScope.UseGeorgian();
        var filePath = workspace.SaveMap("alpha", new MindMap("Alpha"));
        var localizedFileName = "alpha".ToLocalLanguage();

        using var consoleScope = new AppConsoleScope(new ScriptedConsoleSession(localizedFileName, "exit"));
        var page = new HomePage(workspace.AppContext);

        page.Show();

        Assert.Equal(filePath, navigator.OpenedEditMapFilePath);
    }
}
