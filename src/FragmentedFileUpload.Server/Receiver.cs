using System;
using System.IO;
using System.Net.Http;
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
            if (!FileSystem.DirectoryExists(TempPath))
                FileSystem.CreateDirectory(TempPath);
            var partFilePath = FileSystem.PathCombine(TempPath, fileName);
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
