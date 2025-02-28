using System.Text;

namespace Amiga.FileFormats.LHA;

public static class LHAReader
{
    public static ILHA LoadLHAFile(Stream stream) => LoadLHAFile(stream, null);

    public static ILHA LoadLHAFile(Stream stream, FileInfo? fileInfo)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        return new Archive(reader, fileInfo);
    }

    public static ILHA LoadLHAFile(string filename)
    {
        using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);
        return LoadLHAFile(stream, new FileInfo(filename));
    }
}