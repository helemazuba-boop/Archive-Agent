namespace FileSorter.Business
{
    public class CrossPlatformPath : IPath
    {
        public string Combine(params string[] paths)
        {
            return Path.Combine(paths).Replace('\\', '/');
        }
    }
}
