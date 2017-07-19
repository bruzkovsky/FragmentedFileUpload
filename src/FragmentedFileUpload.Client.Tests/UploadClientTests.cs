using System;
using System.Threading;
using Moq;
using NUnit.Framework;

namespace FragmentedFileUpload.Client.Tests
{
    [TestFixture]
    public class UploadClientTests
    {
        private Mock<IFileSystemService> _fileSystemMock;

        [SetUp]
        public void SetUp()
        {
            _fileSystemMock = new Mock<IFileSystemService>();
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenFilePathIsInvalid_AndUploadFileIsCalled_FailsWithInvalidOperationException(string filePath)
        {
            // Arrange
            var client = CreateUploadClient(filePath, "any", "temp");

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile(CancellationToken.None).Wait());
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenUrlIsInvalid_AndSplitFileIsCalled_FailsWithInvalidOperationException(string url)
        {
            // Arrange
            var client = CreateUploadClient("any", url, "temp");

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile(CancellationToken.None).Wait());
        }

        private UploadClient CreateUploadClient(string filePath, string url, string tempPath)
        {
            return UploadClient.Create(filePath, url, tempPath, fileSystemService: _fileSystemMock.Object);
        }

        [Test]
        public void WhenFileDoesNotExist_AndSplitIsCalled_FailsWithInvalidOperationException()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false).Verifiable("FileExists not called.");
            var client = CreateUploadClient("file", "url", "temp");

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile(CancellationToken.None).Wait());

            // Assert
            _fileSystemMock.Verify();
        }
    }
}
