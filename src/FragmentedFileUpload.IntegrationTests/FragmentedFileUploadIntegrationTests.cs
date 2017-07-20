using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FragmentedFileUpload.Client;
using FragmentedFileUpload.Extensions;
using FragmentedFileUpload.Server;
using NUnit.Framework;

namespace FragmentedFileUpload.IntegrationTests
{
    [TestFixture]
    public class FragmentedFileUploadIntegrationTests
    {
        private static readonly string UploadPath = Path.Combine(BinFolder, "Server", "Temp");
        private static readonly string OutputPath = Path.Combine(BinFolder, "Server", "Import");

        private static string BinFolder => AppDomain.CurrentDomain.BaseDirectory;

        private UploadClient CreateUploadClient(
            string filePath,
            string url,
            string tempPath,
            Func<HttpClient, HttpClient> authorizeClient = null,
            Func<HttpClient> httpClientFactory = null,
            IFileSystemService fileSystemService = null,
            Action<HttpStatusCode> onRequestFailed = null)
        {
            return UploadClient.Create(filePath, url, tempPath, authorizeClient,
                httpClientFactory ?? (() => new HttpClient(new WebApiKeyHandler())), onRequestFailed, fileSystemService);
        }

        [Test]
        public void UploadFile_FileIsUploadedInPartsAndSavedOnServer()
        {
            // Arrange
            const string fileName = "PictureQualitySmall1_2017.05.23_08-24-50.iclr";
            const string originalHash = "780faf7b15f08c3cce0fe02c26d932bbd36ffd393eee4e5e6c8e0e383a787fab";
            const string uploadUrl = "http://this.is.a/valid/url/";
            var client =
                CreateUploadClient(Path.Combine(BinFolder, "TestData", fileName),
                    uploadUrl, Path.Combine(BinFolder, "Client", "Temp"));
            client.MaxChunkSizeMegaByte = 0.1;

            // Act
            var result = client.UploadFile().Result;

            // Assert
            Assert.IsTrue(result);
            var filePath = Path.Combine(OutputPath, fileName);
            Assert.IsTrue(File.Exists(filePath));
            using (var stream = File.OpenRead(filePath))
                Assert.AreEqual(originalHash, stream.ComputeSha256Hash());
        }

        private class WebApiKeyHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(UploadFile(request));
            }
        }

        private static HttpResponseMessage UploadFile(HttpRequestMessage request)
        {
            var requestContent = request.Content.ReadAsMultipartAsync().Result;
            var hash = requestContent.Contents.First(c => c.Headers.ContentDisposition.Name == "hash").ReadAsStringAsync().Result;
            var partHash = requestContent.Contents.First(c => c.Headers.ContentDisposition.Name == "partHash").ReadAsStringAsync().Result;
            var file = requestContent.Contents.First(c => c.Headers.ContentDisposition.Name == "file");
            if (file == null || file.Headers.ContentLength <= 0)
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("The file is empty or missing.")
                };

            var fileName = Path.GetFileName(file.Headers.ContentDisposition.FileName);
            if (string.IsNullOrEmpty(fileName))
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("The filename was not set.")
                };

            // take the input stream, and save it to a temp folder using the original file.part name posted
            using (var stream = file.ReadAsStreamAsync().Result)
            {
                var receiver = Receiver.Create(UploadPath, OutputPath, hash);
                try
                {
                    receiver.Receive(stream, fileName, partHash);
                }
                catch (InvalidOperationException e)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Content = new StringContent(e.Message)
                    };
                }
            }

            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("File uploaded.")
            };
        }
    }
}
