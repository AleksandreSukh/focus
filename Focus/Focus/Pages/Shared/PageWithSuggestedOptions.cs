using Systems.Sanity.Focus.Infrastructure;

namespace Systems.Sanity.Focus.Pages.Shared
{
    public abstract class PageWithSuggestedOptions : Page
    {
        protected ConsoleInput GetCommand(string prompt = "") => GetInput(prompt);
    }
}