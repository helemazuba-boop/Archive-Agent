namespace FileSorter.Business.DirectoryManagers
{
    public sealed class DateTimeDirectoryManager : IDirectoryManager
    {
        private readonly IPath _path;

        public DateTimeDirectoryManager(IPath path)
        {
            _path = path;
        }

        public string GetFolderDestination(string destination, IReadonlyFileInfo fileInfo)
        {
            var folder = fileInfo.LastWriteTime.Year.ToString();
            return _path.Combine(destination, folder);
        }

        public string GetNewFileName(string folderDestination, IReadonlyFileInfo fileInfo)
        {
            return _path.Combine(folderDestination, $"{fileInfo.LastWriteTime:dd MMM yyyy HHmmss} [{Path.GetFileNameWithoutExtension(fileInfo.Name)}]{fileInfo.Extension}");
        }
    }
}