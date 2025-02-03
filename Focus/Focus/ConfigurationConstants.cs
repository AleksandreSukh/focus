namespace Systems.Sanity.Focus;

//TODO: move to configuration file
public class ConfigurationConstants
{
    public const string MindMapDirectoryName = "FocusMaps";
    public const string CommandColor = "green";
    public const string GithubPublishRepositoryUrl = "https://github.com/AleksandreSukh/focus";
    public const string RequiredFileNameExtension = ".json";
    public static class NodePrinting
    {
        public const string LeftBorder = "| ";
        public const string LeftBorderAtTheEndOfBranch = ":";
        public const string TabSpaceForIndentation = "    ";
    }
}