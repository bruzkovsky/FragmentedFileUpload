using System.Collections.Generic;
using System.IO;

namespace FragmentedFileUpload
{
    public interface IFileSystemService
    {
        Stream OpenOrCreate(string filePath);
        void CreateDirectory(string directoryPath);
        bool FileExists(string filePath);
        bool DirectoryExists(string directoryPath);
        string GetFileName(string filePath);
        string PathCombine(params string[] parts);
        byte[] ReadAllBytes(string filePath);
        void DeleteFile(string filePath);
        Stream CreateFile(string filePath);
        string GetDirectoryName(string directoryPath);
        IEnumerable<string> GetFilesInDirectory(string directoryPath, string searchpattern);
        Stream OpenRead(string filePath);
        void CopyFileToStream(string file, Stream stream);
        void DeleteDirectory(string directoryPath, bool recursive);
        IEnumerable<string> GetDirectoriesInDirectory(string directoryPath, string searchpattern);
    }

    public class FileSystemService : IFileSystemService
    {
        public Stream OpenOrCreate(string filePath)
        {
            return File.Open(filePath, FileMode.OpenOrCreate);
        }

        public Stream CreateFile(string filePath)
        {
            return File.Create(filePath);
        }

        public string GetDirectoryName(string directoryPath)
        {
            return Path.GetDirectoryName(directoryPath);
        }

        public IEnumerable<string> GetFilesInDirectory(string directoryPath, string searchpattern)
        {
            return Directory.GetFiles(directoryPath, searchpattern);
        }

        public IEnumerable<string> GetDirectoriesInDirectory(string directoryPath, string searchpattern)
        {
            return Directory.GetDirectories(directoryPath, searchpattern);
        }

        public Stream OpenRead(string filePath)
        {
            return File.OpenRead(filePath);
        }

        public void CreateDirectory(string directoryPath)
        {
            Directory.CreateDirectory(directoryPath);
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public void DeleterFile(string filePath)
        {
        }

        public bool DirectoryExists(string directoryPath)
        {
            return Directory.Exists(directoryPath);
        }

        public string GetFileName(string filePath)
        {
            return Path.GetFileName(filePath);
        }

        public string PathCombine(params string[] parts)
        {
            return Path.Combine(parts);
        }

        public byte[] ReadAllBytes(string filePath)
        {
            return File.ReadAllBytes(filePath);
        }

        public void DeleteFile(string filePath)
        {
            File.Delete(filePath);
        }

        public void CopyFileToStream(string file, Stream stream)
        {
            using (var fileChunk = File.OpenRead(file))
            {
                fileChunk.CopyTo(stream);
            }
        }

        public void DeleteDirectory(string directoryPath, bool recursive)
        {
            Directory.Delete(directoryPath, recursive);
        }
    }
}
