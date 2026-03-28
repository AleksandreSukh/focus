#nullable enable

using System;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application;

internal interface IPageNavigator
{
    void OpenEditMap(string filePath, Guid? initialNodeIdentifier = null);

    void OpenCreateMap(string fileName, MindMap mindMap, string? sourceMapFilePath = null);
}
