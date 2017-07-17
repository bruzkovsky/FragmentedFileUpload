using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

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
        public bool UseAuthentication { get; set; } 
        private static HttpClient Client => new HttpClient();
        private string AuthenticationToken { get; set; }
        private HttpClient AuthorizedClient => Client.AuthorizeWith(AuthenticationToken);

        public IFileSystemService FileSystem { private get; set; }

        public static UploadClient Create(string filePath, string uploadUrl, string authenticationToken = null, bool useAuthentication = false, IFileSystemService fileSystemService = null)
        {
            return new UploadClient(fileSystemService ?? new FileSystemService())
            {
                FilePath = filePath,
                UploadUrl = uploadUrl,
                AuthenticationToken = authenticationToken,
                UseAuthentication = useAuthentication
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
            if (!FileSystem.FileExists(FilePath))
                throw new InvalidOperationException("The file does not exist.");

            using (var client = UseAuthentication ? AuthorizedClient : Client)
            {
                using (var content = new MultipartFormDataContent())
                {
                    var fileContent = new ByteArrayContent(FileSystem.ReadAllBytes(FilePath));
                    fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                    {
                        FileName = FileSystem.GetFileName(FilePath)
                    };
                    content.Add(fileContent);

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
    }
}
