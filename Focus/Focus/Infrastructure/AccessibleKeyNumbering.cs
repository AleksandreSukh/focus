using System.Collections.Generic;
using System.Linq;

namespace Systems.Sanity.Focus.Infrastructure
{
    public class AccessibleKeyNumbering
    {
        private const string AlphabetLeft = "asdf";
        private const string AlphabetRight = "jkl;";

        public const int MaxShortcutStringLength = 3;

        private static readonly string[] SpecificWords = new[]
        {
            "ja",
            "ka",
            "fa",
            "ad",
            "da",
            "al",
            "as",
            "la",
            "jak",
            "kaf",
            "dak",
            "aks",
            "ask",
            "fad",
            "kas",
            "ska",
            "alf",
            "fas",
            "ads",
            "dal",
            "das",
            "lad",
            "lsd",
            "sad",
            "als",
            "las",
            "sal"
        };

        private static readonly Dictionary<int, string> NumbersToStrings =
            Enumerable.Range(1, SpecificWords.Length)
                .ToDictionary(k => k, k => SpecificWords[k - 1]);

        private static readonly Dictionary<string, int> StringsToNumers =
            NumbersToStrings.Keys
                .ToDictionary(k => NumbersToStrings[k], k => k);

        public static int GetNumberFor(string input) => 
            StringsToNumers.TryGetValue(input, out int result) ? result : 0;
        public static string GetStringFor(int input) => 
            NumbersToStrings.TryGetValue(input, out string result) ? result : null;
    }
}
