using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Systems.Sanity.Focus.Infrastructure.Input;

public static class CommandLanguageExtensions
{
    private static TranslationMapping[] _translations = Array.Empty<TranslationMapping>();

    public static void Configure(IEnumerable<TranslationDto>? translations)
    {
        _translations = translations?
            .Select(CreateTranslationMapping)
            .Where(mapping => mapping != null)
            .Select(mapping => mapping!)
            .ToArray()
            ?? Array.Empty<TranslationMapping>();
    }

    public static bool IsOtherLanguage(string input) => FindMatchingTranslation(input) != null;

    public static string ToCommandLanguage(this string maybeOtherLanguage)
    {
        var translation = FindMatchingTranslation(maybeOtherLanguage);
        return translation == null
            ? maybeOtherLanguage
            : Translate(maybeOtherLanguage, translation.CommandDictionary);
    }

    public static string ToCommandKey(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var normalizedInput = input.ToLowerInvariant();
        var translation = FindMatchingTranslation(normalizedInput);
        return (translation == null
                ? normalizedInput
                : Translate(normalizedInput, translation.CommandDictionary))
            .ToLowerInvariant();
    }

    public static string ToLocalLanguage(this string input)
    {
        var translation = _translations.FirstOrDefault();
        return translation == null
            ? input
            : Translate(input, translation.LocalDictionary);
    }

    public static IEnumerable<string> WithLocalizations(this IEnumerable<string> source) =>
        source.Select(option => new[] { option }.Concat(_translations.Select(translation => Translate(option, translation.LocalDictionary))))
            .SelectMany(i => i)
            .Distinct();

    private static TranslationMapping? CreateTranslationMapping(TranslationDto? translation)
    {
        if (translation?.CharacterDictionary == null || translation.CharacterDictionary.Count == 0)
            return null;

        var commandDictionary = new Dictionary<char, char>();
        var localDictionary = new Dictionary<char, char>();

        foreach (var pair in translation.CharacterDictionary)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) ||
                string.IsNullOrWhiteSpace(pair.Value) ||
                pair.Key.Length != 1 ||
                pair.Value.Length != 1)
            {
                continue;
            }

            var sourceCharacter = pair.Key[0];
            var targetCharacter = pair.Value[0];

            if (!commandDictionary.ContainsKey(sourceCharacter))
                commandDictionary.Add(sourceCharacter, targetCharacter);

            if (!localDictionary.ContainsKey(targetCharacter))
                localDictionary.Add(targetCharacter, sourceCharacter);
        }

        return commandDictionary.Count == 0
            ? null
            : new TranslationMapping(commandDictionary, localDictionary);
    }

    private static TranslationMapping? FindMatchingTranslation(string input)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        var firstCharacter = input.FirstOrDefault();
        return _translations.FirstOrDefault(translation => translation.CommandDictionary.ContainsKey(firstCharacter));
    }

    private static string Translate(string input, IReadOnlyDictionary<char, char> dictionary)
    {
        var resultStringBuilder = new StringBuilder(input.Length);
        for (var index = 0; index < input.Length; index++)
        {
            var symbol = input[index];
            if (dictionary.TryGetValue(symbol, out var translatedSymbol))
                symbol = translatedSymbol;

            resultStringBuilder.Append(symbol);
        }

        return resultStringBuilder.ToString();
    }

    private sealed class TranslationMapping
    {
        public TranslationMapping(
            Dictionary<char, char> commandDictionary,
            Dictionary<char, char> localDictionary)
        {
            CommandDictionary = commandDictionary;
            LocalDictionary = localDictionary;
        }

        public Dictionary<char, char> CommandDictionary { get; }

        public Dictionary<char, char> LocalDictionary { get; }
    }
}
