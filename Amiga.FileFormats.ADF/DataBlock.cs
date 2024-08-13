namespace Amiga.FileFormats.ADF
{
    // This is only used for OFS or by file extension blocks in OFS and FFS.
    internal class DataBlock
    {
        public byte[] Data { get; }
        public uint NextDataBlock { get; }

        public DataBlock(uint headerBlockSector, uint expectedIndex, byte[] blockData, bool allowInvalidChecksum)
        {
            var reader = new DataReader(blockData);

            if (reader.ReadDword() != 8 || // T_DATA
                reader.ReadDword() != headerBlockSector ||
                reader.ReadDword() != expectedIndex)
                throw new InvalidDataException("Data block header is invalid.");

            uint size = reader.ReadDword();

            if (size > 512 - 24)
                throw new InvalidDataException("Invalid data size in data block.");

            NextDataBlock = reader.ReadDword();

            uint checkSum = reader.ReadDword();

            if (!allowInvalidChecksum)
            {
                if (checkSum != RootBlock.CalculateChecksum(blockData))
                    throw new InvalidDataException("Invalid data block checksum.");
            }

            Data = new byte[size];

            Buffer.BlockCopy(blockData, 24, Data, 0, Data.Length);
        }
    }
}
