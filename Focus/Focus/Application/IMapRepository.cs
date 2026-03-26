#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus.Application;

public interface IMapRepository
{
    string UserMindMapsDirectory { get; }

    FileInfo[] GetTop(int top);

    FileInfo[] GetAll();

    MindMap OpenMap(string filePath, ISet<Guid>? usedIdentifiers = null);

    void SaveMap(string filePath, MindMap map);

    void DeleteMap(FileInfo file);

    void MoveMap(string existingFilePath, string newFilePath);
}
