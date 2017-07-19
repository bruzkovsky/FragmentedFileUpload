using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FragmentedFileUpload.Constants;
using FragmentedFileUpload.Extensions;
using NUnit.Framework;

namespace FragmentedFileUpload.Client.Tests
{
    [TestFixture]
    [Category("Integration")]
    public class UploadClientIntegrationTests
    {
        public static List<MultipartMemoryStreamProvider> RequestContents { get; } =
            new List<MultipartMemoryStreamProvider>();

        private string BinFolder => AppDomain.CurrentDomain.BaseDirectory;

        private UploadClient CreateUploadClient(string filePath, string url, string tempPath)
        {
            return UploadClient.Create(filePath, url, tempPath,
                httpClient: () => new HttpClient(new WebApiKeyHandler()));
        }

        [SetUp]
        public void Setup()
        {
            RequestContents.Clear();
        }

        [Test]
        public void UploadFile_UploadsFileAsMultipartData_WithFileAndHashAndPartHash()
        {
            // Arrange
            const string fileName = "PictureQualitySmall1_2017.05.23_08-24-50.iclr";
            const string originalHash = "780faf7b15f08c3cce0fe02c26d932bbd36ffd393eee4e5e6c8e0e383a787fab";
            var client =
                CreateUploadClient(Path.Combine(BinFolder, "TestData", fileName),
                    "http://localhost:8170/Home/UploadFile/", Path.Combine(BinFolder, "Temp"));
            client.MaxChunkSizeMegaByte = 0.1;

            // Act
            client.UploadFile(CancellationToken.None).Wait();

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

        private class WebApiKeyHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                RequestContents.Add(await request.Content.ReadAsMultipartAsync(cancellationToken));

                return request.CreateResponse(HttpStatusCode.OK);
            }
        }
    }
}