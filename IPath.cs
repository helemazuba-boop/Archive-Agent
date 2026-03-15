namespace FileSorter.Business
{
    public interface IPath
    {
        string Combine(params string[] paths);
    }
}