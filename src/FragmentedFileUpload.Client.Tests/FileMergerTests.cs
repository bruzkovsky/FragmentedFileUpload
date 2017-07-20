using System;
using System.IO;
using Moq;
using NUnit.Framework;

namespace FragmentedFileUpload.Client.Tests
{
    [TestFixture]
    public class FileMergerTests
    {
        private Mock<IFileSystemService> _fileSystemMock;

        [SetUp]
        public void SetUp()
        {
            _fileSystemMock = new Mock<IFileSystemService>();
            _fileSystemMock.Setup(f => f.CreateFile(It.IsAny<string>()))
                .Returns(() => new MemoryStream());
            _fileSystemMock.Setup(f => f.GetFilesInDirectory(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new[]
                {
                    "any.part_01.11", "any.part_02.11", "any.part_03.11", "any.part_04.11", "any.part_05.11", "any.part_06.11",
                    "any.part_07.11", "any.part_08.11", "any.part_09.11", "any.part_10.11", "any.part_11.11"
                });
            _fileSystemMock.Setup(f => f.GetDirectoryName(It.IsAny<string>())).Returns((string s) => s);
            _fileSystemMock.Setup(f => f.GetFileName(It.IsAny<string>())).Returns((string s) => s);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenInputPathIsInvalid_AndMergeFileIsCalled_FailsWithInvalidOperationException(string inputPath)
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var merger = CreateMerger(inputPath, "any");

            // Act
            Assert.Throws<InvalidOperationException>(() => merger.MergeFile());
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenOutputFilePathIsInvalid_AndMergeFileIsCalled_FailsWithInvalidOperationException(
            string outputFilePath)
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            var merger = CreateMerger("input.part_1.1", outputFilePath);

            // Act
            Assert.Throws<InvalidOperationException>(() => merger.MergeFile());
        }

        [Test]
        public void WhenInputPathDoesNotExist_AndMergeFileIsCalled_FailsWithInvalidOperationException()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(false)
                .Verifiable("FileExists not called.");
            var merger = CreateMerger("input.part_1.1", "any");

            // Act
            Assert.Throws<DirectoryNotFoundException>(() => merger.MergeFile());

            // Assert
            _fileSystemMock.Verify();
        }

        [Test]
        public void WhenOutputDirectoryDoesNotExist_AndMergeFileIsCalled_OutputDirectoryIsCreated()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.DirectoryExists("input.part_1.1")).Returns(true).Verifiable("DirectoryExist with input not called.");
            _fileSystemMock.Setup(f => f.GetDirectoryName("output")).Returns("output");
            _fileSystemMock.Setup(f => f.DirectoryExists("output")).Returns(false)
                .Verifiable("DirectoryExists not called.");
            _fileSystemMock.Setup(f => f.CreateDirectory(It.IsAny<string>())).Verifiable("CreateDirectory not called.");

            var merger = CreateMerger("input.part_1.1", "output");

            // Act
            merger.MergeFile();

            // Assert
            _fileSystemMock.Verify();
        }

        [Test]
        public void WhenAllParametersCorrect_AndMergeIsCalled_FilesGetMergedToOutputPathInRightOrder()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true).Verifiable();
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true)
                .Verifiable();
            var callOrder = 0;
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_01.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(0, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_02.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(1, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_03.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(2, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_04.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(3, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_05.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(4, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_06.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(5, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_07.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(6, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_08.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(7, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_09.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(8, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_10.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(9, callOrder++)).Verifiable();
            _fileSystemMock.Setup(f => f.CopyFileToStream("any.part_11.11", It.IsAny<Stream>()))
                .Callback(() => Assert.AreEqual(10, callOrder++)).Verifiable();

            var merger = CreateMerger("input.part_1.1", "output");

            // Act
            merger.MergeFile();

            // Assert
            _fileSystemMock.Verify();
        }

        private FileMerger CreateMerger(string inputPath, string outputFilePath)
        {
            var merger = FileMerger.Create(inputPath, outputFilePath, _fileSystemMock.Object);
            return merger;
        }
    }
}