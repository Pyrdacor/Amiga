namespace Amiga.FileFormats.ADF
{
    public interface IADF
    {
        FileSystem FileSystem { get; }
        bool InternationalMode { get; }
        bool DirectoryCache { get; }
        bool LongFileNames { get; }
        byte[] BootCode { get; }
        int DiskSize { get; }
        IDirectory RootDirectory { get; }
        DateTime LastModificationDate { get; }
        DateTime CreationDate { get; }
    }
}
