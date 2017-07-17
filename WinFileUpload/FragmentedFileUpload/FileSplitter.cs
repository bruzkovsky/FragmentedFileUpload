using System;
using System.Collections.Generic;
using FragmentedFileUpload.Constants;

namespace FragmentedFileUpload
{
    public sealed class FileSplitter
    {
        public string FilePath { get; private set; }
        public string TempFolderPath { get; set; }
        public double MaxChunkSizeMegaByte { get; set; }
        public IEnumerable<string> FileParts { get; set; }

        public IFileSystemService FileSystem { private get; set; }

        public static FileSplitter Create(string fileName, IFileSystemService fileSystemService = null)
        {
            return new FileSplitter(fileSystemService ?? new FileSystemService())
            {
                FilePath = fileName
            };
        }

        private FileSplitter(IFileSystemService fileSystemService)
        {
            FileSystem = fileSystemService;
            FileParts = new List<string>();
        }

        /// <summary>
        /// Split = get number of files 
        /// .. Name = original name + ".part_N.X" (N = file part number, X = total files)
        /// </summary>
        /// <returns></returns>
        public int SplitFile()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                throw new InvalidOperationException("File path cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(TempFolderPath))
                throw new InvalidOperationException("Temporary folder path cannot be null or empty.");
            if (!FileSystem.FileExists(FilePath))
                throw new InvalidOperationException("The file does not exist.");

            if (!FileSystem.DirectoryExists(TempFolderPath))
                FileSystem.CreateDirectory(TempFolderPath);

            var baseFileName = FileSystem.GetFileName(FilePath);
            var bufferChunkSize = MaxChunkSizeMegaByte > 0
                ? (int) Math.Ceiling(MaxChunkSizeMegaByte * (1024 * 1024))
                : 1;
            const int readbufferSize = 1024;
            var fsBuffer = new byte[readbufferSize];
            // adapted from: http://stackoverflow.com/questions/3967541/how-to-split-large-files-efficiently
            using (var stream = FileSystem.OpenOrCreate(FilePath))
            {
                var totalFileParts = (int) Math.Ceiling(stream.Length / (float) bufferChunkSize);
                var fileParts = new List<string>();

                for (var filePartCount = 0; stream.Position < stream.Length; filePartCount++)
                {
                    var filePartName = string.Format($"{baseFileName}{Naming.PartToken}{filePartCount + 1}.{totalFileParts}");
                    filePartName = FileSystem.PathCombine(TempFolderPath, filePartName);
                    fileParts.Add(filePartName);
                    using (var filePart = FileSystem.OpenOrCreate(filePartName))
                    {
                        var bytesRemaining = bufferChunkSize;
                        int bytesRead;
                        while (bytesRemaining > 0 && (bytesRead =
                                   stream.Read(fsBuffer, 0, Math.Min(bytesRemaining, readbufferSize))) > 0)
                        {
                            filePart.Write(fsBuffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }
                    }
                }

                FileParts = fileParts;

                return totalFileParts;
            }
        }
    }
}