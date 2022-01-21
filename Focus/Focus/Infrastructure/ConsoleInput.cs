using System.Linq;

namespace Systems.Sanity.Focus
{
    public sealed class ConsoleInput
    {
        private const char InputSeparator = ' ';
        public readonly string InputString;
        public readonly string[] InputWords;
        public readonly string FirstWord;
        public readonly string[] ParameterWords;
        public readonly string Parameters;

        public ConsoleInput(string inputString)
        {
            InputString = inputString;
            InputWords = inputString.Split(InputSeparator);
            FirstWord = InputWords.First();
            ParameterWords = InputWords.Skip(1).ToArray();
            Parameters = string.Join(InputSeparator, ParameterWords);
        }
    }
}