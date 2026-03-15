namespace FileSorter.Business
{
    public class FileStreamFactory : IStreamFactory
    {
        public Stream CreateSourceStream(string fileNameLocation)
        {
            return new FileStream(fileNameLocation, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        }

        public Stream CreateDestinationStream(string fileNameLocation)
        {
            return new FileStream(fileNameLocation, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        }
    }
}
