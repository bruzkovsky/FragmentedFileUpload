using System;
using System.Collections.Generic;
using System.Linq;
using FragmentedFileUpload.Constants;

namespace FragmentedFileUpload
{
    public class FileMerger
    {
        public string InputPath { get; set; }
        public string FileName { get; set; }
        public string OutputFilePath { get; set; }

        public IFileSystemService FileSystem { private get; set; }

        public void ExtractFilesFromRequest()
        {
            var searchpattern = $"{FileName}{Naming.PartToken}*";
            foreach (var file in FileSystem.GetFilesInDirectory(InputPath, searchpattern))
            {
                MergeFile(file);
            }
        }

        /// <summary>
        /// original name + ".part_N.X" (N = file part number, X = total files)
        /// Objective = enumerate files in folder, look for all matching parts of split file. If found, merge and return true.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool MergeFile(string fileName)
        {
            var rslt = false;
            // parse out the different tokens from the filename according to the convention
            var baseFileName = fileName.Substring(0, fileName.IndexOf(Naming.PartToken, StringComparison.Ordinal));
            var trailingTokens = fileName.Substring(fileName.IndexOf(Naming.PartToken, StringComparison.Ordinal) + Naming.PartToken.Length);
            int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".", StringComparison.Ordinal)), out int fileIndex);
            int.TryParse(trailingTokens.Substring(trailingTokens.IndexOf(".", StringComparison.Ordinal) + 1), out int fileCount);
            // get a list of all file parts in the temp folder
            var searchpattern = FileSystem.GetFileName(baseFileName) + Naming.PartToken + "*";
            var filesList = FileSystem.GetFilesInDirectory(FileSystem.GetDirectoryName(fileName), searchpattern).OrderBy(s => s).ToArray();
            //  merge .. improvement would be to confirm individual parts are there / correctly in sequence, a security check would also be important
            // only proceed if we have received all the file chunks
            if (filesList.Length == fileCount)
            {
                // use a singleton to stop overlapping processes
                if (!MergeFileManager.Instance.InUse(baseFileName))
                {
                    MergeFileManager.Instance.AddFile(baseFileName);
                    if (FileSystem.FileExists(baseFileName))
                        FileSystem.DeleteFile(baseFileName);
                    // add each file located to a list so we can get them into 
                    // the correct order for rebuilding the file
                    var mergeList = new List<SortedFile>();
                    foreach (var file in filesList)
                    {
                        var sFile = new SortedFile();
                        sFile.FileName = file;
                        baseFileName = file.Substring(0, file.IndexOf(Naming.PartToken, StringComparison.Ordinal));
                        trailingTokens = file.Substring(file.IndexOf(Naming.PartToken, StringComparison.Ordinal) + Naming.PartToken.Length);
                        int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".", StringComparison.Ordinal)), out fileIndex);
                        sFile.FileOrder = fileIndex;
                        mergeList.Add(sFile);
                    }
                    // sort by the file-part number to ensure we merge back in the correct order
                    var mergeOrder = mergeList.OrderBy(s => s.FileOrder).ToList();
                    using (var stream = FileSystem.CreateFile(baseFileName))
                    {
                        // merge each file chunk back into one contiguous file stream
                        foreach (var chunk in mergeOrder)
                        {
                            using (var fileChunk = FileSystem.OpenRead(chunk.FileName))
                            {
                                fileChunk.CopyTo(stream);
                            }
                        }
                    }
                    rslt = true;
                    // unlock the file from singleton
                    MergeFileManager.Instance.RemoveFile(baseFileName);
                }
            }
            return rslt;
        }
    }
}