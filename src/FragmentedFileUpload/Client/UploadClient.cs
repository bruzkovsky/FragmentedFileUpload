using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FragmentedFileUpload.Core;
using FragmentedFileUpload.Extensions;
using FragmentedFileUpload.Services;

namespace FragmentedFileUpload.Client
{
    public static class UploadClientExtensions
    {
        public static HttpClient AuthorizeWith(this HttpClient client, string token)
        {
            if (client.DefaultRequestHeaders.Authorization == null)
                client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
            return client;
        }
    }

    public sealed class UploadClient
    {
        public string FilePath { get; set; }
        public string UploadUrl { get; set; }
        public string TempFolderPath { get; set; }
        public double MaxChunkSizeMegaByte { get; set; }

        public Func<HttpClient, HttpClient> AuthorizeClient { private get; set; }
        public Func<HttpClient> ClientFactory { private get; set; }
        public Action<HttpStatusCode> OnRequestFailed { private get; set; }
        public IFileSystemService FileSystem { private get; set; }

        public static UploadClient Create(
            string filePath,
            string uploadUrl,
            string tempFolderPath,
            Func<HttpClient, HttpClient> authorizeClient = null,
            Func<HttpClient> httpClient = null,
            Action<HttpStatusCode> onRequestFailed = null,
            IFileSystemService fileSystemService = null)
        {
            return new UploadClient(httpClient ?? (() => new HttpClient()),
                fileSystemService ?? new FileSystemService())
            {
                FilePath = filePath,
                UploadUrl = uploadUrl,
                TempFolderPath = tempFolderPath,
                AuthorizeClient = authorizeClient,
                OnRequestFailed = onRequestFailed
            };
        }

        private UploadClient(Func<HttpClient> httpClientFactory, IFileSystemService fileSystemService)
        {
            ClientFactory = httpClientFactory;
            FileSystem = fileSystemService;
        }

        public async Task<bool> UploadFile(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                throw new InvalidOperationException("File path cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(UploadUrl))
                throw new InvalidOperationException("URL cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(TempFolderPath))
                throw new InvalidOperationException("Temporary folder path cannot be null or empty.");
            if (!FileSystem.FileExists(FilePath))
                throw new InvalidOperationException("The file does not exist.");

            cancellationToken.ThrowIfCancellationRequested();

            var hash = ComputeSha256Hash(FilePath);

            cancellationToken.ThrowIfCancellationRequested();

            var splitter = FileSplitter.Create(FilePath, FileSystem.PathCombine(TempFolderPath, hash), FileSystem);
            splitter.MaxChunkSizeMegaByte = MaxChunkSizeMegaByte;
            splitter.SplitFile();

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var file in splitter.FileParts)
            {
                await UploadPart(file, hash);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return true;
        }

        private async Task UploadPart(string partFilePath, string hash)
        {
            using (var client = ClientFactory())
            {
                AuthorizeClient?.Invoke(client);
                var partBytes = FileSystem.ReadAllBytes(partFilePath);
                using (var content = new MultipartFormDataContent())
                using (var fileContent = new ByteArrayContent(partBytes))
                using (var partStream = new MemoryStream(partBytes))
                using (var partHashContent = new StringContent(partStream.ComputeSha256Hash()))
                using (var hashContent = new StringContent(hash))
                {
                    fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                    {
                        FileName = FileSystem.GetFileName(partFilePath),
                        Name = "file"
                    };
                    content.Add(fileContent);
                    content.Add(hashContent, "hash");
                    content.Add(partHashContent, "partHash");

                    var result = await client.PostAsync(UploadUrl, content);
                    if (!result.IsSuccessStatusCode)
                    {
                        OnRequestFailed?.Invoke(result.StatusCode);
                        return;
                    }

                    FileSystem.DeleteFile(partFilePath);
                    var directoryPath = FileSystem.GetDirectoryName(partFilePath);
                    if (!FileSystem.EnumerateEntriesInDirectory(directoryPath, "*").Any())
                        FileSystem.DeleteDirectory(directoryPath, false);
                }
            }
        }

        private string ComputeSha256Hash(string filePath)
        {
            using (var stream = FileSystem.OpenRead(filePath))
                return stream.ComputeSha256Hash();
        }

        public async Task ResumeUpload(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(UploadUrl))
                throw new InvalidOperationException("URL cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(TempFolderPath))
                throw new InvalidOperationException("Temporary folder path cannot be null or empty.");

            cancellationToken.ThrowIfCancellationRequested();

            var directoryNames = FileSystem.EnumerateDirectoriesInDirectory(TempFolderPath, "*");
            foreach (var directoryName in directoryNames)
            {
                var fileNames =
                    FileSystem.EnumerateFilesInDirectory(FileSystem.PathCombine(TempFolderPath, directoryName), "*");
                foreach (var fileName in fileNames)
                {
                    await UploadPart(FileSystem.PathCombine(TempFolderPath, fileName), FileSystem.GetFileName(directoryName));
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }
    }
}