using System;
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
            var client = CreateUploadClient(filePath, "any");

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile().Wait());
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenUrlIsInvalid_AndSplitFileIsCalled_FailsWithInvalidOperationException(string url)
        {
            // Arrange
            var client = CreateUploadClient("any", url);

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile().Wait());
        }

        private UploadClient CreateUploadClient(string filePath, string url)
        {
            return UploadClient.Create(filePath, url, fileSystemService: _fileSystemMock.Object);
        }

        [Test]
        public void WhenFileDoesNotExist_AndSplitIsCalled_FailsWithInvalidOperationException()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false).Verifiable("FileExists not called.");
            var client = CreateUploadClient("file", "temp");

            // Act
            Assert.Throws<AggregateException>(() => client.UploadFile().Wait());

            // Assert
            _fileSystemMock.Verify();
        }
    }
}
