using System;
using System.IO;
using System.Security.Cryptography;

namespace FragmentedFileUpload.Extensions
{
    public static class StreamExtensions
    {
        public static string ComputeSha256Hash(this Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
