using System;
using System.IO;
using FragmentedFileUpload.Extensions;

namespace FragmentedFileUpload.Server
{
    public class Receiver
    {
        public string TempPath { get; set; }
        public string OutputPath { get; set; }
        public string Hash { get; set; }
        public IFileSystemService FileSystem { get; set; }

        public static Receiver Create(string tempPath, string outputPath, string hash, IFileSystemService fileSystemService = null)
        {
            return new Receiver(fileSystemService ?? new FileSystemService())
            {
                TempPath = tempPath,
                OutputPath = outputPath,
                Hash = hash
            };
        }

        private Receiver(IFileSystemService fileSystemService)
        {
            FileSystem = fileSystemService;
        }

        public void Receive(Stream fileStream, string fileName, string originalPartHash)
        {
            if (fileStream == null)
                throw new ArgumentException("File stream cannot be null.", nameof(fileStream));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or whitespace.", nameof(fileName));
            if (string.IsNullOrWhiteSpace(originalPartHash))
                throw new ArgumentException("Original hash cannot be null or whitespace.", nameof(originalPartHash));

            if (string.IsNullOrWhiteSpace(TempPath))
                throw new InvalidOperationException("Temporary path cannot be null or whitespace.");
            if (string.IsNullOrWhiteSpace(OutputPath))
                throw new InvalidOperationException("Output path cannot be null or whitespace.");
            if (string.IsNullOrWhiteSpace(Hash))
                throw new InvalidOperationException("Hash cannot be null or whitespace.");

            var partFilePath = FileSystem.PathCombine(TempPath, Hash, fileName);
            var partDirectoryName = FileSystem.GetDirectoryName(partFilePath);
            if (!FileSystem.DirectoryExists(partDirectoryName))
                FileSystem.CreateDirectory(partDirectoryName);
            if (FileSystem.FileExists(partFilePath))
                FileSystem.DeleteFile(partFilePath);
            using (var tempStream = FileSystem.CreateFile(partFilePath))
                fileStream.CopyTo(tempStream);

            using (var partStream = FileSystem.OpenRead(partFilePath))
            {
                var computedPartHash = partStream.ComputeSha256Hash();
                if (!string.Equals(originalPartHash, computedPartHash))
                    throw new InvalidOperationException("Part hash does not match.");
            }

            var merger = FileMerger.Create(partFilePath, OutputPath);
            var outputFilePath = merger.MergeFile();

            if (outputFilePath == null)
                return;

            if (!FileSystem.FileExists(outputFilePath))
                throw new InvalidOperationException("The parts could not be merged.");

            using (var resultStream = FileSystem.OpenRead(outputFilePath))
            {
                var resultHash = resultStream.ComputeSha256Hash();
                if (!string.Equals(resultHash, Hash))
                    throw new InvalidOperationException("The hash of the merged file does not match.");
            }
        }
    }
}
