using System;
using System.IO;
using System.Text;
using Moq;
using NUnit.Framework;

namespace FragmentedFileUpload.Client.Tests
{
    [TestFixture]
    public class FileSplitterTests
    {
        private Mock<IFileSystemService> _fileSystemMock;
        private bool _firstTime;

        [SetUp]
        public void SetUp()
        {
            _fileSystemMock = new Mock<IFileSystemService>();
            _firstTime = true;
            _fileSystemMock.Setup(f => f.OpenOrCreate(It.IsAny<string>()))
                .Returns(() =>
                {
                    if (!_firstTime)
                        return new MemoryStream(Encoding.UTF8.GetBytes("anytime"));
                    _firstTime = false;
                    return new MemoryStream(Encoding.UTF8.GetBytes("forthefirsttime"));
                });
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenFilePathIsInvalid_AndSplitFileIsCalled_FailsWithInvalidOperationException(string filePath)
        {
            // Arrange
            var splitter = CreateSplitter(filePath, "Temp");

            // Act
            Assert.Throws<InvalidOperationException>(() => splitter.SplitFile());
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenTempPathIsInvalid_AndSplitFileIsCalled_FailsWithInvalidOperationException(string tempPath)
        {
            // Arrange
            var splitter = CreateSplitter("file", tempPath);

            // Act
            Assert.Throws<InvalidOperationException>(() => splitter.SplitFile());
        }

        [Test]
        public void WhenFileDoesNotExist_AndSplitIsCalled_FailsWithInvalidOperationException()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false).Verifiable("FileExists not called.");
            var splitter = CreateSplitter("file", "temp");

            // Act
            Assert.Throws<InvalidOperationException>(() => splitter.SplitFile());

            // Assert
            _fileSystemMock.Verify();
        }

        [Test]
        public void WhenTempDirectoryDoesNotExist_AndSplitIsCalled_TempDirectoryIsCreated()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(false)
                .Verifiable("DirectoryExists not called.");
            _fileSystemMock.Setup(f => f.CreateDirectory(It.IsAny<string>())).Verifiable("CreateDirectory not called.");

            var splitter = CreateSplitter("file", "temp");

            // Act
            splitter.SplitFile();

            // Assert
            _fileSystemMock.Verify();
        }

        private FileSplitter CreateSplitter(string filePath, string tempPath)
        {
            var splitter = FileSplitter.Create(filePath, tempPath, _fileSystemMock.Object);
            return splitter;
        }
    }
}
