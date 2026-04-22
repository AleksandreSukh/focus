using Newtonsoft.Json;
using Systems.Sanity.Focus.Domain;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Tests;

public class CommandLanguageExtensionsTests
{
    [Fact]
    public void Configure_WithNoTranslations_LeavesInputsUnchanged()
    {
        CommandLanguageExtensions.Configure(null);

        Assert.False(CommandLanguageExtensions.IsOtherLanguage("ა"));
        Assert.Equal("alpha", "alpha".ToCommandLanguage());
        Assert.Equal("alpha", "alpha".ToLocalLanguage());
        Assert.Equal(new[] { "exit" }, new[] { "exit" }.WithLocalizations().ToArray());
    }

    [Fact]
    public void Configure_WithGeorgianTranslation_TranslatesInBothDirections()
    {
        using var translationScope = TranslationTestScope.UseGeorgian();
        var localized = "alpha".ToLocalLanguage();

        Assert.True(CommandLanguageExtensions.IsOtherLanguage(localized));
        Assert.Equal("alpha", localized.ToCommandLanguage());
        Assert.Equal(localized, "alpha".ToLocalLanguage());
    }

    [Fact]
    public void ToCommandKey_NormalizesEnglishAndLocalizedUppercaseInput()
    {
        using var translationScope = new TranslationTestScope(
            TranslationTestScope.CreateTranslation("caps-test", new Dictionary<string, string>
            {
                ["ä"] = "a",
                ["ö"] = "b"
            }));

        Assert.Equal("exit", "EXIT".ToCommandKey());
        Assert.Equal("ab", "ÄÖ".ToCommandKey());
    }

    [Fact]
    public void Configure_UsesConfiguredTranslationOrder()
    {
        using var translationScope = new TranslationTestScope(
            TranslationTestScope.CreateTranslation("first", new Dictionary<string, string> { ["ა"] = "q" }),
            TranslationTestScope.CreateTranslation("second", new Dictionary<string, string> { ["ა"] = "a" }));

        Assert.Equal("q", "ა".ToCommandLanguage());
    }

    [Fact]
    public void Configure_IgnoresMalformedCharacterPairs()
    {
        using var translationScope = new TranslationTestScope(
            TranslationTestScope.CreateTranslation("mixed", new Dictionary<string, string>
            {
                ["აბ"] = "a",
                ["ა"] = "ab",
                ["ბ"] = "b"
            }));

        Assert.False(CommandLanguageExtensions.IsOtherLanguage("ა"));
        Assert.Equal("b", "ბ".ToCommandLanguage());
        Assert.Equal("ბ", "b".ToLocalLanguage());
    }

    [Fact]
    public void WithLocalizations_ReturnsVariantForEachConfiguredTranslation()
    {
        using var translationScope = new TranslationTestScope(
            TranslationTestScope.CreateTranslation("ka-GE", new Dictionary<string, string> { ["ა"] = "a" }),
            TranslationTestScope.CreateTranslation("test-alt", new Dictionary<string, string> { ["ß"] = "a" }));

        Assert.Equal(new[] { "a", "ა", "ß" }, new[] { "a" }.WithLocalizations().ToArray());
    }

    [Fact]
    public void UserConfig_DeserializesWithoutTranslations()
    {
        var config = JsonConvert.DeserializeObject<UserConfig>(
            """
            {
              "dataFolder": "C:\\FocusData"
            }
            """);

        Assert.NotNull(config);
        Assert.Null(config!.Translations);
    }

    [Fact]
    public void UserConfig_DeserializesConfiguredTranslations()
    {
        var config = JsonConvert.DeserializeObject<UserConfig>(
            """
            {
              "dataFolder": "C:\\FocusData",
              "gitRepository": "",
              "translations": [
                {
                  "languageIdentifier": "ka-GE",
                  "characterDictionary": {
                    "ა": "a",
                    "ბ": "b"
                  }
                }
              ]
            }
            """);

        Assert.NotNull(config);
        Assert.NotNull(config!.Translations);
        Assert.Single(config.Translations);
        Assert.Equal("ka-GE", config.Translations[0].LanguageIdentifier);
        Assert.Equal("a", config.Translations[0].CharacterDictionary!["ა"]);
    }
}
