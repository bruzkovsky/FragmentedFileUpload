using System;
using System.IO;
using System.Linq;
using FragmentedFileUpload.Constants;

namespace FragmentedFileUpload
{
    public class FileMerger
    {
        public string InputFilePath { get; set; }
        public string OutputDirectoryPath { get; set; }

        public IFileSystemService FileSystem { private get; set; }

        public string MergeFile()
        {
            if (string.IsNullOrWhiteSpace(InputFilePath))
                throw new InvalidOperationException("Input path cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(OutputDirectoryPath))
                throw new InvalidOperationException("Output path cannot be null or empty.");

            var inputDirectory = FileSystem.GetDirectoryName(InputFilePath);
            if (!FileSystem.DirectoryExists(inputDirectory))
                throw new DirectoryNotFoundException("Input path does not exist.");

            var fileName = FileSystem.GetFileName(InputFilePath);
            var baseFileName = fileName.Remove(fileName.IndexOf(Naming.PartToken, StringComparison.Ordinal));
            var searchpattern = $"{baseFileName}{Naming.PartToken}*";
            var orderedFiles = FileSystem.EnumerateFilesInDirectory(inputDirectory, searchpattern)
                .OrderBy(s => s).ToArray();

            // naive check if all parts are there
            var partName = orderedFiles.First();
            int.TryParse(partName.Substring(partName.LastIndexOf('.') + 1), out int fileCount);
            if (orderedFiles.Length != fileCount)
                return null;

            // ensure output directory exists and there is no file with the same name
            var outputFilePath = FileSystem.PathCombine(OutputDirectoryPath, baseFileName);
            if (FileSystem.FileExists(outputFilePath))
                FileSystem.DeleteFile(outputFilePath);
            if (!FileSystem.DirectoryExists(OutputDirectoryPath))
                FileSystem.CreateDirectory(OutputDirectoryPath);

            using (var stream = FileSystem.CreateFile(outputFilePath))
            {
                foreach (var file in orderedFiles)
                {
                    if (!FileSystem.FileExists(file))
                        throw new FileNotFoundException("Part not found.", file);

                    FileSystem.CopyFileToStream(file, stream);
                }
            }

            return outputFilePath;
        }

        private FileMerger(IFileSystemService fileSystemService)
        {
            FileSystem = fileSystemService;
        }

        public static FileMerger Create(string inputFilePath, string outputFilePath, IFileSystemService fileSystemService = null)
        {
            return new FileMerger(fileSystemService ?? new FileSystemService())
            {
                InputFilePath = inputFilePath,
                OutputDirectoryPath = outputFilePath
            };
        }
    }
}