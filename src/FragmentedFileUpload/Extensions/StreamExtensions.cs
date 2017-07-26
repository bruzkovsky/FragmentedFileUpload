using System;
using System.IO;
using System.Security.Cryptography;
using FragmentedFileUpload.Constants;

namespace FragmentedFileUpload.Extensions
{
    public static class StreamExtensions
    {
        public static string ComputeSha256Hash(this Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using (var algorithm = SHA256.Create())
            {
                var hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);
                return hash;
            }
        }
    }

    public static class StringExtensions
    {
        public static string GetBaseName(this string partName)
        {
            if (!partName.Contains(Naming.PartToken))
                return partName;
            return partName.Remove(partName.IndexOf(Naming.PartToken, StringComparison.Ordinal));
        }
    }
}