namespace Amiga.FileFormats.ADF
{
    public class ADFReader
    {
        public static IADF LoadADFFile(Stream stream, bool allowInvalidChecksum)
        {
            byte[] buffer = new byte[stream.Length - stream.Position];
            stream.Read(buffer, 0, buffer.Length);
            return new ADF(buffer, allowInvalidChecksum);
        }

        public static IADF LoadADFFile(string filename, bool allowInvalidChecksum)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            return LoadADFFile(stream, allowInvalidChecksum);
        }
    }
}