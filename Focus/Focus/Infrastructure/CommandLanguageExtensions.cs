using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Systems.Sanity.Focus.Infrastructure
{
    [Obsolete("Needs to be refactored")]
    public static class CommandLanguageExtensions
    {
        private static readonly Dictionary<char, char> GeorgianMappingDict;
        private static readonly Dictionary<char, char> ReverseGeorgianMappingDict;
        static CommandLanguageExtensions()
        {
            var GeorgianAlphabetMapping = "აბცდეფგჰიჯკლმნოპქრსტუვწხყზ";
            GeorgianMappingDict = new Dictionary<char, char>();
            ReverseGeorgianMappingDict = new Dictionary<char, char>();
            for (int i = 0; i < GeorgianAlphabetMapping.Length; i++)
            {
                var mapped = (char)('a' + i);
                GeorgianMappingDict.Add(GeorgianAlphabetMapping[i], mapped);
                ReverseGeorgianMappingDict.Add(mapped, GeorgianAlphabetMapping[i]);
            }
        }

        public static bool IsOtherLanguage(string input) => GeorgianMappingDict.ContainsKey(input.FirstOrDefault());

        public static string ToCommandLanguage(this string maybeOtherLanguage)
        {
            var resultStringBuilder = new StringBuilder(maybeOtherLanguage.Length);
            for (var index = 0; index < maybeOtherLanguage.Length; index++)
            {
                var symbol = maybeOtherLanguage[index];
                if (GeorgianMappingDict.ContainsKey(symbol))
                    symbol = GeorgianMappingDict[symbol];
                resultStringBuilder.Append(symbol);
            }

            return resultStringBuilder.ToString();
        } 
        
        public static string ToLocalLanguage(this string input)
        {
            var resultStringBuilder = new StringBuilder(input.Length);
            for (var index = 0; index < input.Length; index++)
            {
                var symbol = input[index];
                if (ReverseGeorgianMappingDict.ContainsKey(symbol))
                    symbol = ReverseGeorgianMappingDict[symbol];
                resultStringBuilder.Append(symbol);
            }

            return resultStringBuilder.ToString();
        }

        public static IEnumerable<string> WithLocalizations(this IEnumerable<string> source) =>
            source.Select(option => new[] { option, option.ToLocalLanguage() })
                .SelectMany(i => i);
    }
}