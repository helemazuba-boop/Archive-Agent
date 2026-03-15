namespace FileSorter.Business
{
    public class DirectoryOrganiserConsoleView : IDirectoryOrganiserView
    {
        public Task ShowStartCopy(IReadonlyFileInfo fileInfo)
        {
            return Console.Out.WriteLineAsync($"Copying: '{fileInfo.Name}'. Size: '{fileInfo.Length}'.");
        }

        public Task ShowEndCopy(IReadonlyFileInfo fileInfo)
        {
            return Console.Out.WriteLineAsync($"Finished copying: '{fileInfo.Name}'. Size: '{fileInfo.Length}'.");
        }

        public Task ShowDuplicateError(IReadonlyFileInfo fileInfo)
        {
            return Console.Error.WriteLineAsync($"Error: Tried to copy duplicate file '{fileInfo.Name}'.");
        }

        public Task ShowWarning()
        {
            return Console.Error.WriteLineAsync("There were some errors. Check the log for details.");
        }
    }
}