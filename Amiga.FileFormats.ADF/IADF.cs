using Amiga.FileFormats.Core;

namespace Amiga.FileFormats.ADF
{
    public interface IADF : IVirtualFileSystem
    {
        FileSystem FileSystem { get; }
        bool InternationalMode { get; }
        bool DirectoryCache { get; }
        bool LongFileNames { get; }
        byte[] BootCode { get; }
        int DiskSize { get; }
        DateTime? LastModificationDate { get; }
        DateTime? CreationDate { get; }
        string DiskName { get; }
    }
}
