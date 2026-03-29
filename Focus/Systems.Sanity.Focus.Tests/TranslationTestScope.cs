#nullable enable

using System.Collections.Generic;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Tests;

internal sealed class TranslationTestScope : IDisposable
{
    public TranslationTestScope(params TranslationDto[] translations)
    {
        CommandLanguageExtensions.Configure(translations);
    }

    public static TranslationTestScope UseGeorgian() => new(CreateGeorgianTranslation());

    public static TranslationDto CreateGeorgianTranslation()
    {
        return new TranslationDto
        {
            LanguageIdentifier = "ka-GE",
            CharacterDictionary = new Dictionary<string, string>
            {
                ["ა"] = "a",
                ["ბ"] = "b",
                ["ც"] = "c",
                ["დ"] = "d",
                ["ე"] = "e",
                ["ფ"] = "f",
                ["გ"] = "g",
                ["ჰ"] = "h",
                ["ი"] = "i",
                ["ჯ"] = "j",
                ["კ"] = "k",
                ["ლ"] = "l",
                ["მ"] = "m",
                ["ნ"] = "n",
                ["ო"] = "o",
                ["პ"] = "p",
                ["ქ"] = "q",
                ["რ"] = "r",
                ["ს"] = "s",
                ["ტ"] = "t",
                ["უ"] = "u",
                ["ვ"] = "v",
                ["წ"] = "w",
                ["ხ"] = "x",
                ["ყ"] = "y",
                ["ზ"] = "z"
            }
        };
    }

    public static TranslationDto CreateTranslation(
        string languageIdentifier,
        IDictionary<string, string> characterDictionary)
    {
        return new TranslationDto
        {
            LanguageIdentifier = languageIdentifier,
            CharacterDictionary = new Dictionary<string, string>(characterDictionary)
        };
    }

    public void Dispose()
    {
        CommandLanguageExtensions.Configure(null);
    }
}
