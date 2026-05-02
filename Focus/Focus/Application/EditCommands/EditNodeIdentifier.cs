#nullable enable

using System;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Application.EditCommands;

internal static class EditNodeIdentifier
{
    public static bool InvokeLocalized(Func<string, bool> action, string parameters) =>
        action(parameters) || action(parameters.ToCommandKey());
}
