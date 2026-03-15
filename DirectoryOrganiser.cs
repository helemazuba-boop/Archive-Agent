using FileSorter.Business.DirectoryManagers;

namespace FileSorter.Business
{
    public class DirectoryOrganiser
    {
        private readonly IDirectoryManager _directoryManager;
        private readonly IStreamFactory _streamFactory;
        private readonly IDirectoryOrganiserView _view;

        public DirectoryOrganiser(IDirectoryManager directoryManager, IStreamFactory streamFactory, IDirectoryOrganiserView view)
        {
            _directoryManager = directoryManager;
            _streamFactory = streamFactory;
            _view = view;
        }

        public async Task Organise(string[] files, string destination)
        {
            var showWarning = false;
            await Parallel.ForEachAsync(files, async (file, token) =>
            {
                var fileInfo = new ReadonlyFileInfo(new FileInfo(file));
                await _view.ShowStartCopy(fileInfo);

                var folderDestination = _directoryManager.GetFolderDestination(destination, fileInfo);

                var newName = _directoryManager.GetNewFileName(folderDestination, fileInfo);
                if (File.Exists(newName)) return;

                FileDirectory.CreateDirectoryIfNew(folderDestination);

                try
                {
                    using var sourceStream = _streamFactory.CreateSourceStream(file);
                        using var destinationStream = _streamFactory.CreateDestinationStream(newName);
                            await sourceStream.CopyToAsync(destinationStream, token);
                }
                catch (IOException)
                {
                    await _view.ShowDuplicateError(fileInfo);
                    showWarning = true;
                }

                await _view.ShowEndCopy(fileInfo);
            });

            if (showWarning) await _view.ShowWarning();
        }
    }
}
