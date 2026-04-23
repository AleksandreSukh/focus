using Systems.Sanity.Focus.DomainServices;

namespace Systems.Sanity.Focus.Pages.Shared.DialogHelpers;

public static class ContentPeek
{
    public static string GetContentPeek(this string fullContent)
    {
        return NodeDisplayHelper.GetContentPeek(fullContent);
    }
}
