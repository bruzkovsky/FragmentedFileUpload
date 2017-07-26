using System;
using System.IO;
using FragmentedFileUpload.Core;
using FragmentedFileUpload.Extensions;
using FragmentedFileUpload.Services;

namespace FragmentedFileUpload.Server
{
    public interface IReceiver
    {
        string TempPath { set; }
        Action<Stream> OnResult { set; }
        string Hash { set; }
        IFileSystemService FileSystem { set; }
        Func<string, IFileSystemService, IFileMerger> FileMergerFactory { set; }
        void Receive(Stream fileStream, string fileName, string originalPartHash);
    }

    public sealed class Receiver : IReceiver
    {
        public string TempPath { private get; set; }
        public Action<Stream> OnResult { private get; set; }
        public string Hash { private get; set; }
        public IFileSystemService FileSystem { private get; set; }
        public Func<string, IFileSystemService, IFileMerger> FileMergerFactory { private get; set; }

        public static Receiver Create(string tempPath, Action<Stream> onResult, string hash, IFileSystemService fileSystemService = null, Func<string, IFileSystemService, IFileMerger> fileMergerFactory = null)
        {
            return new Receiver(fileSystemService ?? new FileSystemService(), fileMergerFactory ?? FileMerger.Create)
            {
                TempPath = tempPath,
                OnResult = onResult,
                Hash = hash
            };
        }

        private Receiver(IFileSystemService fileSystemService, Func<string, IFileSystemService, IFileMerger> fileMergerFactory)
        {
            FileSystem = fileSystemService;
            FileMergerFactory = fileMergerFactory;
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

            var merger = FileMergerFactory(partFilePath, FileSystem);
            using (var resultStream = merger.MergeFile())
            {
                if (resultStream == null)
                    return;

                try
                {
                    var resultHash = resultStream.ComputeSha256Hash();
                    if (!string.Equals(resultHash, Hash))
                        throw new InvalidOperationException("The hash of the merged file does not match.");
                    OnResult?.Invoke(resultStream);
                }
                finally
                {
                    FileSystem.DeleteDirectory(FileSystem.PathCombine(TempPath, Hash), true);
                }
            }
        }
    }
}
