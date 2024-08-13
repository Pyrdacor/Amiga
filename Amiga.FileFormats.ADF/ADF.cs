using Amiga.FileFormats.Core;

namespace Amiga.FileFormats.ADF
{
    internal class ADF : IADF
    {
        private readonly BootBlock bootBlock;
        private readonly RootBlock rootBlock;

        public FileSystem FileSystem => bootBlock.FileSystem;

        public bool InternationalMode => bootBlock.InternationalMode;

        public bool DirectoryCache => bootBlock.DirectoryCache;

        public bool LongFileNames => bootBlock.LongFileNames;

        public byte[] BootCode => bootBlock.BootCode;

        public IDirectory RootDirectory => rootBlock;

        public DateTime LastModificationDate => rootBlock.DiskLastModificationDate;

        public DateTime CreationDate => rootBlock.CreationDate;

        public int DiskSize { get; }

        public const int SizeDD = 512 * 11 * 2 * 80;
        public const int SizeHD = 512 * 22 * 2 * 80;

        public ADF(byte[] data, bool allowInvalidChecksum)
        {
            if (data.Length != SizeDD && data.Length != SizeHD)
                throw new InvalidDataException("The given data size does not match a DD or HD disk size.");

            var sectorDataProvider = new SectorDataProvider(data);
            bootBlock = new BootBlock(sectorDataProvider, allowInvalidChecksum);
            rootBlock = new RootBlock(sectorDataProvider, data.Length == SizeHD, bootBlock.FileSystem,
                bootBlock.DirectoryCache, bootBlock.InternationalMode, allowInvalidChecksum);
            DiskSize = data.Length;
        }
    }
}
