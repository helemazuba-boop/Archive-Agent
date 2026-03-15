namespace FileSorter.Business.DirectoryManagers
{
    public sealed class FileTypeDirectoryManager : IDirectoryManager
    {
        private readonly IPath _path;

        public FileTypeDirectoryManager(IPath path)
        {
            _path = path;
        }

        public string GetFolderDestination(string destination, IReadonlyFileInfo fileInfo)
        {
            var folder = "Other";
            var extension = fileInfo.Extension.ToLower();
            if (ExtensionTypes.IMAGE_EXTENSIONS.Contains(extension))
            {
                folder = "Images";
            }
            if (ExtensionTypes.VIDEO_EXTENSIONS.Contains(extension))
            {
                folder = "Videos";
            }
            if (ExtensionTypes.SOUND_EXTENSIONS.Contains(extension))
            {
                folder = "Sounds";
            }
            if (ExtensionTypes.DOCUMENT_EXTENSIONS.Contains(extension))
            {
                folder = "Documents";
            }
            if (ExtensionTypes.ARCHIVE_EXTENSIONS.Contains(extension))
            {
                folder = "Archive";
            }

            return _path.Combine(destination, folder);
        }

        public string GetNewFileName(string folderDestination, IReadonlyFileInfo fileInfo)
        {
            return _path.Combine(folderDestination, Path.GetFileName(fileInfo.FullName));
        }
    }
}