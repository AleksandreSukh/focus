#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace Systems.Sanity.Focus.Domain;

public interface IMapRepository
{
    string UserMindMapsDirectory { get; }

    FileInfo[] GetTop(int top);

    FileInfo[] GetAll();

    MindMap OpenMap(string filePath, ISet<Guid>? usedIdentifiers = null);

    MindMap OpenMapForEditing(string filePath);

    void SaveMap(string filePath, MindMap map);

    void DeleteMap(FileInfo file, MapDeletionMode deletionMode = MapDeletionMode.DeleteAttachments);

    void MoveMap(string existingFilePath, string newFilePath);
}
