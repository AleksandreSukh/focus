#nullable enable

using System.Collections.Generic;

namespace Systems.Sanity.Focus.Infrastructure.Input;

public sealed class TranslationDto
{
    public string? LanguageIdentifier { get; set; }

    public Dictionary<string, string>? CharacterDictionary { get; set; }
}
