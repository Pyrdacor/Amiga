namespace Amiga.FileFormats.Core
{
    public interface IFile : IDirectoryEntry
    {
        int Size { get; }
        byte[] Data { get; }
    }
}
