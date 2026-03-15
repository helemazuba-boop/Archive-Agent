namespace FileSorter.Business.DirectoryManagers
{
    public sealed class LibraryDirectoryManager : IDirectoryManager
    {
        private readonly IPath _path;
        private const string UNKNOWN_AUTHOR = "UnknownAuthor";
        private const string BOOKS_DIRECTORY = "Books";

        public LibraryDirectoryManager(IPath path)
        {
            _path = path;
        }

        public string GetFolderDestination(string destination, IReadonlyFileInfo fileInfo)
        {
            var parts = fileInfo.Name.Split('-', 2);

            var author = parts[0].Trim();

            if (parts.Length == 1)
            {
                author = UNKNOWN_AUTHOR;
            }

            var authorDirectory = _path.Combine(BOOKS_DIRECTORY, author, Path.GetFileNameWithoutExtension(fileInfo.Name));

            return _path.Combine(destination, authorDirectory);
        }

        public string GetNewFileName(string folderDestination, IReadonlyFileInfo fileInfo)
        {
            return _path.Combine(folderDestination, Path.GetFileName(fileInfo.FullName));
        }
    }
}