namespace Amiga.FileFormats.ADF
{
    internal class FileExtensionBlock
    {
        public byte[] Data { get; }
        public uint NextExtensionBlock { get; }
        public uint NextExpectedDataBlock { get; }

        public FileExtensionBlock(uint sector, uint fileHeaderSector, RootBlock rootBlock,
            SectorDataProvider sectorDataProvider, byte[] blockData, bool allowInvalidChecksum,
            uint nextExpectedDataBlock, uint startIndex)
        {
            var reader = new DataReader(blockData);

            if (reader.ReadDword() != 16 || // T_LIST
                reader.ReadDword() != sector)
                throw new InvalidDataException("File extension block header is invalid.");

            uint numDataBlocks = reader.ReadDword();

            if (reader.ReadDword() != 0 ||
                reader.ReadDword() != 0)
                throw new InvalidDataException("File extension block header is invalid.");

            uint checkSum = reader.ReadDword();

            if (!allowInvalidChecksum)
            {
                if (checkSum != RootBlock.CalculateChecksum(blockData))
                    throw new InvalidDataException("Invalid file extension block checksum.");
            }

            int position = reader.Position;
            reader.Position += 71 * 4; // go to the first data pointer
            var fileData = new List<byte>();

            for (int i = 0; i < Math.Min(72, numDataBlocks); ++i)
            {
                uint dataSector = reader.PeekDword();
                var data = sectorDataProvider.GetSectorData((int)dataSector, 1);

                if (rootBlock.FileSystem == FileSystem.FFS)
                    fileData.AddRange(data);
                else
                {
                    if (nextExpectedDataBlock != dataSector)
                        throw new InvalidDataException("Data sector links are broken.");
                    var dataBlock = new DataBlock(fileHeaderSector, startIndex + (uint)i + 1u, data, allowInvalidChecksum);
                    fileData.AddRange(dataBlock.Data);
                    nextExpectedDataBlock = dataBlock.NextDataBlock;
                }

                reader.Position -= 4;
            }

            NextExpectedDataBlock = nextExpectedDataBlock;

            Data = fileData.ToArray();

            reader.Position = position + 72 * 4 + 47 * 4; // skip unused bytes

            if (reader.ReadDword() != fileHeaderSector)
                throw new InvalidDataException("File extension block is invalid.");

            NextExtensionBlock = reader.ReadDword();

            if (reader.ReadDword() != unchecked((uint)-3)) // ST_FILE
                throw new InvalidDataException("Invalid file extension block subtype.");
        }

        public static void Write(DataWriter writer, int fileHeaderSector, int sector, List<int> dataSectors, int nextExtensionBlockSector)
        {
            writer.WriteDword(16); // T_LIST
            writer.WriteDword((uint)sector);
            writer.WriteDword((uint)dataSectors.Count);
            writer.WriteDword(0);
            writer.WriteDword(0);
            int checksumPosition = writer.Position;
            writer.WriteDword(0); // checksum placeholder

            // Write the data sectors
            for (int i = 0; i < 72; ++i)
            {
                int dataSectorIndex = 71 - i;
                writer.WriteDword(dataSectorIndex < dataSectors.Count ? (uint)dataSectors[dataSectorIndex] : 0);
            }

            for (int i = 0; i < 47; ++i)
                writer.WriteDword(0);

            writer.WriteDword((uint)fileHeaderSector);
            writer.WriteDword((uint)nextExtensionBlockSector);
            writer.WriteDword(unchecked((uint)-3)); // ST_FILE

            uint checksum = RootBlock.CalculateChecksum(writer.ToArray());
            writer.Position = checksumPosition;
            writer.WriteDword(checksum);
            writer.Position = SectorDataProvider.SectorSize;
        }
    }
}
