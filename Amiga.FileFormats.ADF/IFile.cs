namespace Amiga.FileFormats.ADF
{
    public interface IFile : IDirectoryEntry
    {
        int Size { get; }
        byte[] Data { get; }
    }
}
