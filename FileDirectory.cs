namespace FileSorter.Business
{
    public static class FileDirectory
    {
        public static DirectoryInfo CreateDirectoryIfNew(string path)
        {
            if (!Directory.Exists(path))
            {
                return Directory.CreateDirectory(path);
            }
            return new DirectoryInfo(path);
        }
    }
}