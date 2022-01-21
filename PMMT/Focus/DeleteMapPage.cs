using System.IO;

namespace Systems.Sanity.Focus
{
    internal class DeleteMapPage : PageWithExclusiveOptions
    {
        private const string YesOption = "yes";
        private const string NoOption = "no";

        private readonly FileInfo _file;

        public DeleteMapPage(FileInfo file)
        {
            _file = file;
        }

        public override void Show()
        {
            var yesNoResult = GetCommand($"Are you sure you want to delete: \"{_file.Name}\"?").FirstWord;
            switch (yesNoResult)
            {
                case YesOption:
                    _file.Delete();
                    break;
                case NoOption:
                    break;
            }
        }

        protected override string[] GetCommandOptions()
        {
            return new[] { YesOption, NoOption };
        }
    }
}