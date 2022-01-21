namespace Systems.Sanity.Focus
{
    public abstract class PageWithSuggestedOptions : Page
    {
        protected ConsoleInput GetCommand(string prompt = "") => GetInput(prompt);
    }
}