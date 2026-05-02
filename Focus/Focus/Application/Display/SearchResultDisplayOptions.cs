#nullable enable

using System;
using System.Collections.Generic;

namespace Systems.Sanity.Focus.Application.Display;

internal sealed record SearchResultDisplayOptions
{
    public SearchResultDisplayOptions(
        bool includeMapName,
        bool colorizeAncestorPath,
        IReadOnlyList<string>? highlightTerms = null)
    {
        IncludeMapName = includeMapName;
        ColorizeAncestorPath = colorizeAncestorPath;
        HighlightTerms = highlightTerms ?? Array.Empty<string>();
    }

    public bool IncludeMapName { get; }

    public bool ColorizeAncestorPath { get; }

    public IReadOnlyList<string> HighlightTerms { get; }
}
