namespace Amiga.FileFormats.ADF
{
    internal class FileBlock : IFile
    {
        private readonly RootBlock rootBlock;
        internal uint NextHashSector { get; }
        public IDirectory? Parent { get; }
        public string Name { get; }
        public string Path => Parent!.Path + Name;
        public DateTime CreationDate { get; }
        public DateTime LastModificationDate { get; }
        public string Comment { get; }
        public byte[] Data { get; }
        public int Size => Data.Length;

        public FileBlock(uint sector, RootBlock rootBlock, IDirectory parent, byte[] blockData,
            SectorDataProvider sectorDataProvider, bool allowInvalidChecksum)
        {
            this.rootBlock = rootBlock;
            Parent = parent;

            var reader = new DataReader(blockData);

            if (reader.ReadDword() != 2 || // T_HEADER
                reader.ReadDword() != sector)
                throw new InvalidDataException("File block header is invalid.");

            uint numDataBlocks = reader.ReadDword();

            if (reader.ReadDword() != 0)
                throw new InvalidDataException("File block header is invalid.");

            uint firstDataBlockSector = reader.ReadDword(); // we don't use this
            uint checkSum = reader.ReadDword();

            if (!allowInvalidChecksum)
            {
                if (checkSum != RootBlock.CalculateChecksum(blockData))
                    throw new InvalidDataException("Invalid file block checksum.");
            }

            int position = reader.Position;
            reader.Position += 71 * 4; // go to the first data pointer
            var fileData = new List<byte>();
            uint nextExpectedBlock = firstDataBlockSector;

            for (int i = 0; i < Math.Min(72, numDataBlocks); ++i)
            {
                uint dataSector = reader.PeekDword();
                var data = sectorDataProvider.GetSectorData((int)dataSector, 1);

                if (rootBlock.FileSystem == FileSystem.FFS)
                    fileData.AddRange(data);
                else
                {
                    if (nextExpectedBlock != dataSector)
                        throw new InvalidDataException("Data sector links are broken.");
                    var dataBlock = new DataBlock(sector, (uint)i + 1u, data, allowInvalidChecksum);
                    fileData.AddRange(dataBlock.Data);
                    nextExpectedBlock = dataBlock.NextDataBlock;
                }

                reader.Position -= 4;
            }

            reader.Position = position + 72 * 4 + 12; // skip user, group, protection and unused bytes

            uint sizeInBytes = reader.ReadDword();

            if (sizeInBytes < fileData.Count)
                throw new InvalidDataException("The given file size is lower than the provided data size.");

            int commentLength = Math.Min(79, (int)reader.ReadByte());
            position = reader.Position;
            Comment = new string(reader.ReadChars(commentLength)).TrimEnd('\0');
            reader.Position = position + 79 + 12;

            LastModificationDate = Util.ReadDateTime(reader);

            int nameLength = Math.Min(30, (int)reader.ReadByte());
            position = reader.Position;
            Name = new string(reader.ReadChars(nameLength)).TrimEnd('\0');
            reader.Position = position + 30;

            if (reader.ReadByte() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0)
                throw new InvalidDataException("File block is invalid.");

            uint nextLink = reader.ReadDword(); // TODO: support links?

            if (reader.ReadDword() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0)
                throw new InvalidDataException("File block is invalid.");

            NextHashSector = reader.ReadDword();

            if (reader.ReadDword() != (parent as BaseDirectory)!.Sector)
                throw new InvalidDataException("File block is invalid.");

            uint extensionBlockSector = reader.ReadDword();

            if (extensionBlockSector == 0 && rootBlock.FileSystem == FileSystem.OFS && nextExpectedBlock != 0)
                throw new InvalidDataException("Data sector links are broken.");

            uint extensionStartIndex = 72;

            while (extensionBlockSector != 0)
            {
                var extensionBlock = new FileExtensionBlock(extensionBlockSector, sector, rootBlock,
                    sectorDataProvider, sectorDataProvider.GetSectorData((int)extensionBlockSector, 1),
                    allowInvalidChecksum, nextExpectedBlock, extensionStartIndex);
                extensionStartIndex += 72;
                fileData.AddRange(extensionBlock.Data);
                extensionBlockSector = extensionBlock.NextExtensionBlock;
                nextExpectedBlock = extensionBlock.NextExpectedDataBlock;
            }

            Data = fileData.ToArray();

            if (reader.ReadDword() != unchecked((uint)-3)) // ST_FILE
                throw new InvalidDataException("Invalid file block subtype.");
        }
    }
}
