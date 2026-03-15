namespace FileSorter.Business
{
    public interface IReadonlyFileInfo
    {
        string Extension { get; }
        string FullName { get; }
        string Name { get; }
        DateTime LastWriteTime { get; }
        long Length { get; }
    }
}