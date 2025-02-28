namespace Amiga.FileFormats.ADF;

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

    public static void Write(DataWriter writer, int fileHeaderSector, int index, int nextDataSector, byte[] data, ref int dataIndex, FileSystem fileSystem)
    {
        int maxSize = data.Length - dataIndex;

        if (fileSystem == FileSystem.FFS)
        {
            int size = Math.Min(SectorDataProvider.SectorSize, maxSize);
            writer.WriteBytes(data, dataIndex, size);
            dataIndex += size;
        }
        else
        {
            writer.WriteDword(8); // T_DATA
            writer.WriteDword((uint)fileHeaderSector);
            writer.WriteDword((uint)index);
            int size = Math.Min(SectorDataProvider.SectorSize - 24, maxSize);
            writer.WriteDword((uint)size);
            writer.WriteDword((uint)nextDataSector);
            int checksumPosition = writer.Position;
            writer.WriteDword(0); // checksum placeholder

            writer.WriteBytes(data, dataIndex, size);
            dataIndex += size;

            if (size < SectorDataProvider.SectorSize - 24)
            {
                int sizeToFill = SectorDataProvider.SectorSize - 24 - size;
                writer.WriteBytes(Enumerable.Repeat((byte)0, sizeToFill).ToArray());
            }

            uint checksum = RootBlock.CalculateChecksum(writer.ToArray());
            writer.Position = checksumPosition;
            writer.WriteDword(checksum);
            writer.Position = SectorDataProvider.SectorSize;
        }
    }
}
