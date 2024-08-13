namespace Amiga.FileFormats.Core
{
    public interface IVirtualFileSystem
    {
        IDirectory RootDirectory { get; }
    }
}
