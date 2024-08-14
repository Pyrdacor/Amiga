namespace Amiga.FileFormats.ADF
{
    public static class ADFReader
    {
        public static IADF LoadADFFile(Stream stream, bool allowInvalidChecksum = false)
        {
            byte[] buffer = new byte[stream.Length - stream.Position];
            stream.Read(buffer, 0, buffer.Length);
            return new ADF(buffer, allowInvalidChecksum);
        }

        public static IADF LoadADFFile(string filename, bool allowInvalidChecksum = false)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            return LoadADFFile(stream, allowInvalidChecksum);
        }
    }
}