using System;
using System.Collections.Generic;
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

    public interface IUploadClient
    {
        string FilePath { get; set; }
        string UploadUrl { get; set; }
        string TempFolderPath { get; set; }
        double MaxChunkSizeMegaByte { get; set; }
        Func<HttpClient, HttpClient> AuthorizeClient { set; }
        Func<HttpClient> ClientFactory { set; }
        Action<HttpResponseMessage> OnRequestComplete { set; }
        Action<HttpStatusCode> OnRequestFailed { set; }
        IFileSystemService FileSystem { set; }
        Task<bool> UploadFile(CancellationToken cancellationToken = default(CancellationToken));
        Task ResumeUpload(CancellationToken cancellationToken = default(CancellationToken));
    }

    public sealed class UploadClient : IUploadClient
    {
        public string FilePath { get; set; }
        public string UploadUrl { get; set; }
        public string TempFolderPath { get; set; }
        public double MaxChunkSizeMegaByte { get; set; }

        public Func<HttpClient, HttpClient> AuthorizeClient { private get; set; }
        public Func<HttpClient> ClientFactory { private get; set; }
        public Action<HttpResponseMessage> OnRequestComplete { private get; set; }
        public Action<HttpStatusCode> OnRequestFailed { private get; set; }
        public IFileSystemService FileSystem { private get; set; }

        public static UploadClient Create(
            string filePath,
            string uploadUrl,
            string tempFolderPath,
            Func<HttpClient, HttpClient> authorizeClient = null,
            Func<HttpClient> httpClient = null,
            Action<HttpResponseMessage> onRequestComplete = null,
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
                OnRequestComplete = onRequestComplete,
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

            var result = new HttpResponseMessage(HttpStatusCode.OK);
            var parts = splitter.FileParts;
            if (!await UploadAllParts(parts, hash, cancellationToken))
                return false;
            OnRequestComplete?.Invoke(result);

            return true;
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
                await UploadAllParts(fileNames, FileSystem.GetFileName(directoryName), cancellationToken);
            }
        }

        private async Task<bool> UploadAllParts(
            IEnumerable<string> parts,
            string hash,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (var file in parts)
            {
                HttpResponseMessage result;
                try
                {
                    result = await UploadPart(file, hash);
                }
                catch (HttpRequestException)
                {
                    OnRequestFailed?.Invoke(HttpStatusCode.NotFound);
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!result.IsSuccessStatusCode)
                {
                    OnRequestFailed?.Invoke(result.StatusCode);
                    return false;
                }

                DeletePart(file);

                cancellationToken.ThrowIfCancellationRequested();
            }
            return true;
        }

        private void DeletePart(string file)
        {
            FileSystem.DeleteFile(file);
            var directoryPath = FileSystem.GetDirectoryName(file);
            if (!FileSystem.EnumerateEntriesInDirectory(directoryPath, "*").Any())
                FileSystem.DeleteDirectory(directoryPath, false);
        }

        private async Task<HttpResponseMessage> UploadPart(string partFilePath, string hash)
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

                    return await client.PostAsync(UploadUrl, content);
                }
            }
        }

        private string ComputeSha256Hash(string filePath)
        {
            using (var stream = FileSystem.OpenRead(filePath))
                return stream.ComputeSha256Hash();
        }
    }
}