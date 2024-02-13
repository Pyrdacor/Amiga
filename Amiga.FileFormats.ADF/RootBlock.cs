using Amiga.FileFormats.Core;
using System.Data;

namespace Amiga.FileFormats.ADF
{
    internal class RootBlock : BaseDirectory
    {
        private class DirectoryEntry
        {
            public DirectoryEntry(string path, DirectoryEntry? parent)
            {
                Path = path;
                Parent = parent;
            }

            public string Path { get; }
            public DirectoryEntry? Parent { get; }
            public Dictionary<string, int> Files { get; } = new();
            public List<string> Directories { get; } = new();
        }

        public const int Size = SectorDataProvider.SectorSize;
        public override DateTime LastModificationDate { get; }
        public DateTime DiskLastModificationDate { get; }
        public override DateTime CreationDate { get; }
        public override IDirectory? Parent => null;
        public override string Name { get; } = "<root>";
        public override string Path => "/";
        public override string Comment => "";
        internal override uint Sector { get; }
        public FileSystem FileSystem { get; }

        public RootBlock(SectorDataProvider sectorDataProvider, bool hd, FileSystem fileSystem,
            bool dirCache, bool internationalMode, bool allowInvalidChecksum)
            : base(sectorDataProvider, dirCache, internationalMode, allowInvalidChecksum)
        {
            // The rootblock is always located in the center of the disk (sector 880 or 1760).
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
            // TODO: later check if some files/dirs are marked as free (= deleted)?

            LastModificationDate = Util.ReadDateTime(reader);

            int volumeNameLength = Math.Min(30, (int)reader.ReadByte());
            int position = reader.Position;
            Name = new string(reader.ReadChars(volumeNameLength)).TrimEnd('\0');
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

        public static ADFWriteResult Write(ADFWriter.WriteSectors sectorWriter, ADFWriterConfiguration configuration,
            string name, Dictionary<string, byte[]> files, List<string> emptyDirectoryPaths)
        {
            int sectorCount = configuration.HD ? 3520 : 1760;
            int rootBlockSector = configuration.HD ? 1760 : 880;
            var now = DateTime.Now;

            // Create all directory and file blocks.
            // Start at sector 882/1762 and go up to the end.
            // Then start at sector 2.
            // Omit the empty directories if the files would not fit otherwise.
            // Always place the bitmap sector at 881/1761.
            // Order the files' sectors by file size so that large files have
            // higher chances of use consecutive sectors.
            var createdDirectories = new Dictionary<string, DirectoryEntry>();
            var rootFiles = new Dictionary<string, int>();
            var allFiles = new Dictionary<string, int>();
            var allFileParents = new Dictionary<string, DirectoryEntry?>();
            var fileSectors = new Dictionary<string, List<int>>();
            var directorySectors = new Dictionary<string, int>();
            var filesBySector = new Dictionary<int, string>();

            foreach (var file in files)
            {
                var parts = file.Key.Split('/');

                if (parts.Length > 1)
                {
                    string path = parts[0];
                    DirectoryEntry? parent = null;

                    for (int i = 0; i < parts.Length - 1; ++i)
                    {
                        if (i != 0)
                            path += '/' + parts[i];

                        if (createdDirectories.TryGetValue(path, out var dir))
                            parent = dir;
                        else
                        {
                            if (parent is not null)
                                parent.Directories.Add(parts[i]);
                            parent = new DirectoryEntry(path, parent);
                            createdDirectories.Add(path, parent);
                        }
                    }

                    parent!.Files.Add(parts[^1], file.Value.Length);
                    allFiles.Add(path + '/' + parts[^1], file.Value.Length);
                    allFileParents.Add(path + '/' + parts[^1], parent);
                }
                else
                {
                    rootFiles.Add(parts[0], file.Value.Length);
                    allFiles.Add(parts[0], file.Value.Length);
                    allFileParents.Add(parts[0], null);
                }
            }

            var sortedFiles = allFiles.ToList();
            sortedFiles.Sort((a, b) => b.Value.CompareTo(a.Value)); // largest first
            int entrySector = rootBlockSector + 2;

            int GetSectorCountFromFileSize(int size)
            {
                // We always need 1 file header block.
                // Then up to 72 data blocks (size depends on filesystem).
                // Then for every 72 additional data blocks we need 1 extension block.
                int sizePerDataBlock = configuration.FileSystem == FileSystem.OFS
                    ? SectorDataProvider.SectorSize - 24
                    : SectorDataProvider.SectorSize;
                int numDataBlocks = (size + sizePerDataBlock - 1) / sizePerDataBlock;
                return Math.Max(1, (numDataBlocks + 71) / 72) + numDataBlocks;
            }

            int NextSector()
            {
                var sector = entrySector++;

                if (entrySector == sectorCount)
                    entrySector = 2;
                else if (entrySector == rootBlockSector)
                    throw new OutOfMemoryException(); // not enough sectors

                return sector;
            }

            void EnsureDirectorySector(DirectoryEntry dir)
            {
                if (dir.Parent is not null)
                    EnsureDirectorySector(dir.Parent);

                if (!directorySectors.ContainsKey(dir.Path))
                    directorySectors.Add(dir.Path, NextSector());
            }

            void EnsureDirectorySectors(string file)
            {
                var parent = allFileParents[file];

                if (parent is not null)
                    EnsureDirectorySector(parent);
            }

            try
            {
                foreach (var file in sortedFiles)
                {
                    EnsureDirectorySectors(file.Key);

                    int numSectors = GetSectorCountFromFileSize(file.Value);
                    var sectors = new List<int>();

                    for (int i = 0; i < numSectors; ++i)
                    {
                        sectors.Add(NextSector());
                    }

                    fileSectors.Add(file.Key, sectors);
                    filesBySector.Add(sectors[0], file.Key);
                }
            }
            catch (OutOfMemoryException)
            {
                return ADFWriteResult.DiskFullError; // not enough sectors
            }

            var result = ADFWriteResult.Success;
            var createdDirectoriesBackup = new Dictionary<string, DirectoryEntry>(createdDirectories);
            var directorySectorBackup = new Dictionary<string, int>(directorySectors);

            try
            {
                foreach (var emptyDir in emptyDirectoryPaths)
                {
                    var parts = emptyDir.Split('/');

                    if (parts.Length > 1)
                    {
                        string path = parts[0];
                        DirectoryEntry? parent = null;

                        for (int i = 0; i < parts.Length - 1; ++i)
                        {
                            if (createdDirectories.TryGetValue(path, out var dir))
                                parent = dir;
                            else
                            {
                                if (parent is not null)
                                    parent.Directories.Add(parts[i]);
                                parent = new DirectoryEntry(path, parent);
                                createdDirectories.Add(path, parent);
                            }

                            path += '/' + parts[i];
                        }

                        parent!.Directories.Add(parts[^1]);
                        path += '/' + parts[^1];
                        parent = new DirectoryEntry(path, parent);
                        createdDirectories.Add(path, parent);
                        EnsureDirectorySector(parent);
                    }
                    else
                    {
                        var dir = new DirectoryEntry(parts[0], null);
                        createdDirectories.Add(parts[0], dir);
                        EnsureDirectorySector(dir);
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                if (configuration.DiskFullBehavior.HasFlag(DiskFullBehavior.OmitEmptyDirectories))
                {
                    createdDirectories = createdDirectoriesBackup;
                    directorySectors = directorySectorBackup;
                    result = ADFWriteResult.OmittedEmptyDirectories;
                }
                else
                {
                    return ADFWriteResult.DiskFullError;
                }
            }

            int SectorRank(int sector) => sector >= rootBlockSector ? sector - rootBlockSector : sector + rootBlockSector;

            var directoriesBySector = new Dictionary<int, string>();
            foreach (var dirSector in directorySectors)
                directoriesBySector.Add(dirSector.Value, dirSector.Key);
            var rootDirectories = createdDirectories.Where(d => d.Value.Parent is null).ToList();
            var rootHashedEntries = rootDirectories.Select(dir => new { name = dir.Key, sector = directorySectors[dir.Key] })
                .Concat(rootFiles.Keys.Select(file => new { name = file, sector = fileSectors[file][0] }))
                .Select(entry => new { entry.sector, hash = HashName(entry.name, configuration.InternationalMode) })
                .GroupBy(entry => entry.hash)
                .ToDictionary(entry => entry.Key, entry => entry.Select(e => e.sector).OrderBy(s => SectorRank(s)).ToList());

            void WriteFilesAndDirectories(Dictionary<uint, List<int>> hashedEntries, int parentSector)
            {
                foreach (var hashedEntry in hashedEntries)
                {
                    for (int i = 0; i < hashedEntry.Value.Count; ++i)
                    {
                        int sector = hashedEntry.Value[i];
                        int nextSector = i == hashedEntry.Value.Count - 1 ? 0 : hashedEntry.Value[i + 1];

                        if (directoriesBySector.TryGetValue(sector, out var directoryPath))
                        {
                            var directory = createdDirectories[directoryPath];
                            // Key: Hash, Value: List of sectors (directory blocks and first file blocks)
                            var dirHashedEntries = directory.Directories.Select(dir => new { name = dir, sector = directorySectors[directory.Path + '/' + dir] })
                                .Concat(directory.Files.Keys.Select(file => new { name = file, sector = fileSectors[directory.Path + '/' + file][0] }))
                                .Select(entry => new { entry.sector, hash = HashName(entry.name, configuration.InternationalMode) })
                                .GroupBy(entry => entry.hash)
                                .ToDictionary(entry => entry.Key, entry => entry.Select(e => e.sector).OrderBy(s => SectorRank(s)).ToList());

                            sectorWriter(sector, 1,
                                writer => DirectoryBlock.Write(writer, System.IO.Path.GetFileName(directoryPath), sector, new SortedList<uint, int>(dirHashedEntries.ToDictionary(e => e.Key, e => e.Value[0])), (uint)nextSector, (uint)parentSector, now));

                            WriteFilesAndDirectories(dirHashedEntries, sector);
                        }
                        else if (filesBySector.TryGetValue(sector, out var filePath))
                        {
                            var sectors = fileSectors[filePath];
                            var dataSectors = sectors.Skip(1).Take(72).ToList();
                            var data = files[filePath];
                            int extensionSector = sectors.Count > 73 ? sectors[73] : 0;
                            int dataIndex = 0;
                            int sectorListOffset = 74;

                            // Write header block
                            sectorWriter(sector, 1, writer => FileBlock.Write(writer, System.IO.Path.GetFileName(filePath), sectors[0], dataSectors, extensionSector, data.Length, (uint)nextSector, (uint)parentSector, now));

                            // Write data blocks and extension block
                            int dataBlockStartIndex = 1;
                            while (true)
                            {
                                int endDataSector = extensionSector == 0 ? 0 : extensionSector + 1;

                                for (int d = 0; d < dataSectors.Count; ++d)
                                {
                                    int nextDataSector = d == dataSectors.Count - 1 ? endDataSector : dataSectors[d + 1];
                                    sectorWriter(dataSectors[d], 1, writer => DataBlock.Write(writer, sectors[0], dataBlockStartIndex + d, nextDataSector, data, ref dataIndex, configuration.FileSystem));
                                }

                                if (extensionSector == 0)
                                    break;

                                dataBlockStartIndex += 72;
                                dataSectors = sectors.Skip(sectorListOffset).Take(72).ToList();
                                int nextExtensionSector = sectors.Count > sectorListOffset + 72 ? sectors[sectorListOffset + 72] : 0;
                                //int nextDataSector = sectors.Count > sectorListOffset + 72 ? sectors[sectorListOffset + 72] : 0;

                                // Write the extension block
                                sectorWriter(extensionSector, 1, writer => FileExtensionBlock.Write(writer, sectors[0], extensionSector, dataSectors, nextExtensionSector));

                                extensionSector = nextExtensionSector;
                                sectorListOffset += 73;
                            }
                        }
                    }
                }
            }

            // Write all directory and file blocks
            WriteFilesAndDirectories(rootHashedEntries, rootBlockSector);

            // Write the root block
            sectorWriter(rootBlockSector, 1, writer =>
            {
                writer.WriteDword(2); // T_HEADER
                writer.WriteDword(0);
                writer.WriteDword(0);
                writer.WriteDword(72);
                writer.WriteDword(0);
                int checksumPosition = writer.Position;
                writer.WriteDword(0); // checksum placeholder

                // Write the root hash table
                for (uint i = 0; i < 72; ++i)
                {
                    writer.WriteDword(rootHashedEntries.TryGetValue(i, out var sectors) ? (uint)sectors[0] : 0);
                }

                writer.WriteDword(0xffffffffu); // -1: valid bitmaps
                writer.WriteDword((uint)(rootBlockSector + 1)); // bitmap is stored in next sector
                // no more bitmap sectors are needed for floppies!
                // clear the other 24 sector fields and also the bitmap extension field
                for (int i = 0; i < 25; ++i)
                    writer.WriteDword(0);

                Util.WriteDateTime(writer, now); // last root dir modification time (TODO: maybe use last mod of any containing dir/file?)
                if (name.Length > 30)
                    name = name[0..30];
                writer.WriteByte((byte)name.Length);
                writer.WriteChars(name.PadRight(30, '\0').ToCharArray());
                writer.WriteByte(0);
                writer.WriteDword(0);
                writer.WriteDword(0);
                Util.WriteDateTime(writer, now); // last disk modification time
                Util.WriteDateTime(writer, now); // creation time
                writer.WriteDword(0);
                writer.WriteDword(0);
                writer.WriteDword(0); // we don't support dir cache
                writer.WriteDword(1); // ST_ROOT

                uint checksum = CalculateChecksum(writer.ToArray());
                writer.Position = checksumPosition;
                writer.WriteDword(checksum);
                writer.Position = Size;
            });

            // Write the bitmap data block
            sectorWriter(rootBlockSector + 1, 1, writer =>
            {
                writer.WriteDword(0); // checksum placeholder

                int numLongs = configuration.HD ? 110 : 55;
                int firstHalfEndSector = entrySector <= rootBlockSector ? entrySector : rootBlockSector;
                int secondHalfEndSector = entrySector <= rootBlockSector ? sectorCount : entrySector;
                bool firstHalfFilled = entrySector <= rootBlockSector;
                int fullLongs = (firstHalfEndSector - 2) / 32;
                int rootLong = (rootBlockSector - 2) / 32; // the long which contains the root block bit (starts the second half)
                int sectorEnd = firstHalfEndSector;
                bool filled = firstHalfFilled;
                int partialBits = (sectorEnd - 2) % 32;
                bool switchHalves = rootLong == fullLongs && sectorEnd == firstHalfEndSector;

                while (writer.Position < (numLongs + 1) * 4) // + 1 for the checksum long
                {
                    uint value = filled ? 0 : 0xffffffff;

                    for (int i = 0; i < fullLongs; ++i)
                        writer.WriteDword(value);

                    if (partialBits != 0 || switchHalves)
                    {
                        uint bitmap = 0xffffffff;

                        if (filled)
                        {
                            for (int i = 0; i < partialBits; ++i)
                                bitmap &= ~(1u << i);
                        }

                        if (switchHalves)
                        {
                            // switch to second half
                            filled = true; // the second half is always filled at the beginning
                            sectorEnd = secondHalfEndSector;

                            int i = (rootBlockSector - 2) % 32;
                            int maxSectors = secondHalfEndSector - rootBlockSector;
                            for (int j = 0; j < maxSectors && i < 32; ++j, ++i)
                            {
                                bitmap &= ~(1u << i);
                            }

                            filled = i + maxSectors > 32;
                            switchHalves = false;
                        }
                        else
                        {
                            // If we didn't switch halves, we just found a partial long.
                            // This means the following sectors are all free. So we can
                            // just set the sectorEnd to the rootBlockSector (for first
                            // half) or to sectorCount (for second half). Also reset the
                            // filled flag to false.
                            filled = false;
                            sectorEnd = sectorEnd < rootBlockSector ? rootBlockSector : sectorCount;
                            switchHalves = sectorEnd == rootBlockSector;
                        }

                        writer.WriteDword(bitmap);
                    }
                    else
                    {
                        // If we are here, no partial long was present and we also didn't
                        // arrive at the second half. This means we either are at the end
                        // of the stream or we finished exactly at a 32 bit boundary with
                        // a filled state. In the latter case we have to invert the filled
                        // state.
                        if (writer.Position == (numLongs + 1) * 4)
                            break;
                        if (!filled)
                            throw new InvalidOperationException("Invalid bitmap state.");
                        filled = false;
                        sectorEnd = sectorEnd < rootBlockSector ? rootBlockSector : sectorCount;
                        switchHalves = sectorEnd == rootBlockSector;
                    }

                    int currentSectorOffset = (writer.Position - 4) * 8;
                    fullLongs = (sectorEnd - currentSectorOffset - 2) / 32;
                    partialBits = (sectorEnd - currentSectorOffset - 2) % 32;
                }

                // Fill the rest of the sector with zeros
                writer.WriteBytes(Enumerable.Repeat((byte)0, 508 - numLongs * 4).ToArray());

                uint checksum = CalculateChecksum(writer.ToArray());
                writer.Position = 0;
                writer.WriteDword(checksum);
                writer.Position = Size;
            });

            return result;
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
