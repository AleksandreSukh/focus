namespace Systems.Sanity.Focus.Pages.Shared.DialogHelpers;

public static class ContentPeek
{
    public static string GetContentPeek(this string fullContent)
    {
        const int peekContentLength = 32;

        var fullContentLength = fullContent.Length;
        var contentPeek = fullContentLength <= peekContentLength
            ? fullContent
            : fullContent.Substring(0, peekContentLength) + "...";

        return contentPeek;
    }
}