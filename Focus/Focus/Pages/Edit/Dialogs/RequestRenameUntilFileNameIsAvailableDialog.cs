using System;
using System.IO;
using Systems.Sanity.Focus.Application;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
	internal class RequestRenameUntilFileNameIsAvailableDialog : Page
	{
		private readonly Action<string> _fileAction;
		private readonly string _tartgetDir;
		private string _fileName;
		private readonly string _fileExtension;

		public RequestRenameUntilFileNameIsAvailableDialog(
			string tartgetDir,
			string fileName,
			Action<string> fileAction,
			string fileExtension = null)
		{
			_tartgetDir = tartgetDir;
			_fileName = fileName;
			_fileAction = fileAction;
			_fileExtension = string.IsNullOrWhiteSpace(fileExtension)
				? ConfigurationConstants.RequiredFileNameExtension
				: fileExtension;
		}

		public override void Show()
		{
			var filePath = GetNewFilePath(_tartgetDir, _fileName);
			_fileAction(filePath);
		}

		string GetNewFilePath(string dir, string fileName)
		{
			var parentDir = new DirectoryInfo(dir);
			if (!parentDir.Exists)
				parentDir.Create();

			var fileNameToTry = fileName;
			var suggestionBaseFileName = fileName;
			var existingFileWithThisNameCounter = 2;
			var filePath = MapFilePathHelper.GetFullFilePath(dir, fileNameToTry, _fileExtension);
			while (File.Exists(filePath))
			{
				var fileNameToAlter = Path.GetFileNameWithoutExtension(filePath);
				var suggestedFileName = $"{suggestionBaseFileName}_({existingFileWithThisNameCounter})";

				var newName =
					AppConsole.Current.CommandLineEditor.Read(
						$"File: {fileNameToAlter} already exists. Use suggested name {suggestedFileName} by pressing Enter or type new name below{Environment.NewLine}>");

				var useSuggestedFileName =
					string.IsNullOrWhiteSpace(newName) ||
					string.Equals(newName, suggestedFileName, StringComparison.Ordinal);

				if (useSuggestedFileName)
				{
					fileNameToTry = suggestedFileName;
					existingFileWithThisNameCounter++;
				}
				else
				{
					fileNameToTry = newName;
					suggestionBaseFileName = newName;
					existingFileWithThisNameCounter = 2;
				}

				filePath = MapFilePathHelper.GetFullFilePath(dir, fileNameToTry, _fileExtension);
			}

			return filePath;
		}
	}
}
