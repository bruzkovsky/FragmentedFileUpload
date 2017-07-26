using System;
using System.IO;
using System.Linq;
using FragmentedFileUpload.Constants;
using FragmentedFileUpload.Extensions;
using FragmentedFileUpload.Services;

namespace FragmentedFileUpload.Core
{
    public interface IFileMerger
    {
        string InputFilePath { set; }
        IFileSystemService FileSystem { set; }
        Stream MergeFile();
    }

    public class FileMerger : IFileMerger
    {
        public string InputFilePath { private get; set; }

        public IFileSystemService FileSystem { private get; set; }

        public Stream MergeFile()
        {
            if (string.IsNullOrWhiteSpace(InputFilePath))
                throw new InvalidOperationException("Input path cannot be null or empty.");

            var inputDirectory = FileSystem.GetDirectoryName(InputFilePath);
            if (!FileSystem.DirectoryExists(inputDirectory))
                throw new DirectoryNotFoundException("Input path does not exist.");

            var fileName = FileSystem.GetFileName(InputFilePath);
            var baseFileName = fileName.GetBaseName();
            var searchpattern = $"{baseFileName}{Naming.PartToken}*";
            var orderedFiles = FileSystem.EnumerateFilesInDirectory(inputDirectory, searchpattern)
                .OrderBy(s => s).ToArray();

            // naive check if all parts are there
            var partName = orderedFiles.First();
            int.TryParse(partName.Substring(partName.LastIndexOf('.') + 1), out int fileCount);
            if (orderedFiles.Length != fileCount)
                return null;

            var stream = new MemoryStream();
            foreach (var file in orderedFiles)
            {
                if (!FileSystem.FileExists(file))
                    throw new FileNotFoundException("Part not found.", file);

                FileSystem.CopyFileToStream(file, stream);
            }
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        private FileMerger(IFileSystemService fileSystemService)
        {
            FileSystem = fileSystemService;
        }

        public static FileMerger Create(string inputFilePath, IFileSystemService fileSystemService = null)
        {
            return new FileMerger(fileSystemService ?? new FileSystemService())
            {
                InputFilePath = inputFilePath
            };
        }
    }
}