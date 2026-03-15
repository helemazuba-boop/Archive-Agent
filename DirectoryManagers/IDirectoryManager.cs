namespace FileSorter.Business.DirectoryManagers
{
    public interface IDirectoryManager
    {
        string GetFolderDestination(string destination, IReadonlyFileInfo fileInfo);
        string GetNewFileName(string folderDestination, IReadonlyFileInfo fileInfo);
    }
}