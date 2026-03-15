using System.Text.RegularExpressions;

namespace FileSorter.Business.DirectoryManagers
{
    public sealed class XboxDirectoryManager : IDirectoryManager
    {
        private readonly IPath _path;

        public XboxDirectoryManager(IPath path)
        {
            _path = path;
        }

        public string GetFolderDestination(string destination, IReadonlyFileInfo fileInfo)
        {
            var gameAndDate = fileInfo.Name.Split('-');

            var gamePart = new string(gameAndDate[0].Trim().Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            gamePart = Regex.Replace(gamePart, @"\s+", " ");

            var subFolder = "Clips";
            if (ExtensionTypes.IMAGE_EXTENSIONS.Contains(fileInfo.Extension.ToLower()))
            {
                subFolder = "Screenshots";
            }
            var combined = Path.Combine(gamePart, subFolder);
            return _path.Combine(destination, combined);
        }

        public string GetNewFileName(string folderDestination, IReadonlyFileInfo fileInfo)
        {
            return _path.Combine(folderDestination, Path.GetFileName(fileInfo.FullName));
        }
    }
}