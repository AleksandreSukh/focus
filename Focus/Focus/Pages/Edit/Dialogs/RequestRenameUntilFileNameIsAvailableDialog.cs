using System;
using System.IO;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
    internal class RequestRenameUntilFileNameIsAvailableDialog : Page
    {
        private readonly Action<string> _fileAction;
        private readonly string _tartgetDir;
        private string _fileName;

        public RequestRenameUntilFileNameIsAvailableDialog(string tartgetDir, string fileName, Action<string> fileAction)
        {
            _tartgetDir = tartgetDir;
            _fileName = fileName;
            _fileAction = fileAction;
        }

        public override void Show()
        {
            var filePath = GetNewFilePath(_tartgetDir, _fileName);
            _fileAction(filePath);
        }

        public static string GetNewFilePath(string dir, string fileName)
        {
            var parentDir = new DirectoryInfo(dir);
            if (!parentDir.Exists)
                parentDir.Create();

            var filePath = MapFileHelper.GetFullFilePath(dir, fileName);

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

                filePath = MapFileHelper.GetFullFilePath(dir, newFileName);
                existingFileWithThisNameCounter++;
            }

            return filePath;
        }
    }
}