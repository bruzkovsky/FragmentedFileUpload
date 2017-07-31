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
using FragmentedFileUpload.Services;
using NUnit.Framework;

namespace FragmentedFileUpload.Tests
{
    [TestFixture]
    [Category("IntegrationTests")]
    public class FragmentedFileUploadIntegrationTests
    {
        private static string UploadPath { get; } = Path.Combine(BinFolder, "Server", "Temp");
        private static string OutputPath { get; } = Path.Combine(BinFolder, "Server", "Import");
        private static string TempPath { get; } = Path.Combine(BinFolder, "Client", "Temp");
        private static string TestDataPath { get; } = Path.Combine(BinFolder, "TestData");

        private static string BinFolder => AppDomain.CurrentDomain.BaseDirectory;

        private UploadClient CreateUploadClient(
            string filePath,
            string url,
            string tempPath,
            Func<HttpClient, HttpClient> authorizeClient = null,
            Func<HttpClient> httpClientFactory = null,
            IFileSystemService fileSystemService = null,
            Action<HttpResponseMessage> onRequestComplete = null,
            Action<HttpStatusCode> onRequestFailed = null)
        {
            return UploadClient.Create(filePath, url, tempPath, authorizeClient,
                httpClientFactory ?? (() => new HttpClient(new WebApiKeyHandler())), onRequestComplete, onRequestFailed, fileSystemService);
        }

        [SetUp]
        public void Setup()
        {
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);
            if (Directory.Exists(OutputPath))
                Directory.Delete(OutputPath, true);
            if (Directory.Exists(UploadPath))
                Directory.Delete(UploadPath, true);
        }

        [Test]
        public void UploadFile_FileIsUploadedInPartsAndSavedOnServer()
        {
            // Arrange
            const string fileName = "PictureQualitySmall1_2017.05.23_08-24-50.iclr";
            const string originalHash = "780faf7b15f08c3cce0fe02c26d932bbd36ffd393eee4e5e6c8e0e383a787fab";
            const string uploadUrl = "http://this.is.a/valid/url/";
            var client =
                CreateUploadClient(Path.Combine(TestDataPath, fileName),
                    uploadUrl, TempPath);
            client.MaxChunkSizeMegaByte = 0.1;

            // Act
            var result = client.UploadFile().Result;

            // Assert
            Assert.IsTrue(result);
            var filePath = Path.Combine(OutputPath, fileName);
            Assert.IsTrue(File.Exists(filePath));
            using (var stream = File.OpenRead(filePath))
                Assert.AreEqual(originalHash, stream.ComputeSha256Hash());
            Assert.IsFalse(Directory.Exists(Path.Combine(TempPath, originalHash)));
        }

        [Test]
        public void ResumeUpload_FileIsUploadedInPartsAndSavedOnServer()
        {
            // Arrange
            const string fileName = "PictureQualitySmall1_2017.05.23_08-24-50.iclr";
            const string originalHash = "780faf7b15f08c3cce0fe02c26d932bbd36ffd393eee4e5e6c8e0e383a787fab";
            const string uploadUrl = "http://this.is.a/valid/url/";
            var resumeTestPath = Path.Combine(TestDataPath, "ResumeTest");
            foreach (var directory in Directory.GetDirectories(resumeTestPath, "*", SearchOption.AllDirectories))
            {
                if (directory != null)
                {
                    var tempDirectoryPath = directory.Replace(resumeTestPath, BinFolder);
                    Directory.CreateDirectory(tempDirectoryPath);
                    foreach (var file in Directory.GetFiles(directory))
                    {
                        if (file != null)
                            File.Copy(file, Path.Combine(tempDirectoryPath, Path.GetFileName(file)));
                    }
                }
            }
            var client =
                CreateUploadClient(Path.Combine(TestDataPath, fileName),
                    uploadUrl, TempPath);
            client.MaxChunkSizeMegaByte = 0.1;

            // Act
            client.ResumeUpload().Wait();

            // Assert
            var filePath = Path.Combine(OutputPath, fileName);
            Assert.IsTrue(File.Exists(filePath));
            using (var stream = File.OpenRead(filePath))
                Assert.AreEqual(originalHash, stream.ComputeSha256Hash());
            Assert.IsFalse(Directory.Exists(Path.Combine(TempPath, originalHash)));
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
                var receiver = Receiver.Create(UploadPath, s =>
                {
                    var filePath = Path.Combine(OutputPath, fileName.GetBaseName());
                    if (!Directory.Exists(OutputPath))
                        Directory.CreateDirectory(OutputPath);
                    using (var outStream = File.OpenWrite(filePath))
                        s.CopyTo(outStream);
                }, hash);
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
