namespace FileSorter.Business
{
    public interface IStreamFactory
    {
        Stream CreateSourceStream(string fileNameLocation);
        Stream CreateDestinationStream(string fileNameLocation);
    }
}