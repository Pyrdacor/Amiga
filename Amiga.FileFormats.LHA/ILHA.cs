using Amiga.FileFormats.Core;

namespace Amiga.FileFormats.LHA
{
    public interface ILHA : IVirtualFileSystem
    {
        IFile[] GetAllFiles();
        IDirectory[] GetAllEmptyDirectories();
    }
}
