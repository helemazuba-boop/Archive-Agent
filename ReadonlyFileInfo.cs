namespace FileSorter.Business
{
    public class ReadonlyFileInfo : IReadonlyFileInfo
    {
        public string Extension { get; private set; }
        public string FullName { get; private set; }
        public string Name { get; private set; }
        public DateTime LastWriteTime { get; private set; }
        public long Length { get; private set; }

        public ReadonlyFileInfo(FileInfo fileInfo)
        {
            Extension = fileInfo.Extension;
            FullName = fileInfo.FullName;
            Name = fileInfo.Name;
            LastWriteTime = fileInfo.LastWriteTime;
            Length = fileInfo.Length;
        }
    }
}