#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.DomainServices;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared.Dialogs;

namespace Systems.Sanity.Focus.Pages.Shared;

internal class SearchResultsPage : Page
{
    private readonly IReadOnlyList<NodeSearchResult> _results;
    private readonly string _title;
    private readonly bool _includeMapName;

    public SearchResultsPage(IReadOnlyList<NodeSearchResult> results, string title, bool includeMapName)
    {
        _results = results;
        _title = title;
        _includeMapName = includeMapName;
    }

    public override void Show()
    {
        SelectResult();
    }

    public NodeSearchResult? SelectResult()
    {
        while (true)
        {
            AppConsole.Current.Clear();
            ColorfulConsole.WriteLine(BuildResultsScreen());

            var input = GetInput("Type result number to open or press Enter to cancel").InputString;
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (int.TryParse(input, out var resultNumber) &&
                resultNumber >= 1 &&
                resultNumber <= _results.Count)
            {
                return _results[resultNumber - 1];
            }
        }
    }

    protected override IEnumerable<string> GetPageSpecificSuggestions(string text, int index)
    {
        return Enumerable.Range(1, _results.Count)
            .Select(number => number.ToString());
    }

    private string BuildResultsScreen()
    {
        var messageBuilder = new StringBuilder();
        messageBuilder.AppendLine();
        messageBuilder.AppendLineCentered($"*** {_title} ***");
        messageBuilder.AppendLine();

        for (var index = 0; index < _results.Count; index++)
        {
            messageBuilder.AppendLine($"{index + 1}. {_results[index].ToDisplayString(_includeMapName)}");
        }

        messageBuilder.AppendLine();
        return messageBuilder.ToString();
    }
}
