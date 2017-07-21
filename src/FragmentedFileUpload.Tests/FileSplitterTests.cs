using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FragmentedFileUpload.Constants;
using FragmentedFileUpload.Core;
using FragmentedFileUpload.Services;
using Moq;
using NUnit.Framework;

namespace FragmentedFileUpload.Tests
{
    [TestFixture]
    public class FileSplitterTests
    {
        private Mock<IFileSystemService> _fileSystemMock;

        [SetUp]
        public void SetUp()
        {
            _fileSystemMock = new Mock<IFileSystemService>();
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.OpenOrCreate(It.IsAny<string>())).Returns((string s) => new MemoryStream());
            _fileSystemMock.Setup(f => f.OpenRead(It.IsAny<string>()))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes("This is some readable content.")));
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
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false)
                .Verifiable("FileExists not called.");
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
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(false)
                .Verifiable("DirectoryExists not called.");
            _fileSystemMock.Setup(f => f.CreateDirectory(It.IsAny<string>())).Verifiable("CreateDirectory not called.");

            var splitter = CreateSplitter("file", "temp");

            // Act
            splitter.SplitFile();

            // Assert
            _fileSystemMock.Verify();
        }

        [Test]
        public void SplitFile_FilePartsHaveRightPaths()
        {
            // Arrange
            _fileSystemMock.Setup(f => f.CreateDirectory(It.IsAny<string>())).Verifiable("CreateDirectory not called.");
            _fileSystemMock.Setup(f => f.GetFileName(It.IsAny<string>())).Returns((string s) => Path.GetFileName(s))
                .Verifiable("GetFileName not called.");
            // create a 1MB file with random content
            var bigBytes = new byte[1024 * 1024];
            new Random().NextBytes(bigBytes);
            _fileSystemMock.Setup(f => f.OpenRead(It.IsAny<string>())).Returns(new MemoryStream(bigBytes));
            _fileSystemMock.Setup(f => f.PathCombine(It.IsAny<string[]>())).Returns((string[] s) => Path.Combine(s));

            const string fileName = "file";
            const string tempFolder = "temp";
            var fileList = new List<string>();

            _fileSystemMock.Setup(f => f.OpenOrCreate(It.IsAny<string>())).Callback((string s) => fileList.Add(s))
                .Returns((string s) => new MemoryStream());

            var splitter = CreateSplitter(fileName, tempFolder);
            splitter.MaxChunkSizeMegaByte = 0.1;

            // Act
            var parts = splitter.SplitFile();

            // Assert
            Assert.AreEqual(10, parts);
            Assert.AreEqual(10, fileList.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    $"{tempFolder}\\{fileName}{Naming.PartToken}01.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}02.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}03.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}04.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}05.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}06.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}07.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}08.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}09.10",
                    $"{tempFolder}\\{fileName}{Naming.PartToken}10.10"
                }, fileList);
            _fileSystemMock.Verify();
        }

        private FileSplitter CreateSplitter(string filePath, string tempPath)
        {
            var splitter = FileSplitter.Create(filePath, tempPath, _fileSystemMock.Object);
            return splitter;
        }
    }
}