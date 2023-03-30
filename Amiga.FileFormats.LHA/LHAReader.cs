using System.Text;

namespace Amiga.FileFormats.LHA
{
    public static class LHAReader
    {
        public static ILHA LoadADFFile(Stream stream) => LoadADFFile(stream, null);

        public static ILHA LoadADFFile(Stream stream, FileInfo? fileInfo)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            return new Archive(reader, fileInfo);
        }

        public static ILHA LoadADFFile(string filename)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            return LoadADFFile(stream, new FileInfo(filename));
        }
    }
}