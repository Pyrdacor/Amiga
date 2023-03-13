namespace Amiga.FileFormats.ADF
{
    internal class BootBlock
    {
        public const int Size = 1024; // always for floppies
        public char[] DiskType { get; }
        public FileSystem FileSystem { get; }
        public bool InternationalMode { get; }
        public bool DirectoryCache { get; }
        public bool LongFileNames { get; }
        public string BootCode { get; }

        public BootBlock(SectorDataProvider sectorDataProvider, bool allowInvalidChecksum)
        {
            // The bootblock is always located in the first two blocks.
            var blockData = sectorDataProvider.GetSectorData(0, 2);
            var reader = new DataReader(blockData);

            DiskType = reader.ReadChars(4);

            if (DiskType[0] != 'D' || DiskType[1] != 'O' || DiskType[2] != 'S')
                throw new InvalidDataException("Not a valid ADF file.");

            int flags = DiskType[3];

            FileSystem = (FileSystem)(flags & 0x1);
            InternationalMode = flags >= 2;
            DirectoryCache = flags >= 4;
            LongFileNames = flags >= 6;

            uint checkSum = reader.ReadDword();

            if (!allowInvalidChecksum)
            {
                byte[] data = new byte[blockData.Length];
                Buffer.BlockCopy(blockData, 0, data, 0, blockData.Length);

                // Clear the checksum for calculations
                data[4] = 0;
                data[5] = 0;
                data[6] = 0;
                data[7] = 0;

                if (checkSum != CalculateChecksum(data))
                    throw new InvalidDataException("Invalid bootblock checksum.");
            }

            uint rootBlockSector = reader.ReadDword();

            if (rootBlockSector != 880) // this is also true for HD disks
                throw new InvalidDataException("Invalid rootblock sector location.");

            BootCode = new string(reader.ReadChars(Size - 12)).TrimEnd('\0');
        }

        private static uint CalculateChecksum(byte[] data)
        {
            var reader = new DataReader(data);
            uint checksum = 0;
            uint precsum;

            for (int i = 0; i < Size / sizeof(uint); ++i)
            {
                precsum = checksum;

                if ((checksum += reader.ReadDword()) < precsum)
                    ++checksum;
            }
            return ~checksum;
        }
    }
}
