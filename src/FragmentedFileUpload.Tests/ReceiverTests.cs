using System;
using System.IO;
using System.Text;
using FragmentedFileUpload.Core;
using FragmentedFileUpload.Extensions;
using FragmentedFileUpload.Server;
using FragmentedFileUpload.Services;
using Moq;
using NUnit.Framework;

namespace FragmentedFileUpload.Tests
{
    [TestFixture]
    public class ReceiverTests
    {
        private Mock<IFileSystemService> _fileSystemMock;
        private Mock<IFileMerger> _fileMergerMock;

        private Receiver CreateReceiver(string tempPath, string hash)
        {
            return Receiver.Create(tempPath, s => {}, hash, _fileSystemMock.Object, (_,__) => _fileMergerMock.Object);
        }

        [SetUp]
        public void Setup()
        {
            _fileSystemMock = new Mock<IFileSystemService>();
            _fileSystemMock.Setup(f => f.GetDirectoryName(It.IsAny<string>())).Returns((string s) => s);
            _fileSystemMock.Setup(f => f.PathCombine(It.IsAny<string[]>())).Returns((string[] s) => Path.Combine(s));
            _fileSystemMock.Setup(f => f.CreateFile(It.IsAny<string>())).Returns((string s) => new MemoryStream());
            _fileSystemMock.Setup(f => f.GetFileName(It.IsAny<string>())).Returns("file.part_1.1");
            _fileSystemMock.Setup(f => f.EnumerateFilesInDirectory(It.IsAny<string>(), It.IsAny<string>())).Returns(new[]{"file.part_1.1"});
            _fileSystemMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(f => f.OpenRead(It.IsAny<string>()))
                .Returns((string s) => new MemoryStream(Encoding.UTF8.GetBytes("Some content.")));

            _fileMergerMock = new Mock<IFileMerger>();
            _fileMergerMock.Setup(m => m.MergeFile()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("Merged file content")));
        }

        [Test]
        public void WhenStreamIsInvalid_AndReceiveIsCalled_FailsWithArgumentException()
        {
            // Arrange
            var client = CreateReceiver("temp", "hash");

            // Act
            Assert.Throws<ArgumentException>(() => client.Receive(null, "file", "hash"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenFilePathIsInvalid_AndReceiveIsCalled_FailsWithArgumentException(string filePath)
        {
            // Arrange
            var client = CreateReceiver("temp", "hash");

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<ArgumentException>(() => client.Receive(stream, filePath, "hash"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenPartHashIsInvalid_AndReceiveIsCalled_FailsWithArgumentException(string hash)
        {
            // Arrange
            var client = CreateReceiver("temp", "hash");

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<ArgumentException>(() => client.Receive(stream, "file", hash));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenTempPathIsInvalid_AndReceiveIsCalled_FailsWithInvalidOperationException(string temp)
        {
            // Arrange
            var client = CreateReceiver(temp, "hash");

            // Act
            Assert.Throws<InvalidOperationException>(() => client.Receive(new MemoryStream(), "file", "hash"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenOutputPathIsInvalid_AndReceiveIsCalled_FailsWithInvalidOperationException(string output)
        {
            // Arrange
            var client = CreateReceiver("temp", "hash");

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<InvalidOperationException>(() => client.Receive(stream, "file", "hash"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenHashIsInvalid_AndReceiveIsCalled_FailsWithInvalidOperationException(string hash)
        {
            // Arrange
            var client = CreateReceiver("temp", hash);

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<InvalidOperationException>(() => client.Receive(stream, "file", "hash"));
        }

        [Test]
        public void Receive_PartHashIsNotEqual_FailsWithInvalidOperationException()
        {
            var partHash = new MemoryStream(Encoding.UTF8.GetBytes("Sum contänt.")).ComputeSha256Hash();
            var hash = new MemoryStream(Encoding.UTF8.GetBytes("Merged file content")).ComputeSha256Hash();
            // Arrange
            var client = CreateReceiver("temp", hash);

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<InvalidOperationException>(() => client.Receive(stream, "file", partHash));
        }

        [Test]
        public void Receive_HashIsNotEqual_FailsWithInvalidOperationException_TempFolderIsDeleted()
        {
            var partHash = new MemoryStream(Encoding.UTF8.GetBytes("Some content.")).ComputeSha256Hash();
            var hash = new MemoryStream(Encoding.UTF8.GetBytes("Mörgd fail contänt")).ComputeSha256Hash();
            // Arrange
            var client = CreateReceiver("temp", hash);

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<InvalidOperationException>(() => client.Receive(stream, "file", partHash));

            // Assert
            _fileSystemMock.Verify(f => f.DeleteDirectory(Path.Combine("temp", hash), true), Times.Once);
        }

        [Test]
        public void Receive_AfterSuccessfulMerge_TempFolderIsDeleted()
        {
            var partHash = new MemoryStream(Encoding.UTF8.GetBytes("Some content.")).ComputeSha256Hash();
            var hash = new MemoryStream(Encoding.UTF8.GetBytes("Merged file content")).ComputeSha256Hash();
            // Arrange
            var client = CreateReceiver("temp", hash);

            // Act
            using (var stream = new MemoryStream())
                client.Receive(stream, "file", partHash);

            // Assert
            _fileSystemMock.Verify(f => f.DeleteDirectory(Path.Combine("temp", hash), true), Times.Once);
        }
    }
}