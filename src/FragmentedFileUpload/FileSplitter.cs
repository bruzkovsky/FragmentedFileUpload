using System;
using System.Collections.Generic;
using FragmentedFileUpload.Constants;

namespace FragmentedFileUpload
{
    public sealed class FileSplitter
    {
        private const int Mega = 1024 * 1024;

        public string FilePath { get; private set; }
        public string TempFolderPath { get; set; }
        public double MaxChunkSizeMegaByte { get; set; }
        public IEnumerable<string> FileParts { get; set; }

        public IFileSystemService FileSystem { private get; set; }

        public static FileSplitter Create(string fileName, string tempFolderPath, IFileSystemService fileSystemService = null)
        {
            return new FileSplitter(fileSystemService ?? new FileSystemService())
            {
                FilePath = fileName,
                TempFolderPath = tempFolderPath
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
                ? (int) Math.Ceiling(MaxChunkSizeMegaByte * Mega)
                : 1 * Mega;
            const int readbufferSize = 1024;
            var fsBuffer = new byte[readbufferSize];
            // adapted from: http://stackoverflow.com/questions/3967541/how-to-split-large-files-efficiently
            using (var stream = FileSystem.OpenRead(FilePath))
            {
                var totalFileParts = (int) Math.Ceiling(stream.Length / (float) bufferChunkSize);
                // make digit count the same for index and total. Example: xyz.part_003.100 (D3) or xyz.part_03.10 (D2)
                var numberOfDigits = Math.Floor(Math.Log10(totalFileParts)) + 1;
                var fileParts = new List<string>();

                for (var filePartCount = 1; stream.Position < stream.Length; filePartCount++)
                {
                    var formattedFilePartCount = filePartCount.ToString($"D{numberOfDigits}");
                    var filePartName = $"{baseFileName}{Naming.PartToken}{formattedFilePartCount}.{totalFileParts}";
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