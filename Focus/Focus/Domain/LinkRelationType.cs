using System;

namespace Systems.Sanity.Focus.Domain;

public enum LinkRelationType
{
    Relates,
    Prerequisite,
    TodoWith,
    Causes
}

public static class LinkRelationTypeExtensions
{
    private static readonly LinkRelationType[] SupportedTypes =
    {
        LinkRelationType.Relates,
        LinkRelationType.Prerequisite,
        LinkRelationType.TodoWith,
        LinkRelationType.Causes
    };

    public static LinkRelationType[] GetSupportedTypes() => SupportedTypes;

    public static string ToDisplayString(this LinkRelationType relationType)
    {
        return relationType switch
        {
            LinkRelationType.Relates => "relates",
            LinkRelationType.Prerequisite => "prerequisite",
            LinkRelationType.TodoWith => "todo-with",
            LinkRelationType.Causes => "causes",
            _ => throw new ArgumentOutOfRangeException(nameof(relationType), relationType, null)
        };
    }
}
