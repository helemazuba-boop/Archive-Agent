namespace FileSorter.Business
{
    public interface IDirectoryOrganiserView
    {
        Task ShowStartCopy(IReadonlyFileInfo fileInfo);
        Task ShowEndCopy(IReadonlyFileInfo fileInfo);
        Task ShowDuplicateError(IReadonlyFileInfo fileInfo);
        Task ShowWarning();
    }
}