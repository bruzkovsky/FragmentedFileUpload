using System;
using System.IO;
using System.Text;
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

        private Receiver CreateReceiver(string tempPath, string outputPath, string hash)
        {
            return Receiver.Create(tempPath, outputPath, hash, _fileSystemMock.Object);
        }

        [SetUp]
        public void Setup()
        {
            _fileSystemMock = new Mock<IFileSystemService>();
            _fileSystemMock.Setup(f => f.GetDirectoryName(It.IsAny<string>())).Returns((string s) => s);
            _fileSystemMock.Setup(f => f.PathCombine(It.IsAny<string[]>())).Returns((string[] s) => Path.Combine(s));
            _fileSystemMock.Setup(f => f.CreateFile(It.IsAny<string>())).Returns((string s) => new MemoryStream());
            _fileSystemMock.Setup(f => f.OpenRead(It.IsAny<string>()))
                .Returns((string s) => new MemoryStream(Encoding.UTF8.GetBytes(s)));
        }

        [Test]
        public void WhenStreamIsInvalid_AndUploadFileIsCalled_FailsWithArgumentException()
        {
            // Arrange
            var client = CreateReceiver("temp", "output", "hash");

            // Act
            Assert.Throws<ArgumentException>(() => client.Receive(null, "file", "hash"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenFilePathIsInvalid_AndUploadFileIsCalled_FailsWithArgumentException(string filePath)
        {
            // Arrange
            var client = CreateReceiver("temp", "output", "hash");

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<ArgumentException>(() => client.Receive(stream, filePath, "hash"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenPartHashIsInvalid_AndUploadFileIsCalled_FailsWithArgumentException(string hash)
        {
            // Arrange
            var client = CreateReceiver("temp", "output", "hash");

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<ArgumentException>(() => client.Receive(stream, "file", hash));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenTempPathIsInvalid_AndUploadFileIsCalled_FailsWithInvalidOperationException(string temp)
        {
            // Arrange
            var client = CreateReceiver(temp, "output", "hash");

            // Act
            Assert.Throws<InvalidOperationException>(() => client.Receive(new MemoryStream(), "file", "hash"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenOutputPathIsInvalid_AndUploadFileIsCalled_FailsWithInvalidOperationException(string output)
        {
            // Arrange
            var client = CreateReceiver("temp", output, "hash");

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<InvalidOperationException>(() => client.Receive(stream, "file", "hash"));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void WhenHashIsInvalid_AndUploadFileIsCalled_FailsWithInvalidOperationException(string hash)
        {
            // Arrange
            var client = CreateReceiver("temp", "output", hash);

            // Act
            using (var stream = new MemoryStream())
                Assert.Throws<InvalidOperationException>(() => client.Receive(stream, "file", "hash"));
        }
    }
}