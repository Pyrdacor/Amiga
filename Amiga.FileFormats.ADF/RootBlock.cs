namespace Amiga.FileFormats.ADF
{
    internal class RootBlock : BaseDirectory
    {
        public const int Size = SectorDataProvider.SectorSize;
        public override DateTime LastModificationDate { get; }
        public DateTime DiskLastModificationDate { get; }
        public override DateTime CreationDate { get; }
        public override IDirectory? Parent => null;
        public override string Name => "<root>";
        public override string Path => "/";
        public override string Comment => "";
        internal override uint Sector { get; }
        public FileSystem FileSystem { get; }

        public RootBlock(SectorDataProvider sectorDataProvider, bool hd, FileSystem fileSystem,
            bool dirCache, bool internationalMode, bool allowInvalidChecksum)
            : base(sectorDataProvider, dirCache, internationalMode, allowInvalidChecksum)
        {
            // The bootblock is always located in the first two blocks.
            Sector = hd ? 1760u : 880u;
            FileSystem = fileSystem;
            var blockData = sectorDataProvider.GetSectorData((int)Sector, 1);
            var reader = new DataReader(blockData);

            if (reader.ReadDword() != 2 || // T_HEADER
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 72 ||
                reader.ReadDword() != 0) 
                throw new InvalidDataException("Root block header is invalid.");

            uint checkSum = reader.ReadDword();

            if (!allowInvalidChecksum)
            {
                if (checkSum != CalculateChecksum(blockData))
                    throw new InvalidDataException("Invalid root block checksum.");
            }

            // Read the hash table (72 longs)
            HashTable = Enumerable.Range(0, 72).Select(_ => reader.ReadDword()).ToArray();

            reader.Position += 27 * 4; // skip the bitmap data for now

            LastModificationDate = Util.ReadDateTime(reader);

            int volumeNameLength = Math.Min(30, (int)reader.ReadByte());
            int position = reader.Position;
            string name = new string(reader.ReadChars(volumeNameLength)).TrimEnd('\0');
            reader.Position = position + 30;

            if (reader.ReadByte() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0)
                throw new InvalidDataException("Root block is invalid.");

            DiskLastModificationDate = Util.ReadDateTime(reader);
            CreationDate = Util.ReadDateTime(reader);

            if (reader.ReadDword() != 0 ||
                reader.ReadDword() != 0)
                throw new InvalidDataException("Root block is invalid.");

            uint firstDirCacheBlockPointer = reader.ReadDword(); // TODO: support this?

            if ((firstDirCacheBlockPointer != 0) != dirCache)
                throw new InvalidDataException("Dir cache settings mismatches the existence of a dir cache pointer.");

            if (reader.ReadDword() != 1) // ST_ROOT
                throw new InvalidDataException("Invalid root block subtype.");
        }

        internal static uint CalculateChecksum(byte[] blockData)
        {
            byte[] data = new byte[blockData.Length];
            Buffer.BlockCopy(blockData, 0, data, 0, blockData.Length);

            // Clear the checksum for calculations
            data[20] = 0;
            data[21] = 0;
            data[22] = 0;
            data[23] = 0;

            var reader = new DataReader(data);
            uint checksum = 0;

            for (int i = 0; i < Size / sizeof(uint); ++i)
                checksum += reader.ReadDword();

            return unchecked((uint)-checksum);
        }
    }
}
