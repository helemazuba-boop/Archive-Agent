namespace FileSorter.Business.DirectoryManagers
{
    public sealed class AlphabetDirectoryManager : IDirectoryManager
    {
        private const string OTHER = "Other";
        private readonly IPath _path;

        public AlphabetDirectoryManager(IPath path)
        {
            _path = path;
        }

        public string GetFolderDestination(string destination, IReadonlyFileInfo fileInfo)
        {
            if (!char.IsLetter(fileInfo.Name[0])) return _path.Combine(destination, OTHER);

            var folder = fileInfo.Name[0].ToString().ToUpper();

            return _path.Combine(destination, folder);
        }

        public string GetNewFileName(string folderDestination, IReadonlyFileInfo fileInfo)
        {
            return _path.Combine(folderDestination, Path.GetFileName(fileInfo.FullName));
        }
    }
}