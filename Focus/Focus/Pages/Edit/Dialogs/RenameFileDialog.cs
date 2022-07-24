using System.IO;
using System.Linq;
using Systems.Sanity.Focus.Pages.Shared;

namespace Systems.Sanity.Focus.Pages.Edit.Dialogs
{
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
            var newFileName = GetInput("Enter new name here: ").InputString;
            
            while (FileNameIsInvalid(newFileName))
            {
                newFileName = GetInput($"FileName {newFileName} contains invalid characters. Enter new name: ")
                    .InputString;
            }

            new RequestRenameUntilFileNameIsAvailableDialog(_existingFile.DirectoryName, newFileName, 
                filePath => File.Move(existingFilePath, filePath))
                .Show();
        }

        private static bool FileNameIsInvalid(string newFileName)
        {
            var newFileNameChars = newFileName.Distinct().ToHashSet();
            return Path.GetInvalidFileNameChars().Any(ic => newFileNameChars.Contains(ic));
        }
    }
}