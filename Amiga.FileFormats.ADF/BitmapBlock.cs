namespace Amiga.FileFormats.ADF;

internal class BitmapBlock
{
    public const int Size = SectorDataProvider.SectorSize;

    private readonly byte[] bitmapData;

    public uint Sector { get; }

    public bool IsSectorFree(uint sector)
    {
        sector -= 2; // skip boot block sectors (even if not used)

        int longIndex = (int)(sector / 32);
        int byteIndex = longIndex * 4;

        byteIndex += ((int)sector % 32) / 8;

        if (byteIndex >= bitmapData.Length)
            throw new ArgumentOutOfRangeException(nameof(sector), "Relative sector is out of bounds.");

        byte b = bitmapData[byteIndex];
        byte mask = (byte)(1 << (int)(sector % 8));

        return (b & mask) != 0; // if the bit is 0, the sector is allocated, so free is != 0.

    }

    public BitmapBlock(SectorDataProvider sectorDataProvider, uint sector, bool allowInvalidChecksum)
    {
        Sector = sector;

        var blockData = sectorDataProvider.GetSectorData((int)Sector, 1);
        var reader = new DataReader(blockData);

        uint checkSum = reader.ReadDword();

        bitmapData = reader.ReadBytes(508);

        if (!allowInvalidChecksum)
        {
            if (checkSum != CalculateChecksum(bitmapData))
                throw new InvalidDataException("Invalid bitmap block checksum.");
        }
    }

    /// <summary>
    /// Creates a new bitmap block based on the allocated sectors.
    /// </summary>
    /// <param name="hd">Specifies if it is a HD disk, otherwise it is a DD disk.</param>
    /// <param name="allocatedSectorCount">Total amount of allocated sectors, including root and bitmap block but not boot blocks.</param>
    internal BitmapBlock(bool hd, int allocatedSectorCount)
    {
        bitmapData = new byte[508];

        int halfSectorCount = hd ? 1760 : 880;
        int sectorCount = halfSectorCount * 2;
        int endSector = (halfSectorCount + allocatedSectorCount) % sectorCount;

        if (endSector < halfSectorCount)
            endSector += 2; // skip boot blocks if in first disk half

        int bitmapDataIndex = 0;

        int firstHalfEmptyLongs = endSector == 2 || endSector > halfSectorCount ? (halfSectorCount - 2) / 32 : 0;
        
        for (int i = 0; i < firstHalfEmptyLongs * 4; i++)
        {
            // mark as free
            bitmapData[bitmapDataIndex++] = 0xff;
        }

        if (firstHalfEmptyLongs == 0 && endSector >= 34)
        {
            int firstHalfAllocatedLongs = Math.Min((endSector - 2) / 32, (halfSectorCount - 2) / 32);

            // Note: No need to set all the bytes to 0, as they already are initialized to 0.
            // Just increase the bitmapDataIndex.
            bitmapDataIndex += firstHalfAllocatedLongs * 4;            
        }

        // The rest is done sector by sector
        int totalLongs = ((sectorCount - 2) + 31) / 32;
        int currentLong = bitmapDataIndex / 4;
        int currentSector = 2 + bitmapDataIndex * 8;

        while (currentLong++ < totalLongs)
        {
            uint mask = 0;

            for (int i = 0; i < 32 && currentSector < sectorCount; i++, currentSector++)
            {
                if (IsSectorFree(currentSector))
                    mask |= (uint)(1 << i); // mark as free
            }

            bitmapData[bitmapDataIndex++] = (byte)((mask >> 24) & 0xff);
            bitmapData[bitmapDataIndex++] = (byte)((mask >> 16) & 0xff);
            bitmapData[bitmapDataIndex++] = (byte)((mask >> 8) & 0xff);
            bitmapData[bitmapDataIndex++] = (byte)(mask & 0xff);
        }

        // Note: The rest of the bitmap data just stays unchanged (0).

        bool IsSectorFree(int sectorIndex)
        {
            if (sectorIndex == halfSectorCount || sectorIndex == halfSectorCount + 1)
                return false; // root and bitmap block are always allocated

            if (sectorIndex < halfSectorCount) // first disk half sector
                return endSector > halfSectorCount || endSector <= sectorIndex;
            else // second disk half sector
                return endSector > halfSectorCount && endSector <= sectorIndex;
        }
    }

    internal void Write(ADFWriter.WriteSectors sectorWriter, int index)
    {
        sectorWriter(index, 1, writer =>
        {
            writer.WriteDword(CalculateChecksum(bitmapData));
            writer.WriteBytes(bitmapData);
        });
    }

    internal uint CalculateChecksum(byte[] bitmapData)
    {
        var reader = new DataReader(bitmapData);
        uint checksum = 0;

        for (int i = 0; i < bitmapData.Length / sizeof(uint); ++i)
            checksum += reader.ReadDword();

        return unchecked((uint)-checksum);
    }
}
