namespace FileSorter.Business.DirectoryManagers
{
    public sealed class FileExtensionDirectoryManager : IDirectoryManager
    {
        private readonly IPath _path;

        public FileExtensionDirectoryManager(IPath path)
        {
            _path = path;
        }

        public string GetFolderDestination(string destination, IReadonlyFileInfo fileInfo)
        {
            var folder = fileInfo.Extension[1..];

            return _path.Combine(destination, folder.ToLower());
        }

        public string GetNewFileName(string folderDestination, IReadonlyFileInfo fileInfo)
        {
            return _path.Combine(folderDestination, Path.GetFileName(fileInfo.FullName));
        }
    }
}