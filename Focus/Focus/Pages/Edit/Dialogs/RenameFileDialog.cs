#nullable enable

using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Infrastructure;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs;

internal class RenameFileDialog : Page
{
	private readonly FileInfo _existingFile;

	public RenameFileDialog(FileInfo existingFile)
	{
		_existingFile = existingFile;
	}

	public override void Show()
	{
		var existingFilePath = MapFileHelper.GetFullFilePath(_existingFile.DirectoryName, _existingFile.Name);
		var fileExtension = _existingFile.Extension;
		var newFileName = GetInput("Enter new name here: ", _existingFile.NameWithoutExtension())?.InputString;

		if (newFileName == null)
			return;

		newFileName += fileExtension;

		while (FileNameIsInvalid(newFileName))
		{
			newFileName = GetInput($"FileName {newFileName} contains invalid characters. Enter new name: ")?
				.InputString;
		}

		if (newFileName == null)
			return;

		if (_existingFile.Name != newFileName)
			new RequestRenameUntilFileNameIsAvailableDialog(_existingFile.DirectoryName, newFileName,
					filePath => File.Move(existingFilePath, filePath))
				.Show();
	}

	private static bool FileNameIsInvalid(string? newFileName)
	{
		if (newFileName == null)
			return true;
		var newFileNameChars = newFileName.Distinct().ToHashSet();
		return Path.GetInvalidFileNameChars().Any(ic => newFileNameChars.Contains(ic));
	}
}
