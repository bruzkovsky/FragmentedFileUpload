using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FragmentedFileUpload.Extensions;

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
        private static HttpClient Client => new HttpClient();
        public Func<HttpClient, HttpClient> AuthorizeClient { get; set; }

        public IFileSystemService FileSystem { private get; set; }

        public static UploadClient Create(
            string filePath,
            string uploadUrl,
            string tempFolderPath,
            Func<HttpClient, HttpClient> authorizeClient = null,
            IFileSystemService fileSystemService = null)
        {
            return new UploadClient(fileSystemService ?? new FileSystemService())
            {
                FilePath = filePath,
                UploadUrl = uploadUrl,
                TempFolderPath = tempFolderPath,
                AuthorizeClient = authorizeClient
            };
        }

        private UploadClient(IFileSystemService fileSystemService)
        {
            FileSystem = fileSystemService;
        }

        public async Task<bool> UploadFile()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                throw new InvalidOperationException("File path cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(UploadUrl))
                throw new InvalidOperationException("URL cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(TempFolderPath))
                throw new InvalidOperationException("Temporary folder path cannot be null or empty.");
            if (!FileSystem.FileExists(FilePath))
                throw new InvalidOperationException("The file does not exist.");

            var hash = ComputeSha256Hash(FilePath);

            var splitter = FileSplitter.Create(FilePath, TempFolderPath);
            splitter.MaxChunkSizeMegaByte = MaxChunkSizeMegaByte;
            splitter.SplitFile();

            var success = true;
            foreach (var file in splitter.FileParts)
            {
                if (!await UploadPart(file, hash))
                {
                    success = false;
                    break;
                }
            }

            return success;
        }

        private async Task<bool> UploadPart(string partFilePath, string hash)
        {
            using (var client = Client)
            {
                AuthorizeClient?.Invoke(client);
                using (var content = new MultipartFormDataContent())
                using (var fileContent = new ByteArrayContent(FileSystem.ReadAllBytes(partFilePath)))
                using (var hashContent = new StringContent(hash))
                {
                    fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                    {
                        FileName = FileSystem.GetFileName(partFilePath)
                    };
                    content.Add(fileContent);
                    content.Add(hashContent, "hash");

                    try
                    {
                        var result = await client.PostAsync(UploadUrl, content);
                        return result.IsSuccessStatusCode;
                    }
                    catch (Exception)
                    {
                        // log error
                        return false;
                    }
                }
            }
        }

        private string ComputeSha256Hash(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            using (var stream = FileSystem.OpenRead(filePath))
                return stream.ComputeSha256Hash();
        }
    }
}