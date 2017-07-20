using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FragmentedFileUpload.Constants;
using FragmentedFileUpload.Extensions;
using Moq;
using NUnit.Framework;

namespace FragmentedFileUpload.Client.Tests
{
    [TestFixture]
    public class UploadClientTests
    {
        private Mock<IFileSystemService> _fileSystemMock;

        private static List<MultipartMemoryStreamProvider> RequestContents { get; } =
            new List<MultipartMemoryStreamProvider>();
        private static AuthenticationHeaderValue AuthorizationHeader { get; set; }
        private static string RequestUrl { get; set; }
        private static Func<HttpRequestMessage, HttpResponseMessage> ResponseMessageFactory { get; set; }

        private string BinFolder => AppDomain.CurrentDomain.BaseDirectory;


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

        [SetUp]
        public void Setup()
        {
            RequestUrl = null;
            RequestContents.Clear();

            _fileSystemMock = new Mock<IFileSystemService>();
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.OpenRead(It.IsAny<string>())).Returns((string s) => new MemoryStream(Encoding.UTF8.GetBytes(s)));
            _fileSystemMock.Setup(f => f.OpenOrCreate(It.IsAny<string>())).Returns(new MemoryStream());
            _fileSystemMock.Setup(f => f.GetDirectoryName(It.IsAny<string>())).Returns((string s) => s);
            _fileSystemMock.Setup(f => f.PathCombine(It.IsAny<string[]>())).Returns((string[] s) => Path.Combine(s));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenFilePathIsInvalid_AndUploadFileIsCalled_FailsWithInvalidOperationException(string filePath)
        {
            // Arrange
            var client = CreateUploadClient(filePath, "any", "temp", fileSystemService: _fileSystemMock.Object);

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile().Wait());
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenUrlIsInvalid_AndUploadFileIsCalled_FailsWithInvalidOperationException(string url)
        {
            // Arrange
            var client = CreateUploadClient("any", url, "temp", fileSystemService: _fileSystemMock.Object);

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile().Wait());
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenUrlIsInvalid_AndResumeUploadIsCalled_FailsWithInvalidOperationException(string url)
        {
            // Arrange
            var client = CreateUploadClient("any", url, "temp", fileSystemService: _fileSystemMock.Object);

            // Act
            Assert.Throws<AggregateException>(() => client.ResumeUpload().Wait());
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenTempPathIsInvalid_AndUploadFileIsCalled_FailsWithInvalidOperationException(string temp)
        {
            // Arrange
            var client = CreateUploadClient("any", "url", temp, fileSystemService: _fileSystemMock.Object);

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile().Wait());
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenTempPathIsInvalid_AndResumeUploadIsCalled_FailsWithInvalidOperationException(string temp)
        {
            // Arrange
            var client = CreateUploadClient("any", "url", temp, fileSystemService: _fileSystemMock.Object);

            // Act
            Assert.Throws<AggregateException>(() => client.ResumeUpload().Wait());
        }

        [Test]
        public void WhenFileDoesNotExist_AndUploadFileIsCalled_FailsWithInvalidOperationException()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false).Verifiable("FileExists not called.");
            var client = CreateUploadClient("file", "url", "temp", fileSystemService: _fileSystemMock.Object);

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile().Wait());

            // Assert
            _fileSystemMock.Verify();
        }

        [Test]
        public void WhenThereAreFilesInTempDir_AndResumeIsCalled_UploadsRemainingFilesWithValidHash()
        {
            // Arrange
            const string hash = "123456789";
            _fileSystemMock.Setup(f => f.GetDirectoriesInDirectory(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new[] { hash });
            _fileSystemMock.Setup(f => f.GetFilesInDirectory(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new[] {"file1", "file2", "file3"});
            _fileSystemMock.Setup(f => f.GetFileName(It.IsAny<string>())).Returns((string s) => Path.GetFileName(s));
            const string url = "https://this.is.a.valid/url";
            var client = CreateUploadClient("file", url, "temp", fileSystemService: _fileSystemMock.Object);

            // Act
            client.ResumeUpload().Wait();

            // Arrange
            Assert.AreEqual(3, RequestContents.Count);
            var file1Content =
                RequestContents.FirstOrDefault(
                    r => r.Contents.Any(c => c.Headers.ContentDisposition.FileName == "file1"));
            Assert.NotNull(file1Content);
            Assert.AreEqual(hash, file1Content.Contents.First(c => c.Headers.ContentDisposition.Name == "hash").ReadAsStringAsync().Result);
            var file2Content =
                RequestContents.FirstOrDefault(
                    r => r.Contents.Any(c => c.Headers.ContentDisposition.FileName == "file2"));
            Assert.NotNull(file2Content);
            Assert.AreEqual(hash, file2Content.Contents.First(c => c.Headers.ContentDisposition.Name == "hash").ReadAsStringAsync().Result);
            var file3Content =
                RequestContents.FirstOrDefault(
                    r => r.Contents.Any(c => c.Headers.ContentDisposition.FileName == "file2"));
            Assert.NotNull(file3Content);
            Assert.AreEqual(hash, file3Content.Contents.First(c => c.Headers.ContentDisposition.Name == "hash").ReadAsStringAsync().Result);
        }

        [Test]
        public void UploadFile_UploadsFileAsMultipartData_WithFileAndHashAndPartHash()
        {
            // Arrange
            const string fileName = "PictureQualitySmall1_2017.05.23_08-24-50.iclr";
            const string originalHash = "780faf7b15f08c3cce0fe02c26d932bbd36ffd393eee4e5e6c8e0e383a787fab";
            const string uploadUrl = "http://localhost:8170/Home/UploadFile/";
            var client =
                CreateUploadClient(Path.Combine(BinFolder, "TestData", fileName),
                    uploadUrl, Path.Combine(BinFolder, "Temp"));
            client.MaxChunkSizeMegaByte = 0.1;

            // Act
            client.UploadFile().Wait();

            // Assert
            Assert.AreEqual(3, RequestContents.Count);
            for (var i = 0; i < 3; i++)
            {
                var content = RequestContents[i];
                var file = content.Contents.FirstOrDefault(c => c.Headers.ContentDisposition.Name == "file");
                Assert.NotNull(file);
                Assert.AreEqual($"{fileName}{Naming.PartToken}{i + 1}.3", file.Headers.ContentDisposition.FileName);
                var hash = content.Contents.FirstOrDefault(c => c.Headers.ContentDisposition.Name == "hash");
                Assert.NotNull(hash);
                Assert.AreEqual(originalHash, hash.ReadAsStringAsync().Result);
                var partHash = content.Contents.FirstOrDefault(c => c.Headers.ContentDisposition.Name == "partHash");
                Assert.NotNull(partHash);
                using (var stream = file.ReadAsStreamAsync().Result)
                    Assert.AreEqual(stream.ComputeSha256Hash(), partHash.ReadAsStringAsync().Result);
            }
        }

        [Test]
        public void UploadFile_RequestHasValidUrl()
        {
            // Arrange
            const string url = "https://this.is.a.valid/url";
            var client = CreateUploadClient("path", url, "temp", fileSystemService: _fileSystemMock.Object);

            // Act
            client.UploadFile().Wait();

            // Assert
            Assert.AreEqual(url, RequestUrl);
        }

        [Test]
        public void WhenAuthorizationIsProvided_AndUploadFileIsCalled_RequestHasAuthorizationHeader()
        {
            // Arrange
            const string token = "bearer xyz12345";
            const string url = "https://this.is.a.valid/url";
            var client = CreateUploadClient("path", url, "temp", c => c.AuthorizeWith(token), fileSystemService: _fileSystemMock.Object);

            // Act
            client.UploadFile().Wait();

            // Assert
            Assert.AreEqual(token, $"{AuthorizationHeader.Scheme} {AuthorizationHeader.Parameter}");
        }

        [Test]
        public void UploadFile_WhenResponseStatusIsUnauthorized_InvokeRequestFailedCallback()
        {
            // Arrange
            const string url = "https://this.is.a.valid/url";
            var statusCode = HttpStatusCode.OK;
            var client = CreateUploadClient("path", url, "temp", fileSystemService: _fileSystemMock.Object, onRequestFailed: c => statusCode = c);
            ResponseMessageFactory = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);

            // Act
            client.UploadFile().Wait();

            // Assert
            Assert.AreEqual(HttpStatusCode.Unauthorized, statusCode);
        }

        [Test]
        public void UploadFile_WhenResponseStatusIsBadRequest_InvokeRequestFailedCallback()
        {
            // Arrange
            const string url = "https://this.is.a.valid/url";
            var statusCode = HttpStatusCode.OK;
            var client = CreateUploadClient("path", url, "temp", fileSystemService: _fileSystemMock.Object, onRequestFailed: c => statusCode = c);
            ResponseMessageFactory = _ => new HttpResponseMessage(HttpStatusCode.BadRequest);

            // Act
            client.UploadFile().Wait();

            // Assert
            Assert.AreEqual(HttpStatusCode.BadRequest, statusCode);
        }

        [Test]
        public void UploadFile_WhenResponseStatusIsInternalServerError_InvokeRequestFailedCallback()
        {
            // Arrange
            const string url = "https://this.is.a.valid/url";
            var statusCode = HttpStatusCode.OK;
            var client = CreateUploadClient("path", url, "temp", fileSystemService: _fileSystemMock.Object, onRequestFailed: c => statusCode = c);
            ResponseMessageFactory = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

            // Act
            client.UploadFile().Wait();

            // Assert
            Assert.AreEqual(HttpStatusCode.InternalServerError, statusCode);
        }

        [Test]
        public void UploadFile_WhenResponseStatusIsNotFound_InvokeRequestFailedCallback()
        {
            // Arrange
            const string url = "https://this.is.a.valid/url";
            var statusCode = HttpStatusCode.OK;
            var client = CreateUploadClient("path", url, "temp", fileSystemService: _fileSystemMock.Object, onRequestFailed: c => statusCode = c);
            ResponseMessageFactory = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

            // Act
            client.UploadFile().Wait();

            // Assert
            Assert.AreEqual(HttpStatusCode.NotFound, statusCode);
        }

        private class WebApiKeyHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                RequestUrl = request.RequestUri.AbsoluteUri;
                AuthorizationHeader = request.Headers.Authorization;
                RequestContents.Add(await request.Content.ReadAsMultipartAsync(cancellationToken));

                return ResponseMessageFactory?.Invoke(request) ?? request.CreateResponse(HttpStatusCode.OK);
            }
        }
    }
}
