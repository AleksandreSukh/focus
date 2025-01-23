namespace Systems.Sanity.Focus.Infrastructure.Input.ReadLine
{
    public interface IAutoCompleteHandler
    {
        char[] Separators { get; set; }
        string[] GetSuggestions(string text, int index);
        void BeforeAutoComplete();
        void AfterAutoComplete();
    }
}