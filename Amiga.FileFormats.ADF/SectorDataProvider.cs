﻿namespace Amiga.FileFormats.ADF;

internal class SectorDataProvider
{
    public const int SectorSize = 512;
    private readonly byte[] diskData;

    public SectorDataProvider(byte[] diskData)
    {
        this.diskData = diskData;
    }

    public byte[] GetSectorData(int index, int count)
    {
        byte[] data = new byte[count * SectorSize];
        Buffer.BlockCopy(diskData, index * SectorSize, data, 0, data.Length);
        return data;
    }

    public byte[] GetSectorData(IEnumerable<int> sectorIndices)
    {
        var sectorIndexList = sectorIndices.ToList();
			byte[] data = new byte[sectorIndexList.Count * SectorSize];
        int i = 0;

        foreach (var blockIndex in sectorIndexList)
        {
            Buffer.BlockCopy(diskData, blockIndex * SectorSize, data, i++ * SectorSize, SectorSize);
        }

        return data;
    }
}
