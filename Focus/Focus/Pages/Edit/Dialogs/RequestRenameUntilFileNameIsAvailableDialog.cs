using System;
using System.IO;
using Systems.Sanity.Focus.Infrastructure.Input.ReadLine;
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

			var filePath = MapFileHelper.GetFullFilePath(dir, fileName, _fileExtension);

			var existingFileWithThisNameCounter = 2;
			while (File.Exists(filePath))
			{
				var fileNameToAlter = Path.GetFileNameWithoutExtension(filePath);
				var suggestedFileName = $"{fileNameToAlter}_({existingFileWithThisNameCounter})";

				var newName =
					ReadLine.Read(
						$"File: {fileNameToAlter} already exists. Use suggested name {suggestedFileName} by pressing Enter or type new name below{Environment.NewLine}>");

				var newFileName = !string.IsNullOrWhiteSpace(newName)
					? newName
					: suggestedFileName;

				filePath = MapFileHelper.GetFullFilePath(dir, newFileName, _fileExtension);
				existingFileWithThisNameCounter++;
			}

			return filePath;
		}
	}
}
