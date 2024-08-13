using Amiga.FileFormats.Core;

namespace Amiga.FileFormats.ADF
{
    internal class DirectoryBlock : BaseDirectory
    {
        private readonly RootBlock rootBlock;
        public const int Size = SectorDataProvider.SectorSize;
        public override DateTime? LastModificationDate { get; }
        public override DateTime? CreationDate => rootBlock.CreationDate;
        public override IDirectory? Parent { get; }
        public override string Name { get; }
        public override string Path => Parent!.Path + Name + "/";
        public override string Comment { get; }
        internal uint NextHashSector { get; }
        internal override uint Sector { get; }

        public DirectoryBlock(uint sector, RootBlock rootBlock, IDirectory parent, byte[] blockData,
            SectorDataProvider sectorDataProvider, bool dirCache, bool internationalMode, bool allowInvalidChecksum)
            : base(rootBlock, sectorDataProvider, dirCache, internationalMode, allowInvalidChecksum)
        {
            this.rootBlock = rootBlock;
            Parent = parent;
            Sector = sector;

            var reader = new DataReader(blockData);

            if (reader.ReadDword() != 2 || // T_HEADER
                reader.ReadDword() != sector ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0) 
                throw new InvalidDataException("Directory block header is invalid.");

            uint checkSum = reader.ReadDword();

            if (!allowInvalidChecksum)
            {
                if (checkSum != RootBlock.CalculateChecksum(blockData))
                    throw new InvalidDataException("Invalid directory block checksum.");
            }

            // Read the hash table (72 longs)
            HashTable = Enumerable.Range(0, 72).Select(_ => reader.ReadDword()).ToArray();

            reader.Position += 16; // skip user, group, protection and unused bytes

            int commentLength = Math.Min(79, (int)reader.ReadByte());
            int position = reader.Position;
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
                throw new InvalidDataException("Directory block is invalid.");

            uint nextLink = reader.ReadDword(); // TODO: support links?

            if (reader.ReadDword() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0 ||
                reader.ReadDword() != 0)
                throw new InvalidDataException("Directory block is invalid.");

            NextHashSector = reader.ReadDword();

            if (reader.ReadDword() != (parent as BaseDirectory)!.Sector)
                throw new InvalidDataException("Directory block is invalid.");

            uint firstDirCacheBlockPointer = reader.ReadDword(); // TODO: support this?

            if ((firstDirCacheBlockPointer != 0) != dirCache)
                throw new InvalidDataException("Dir cache settings mismatches the existence of a dir cache pointer.");

            if (reader.ReadDword() != 2) // ST_USERDIR
                throw new InvalidDataException("Invalid directory block subtype.");
        }

        public static void Write(DataWriter writer, string name, int sector,
            SortedList<uint, int> hashTable, uint nextHashSector, uint parentSector,
            DateTime dateTime)
        {
            writer.WriteDword(2); // T_HEADER
            writer.WriteDword((uint)sector);
            writer.WriteDword(0);
            writer.WriteDword(0);
            writer.WriteDword(0);
            int checksumPosition = writer.Position;
            writer.WriteDword(0); // checksum placeholder

            // Write the hash table
            for (uint i = 0; i < 72; ++i)
            {
                writer.WriteDword(hashTable.TryGetValue(i, out var hashSector) ? (uint)hashSector : 0);
            }

            writer.WriteDword(0);
            writer.WriteDword(0);
            writer.WriteDword(0);
            writer.WriteDword(0);

            // TODO: for now we don't support comments but we add the code to make it easier later
            string comment = "";
            comment = comment.Length > 79 ? comment[..79] : comment;
            writer.WriteByte((byte)comment.Length);
            writer.WriteChars(comment.PadRight(79, '\0').ToCharArray());

            writer.WriteDword(0);
            writer.WriteDword(0);
            writer.WriteDword(0);

            Util.WriteDateTime(writer, dateTime); // last access date

            name = name.Length > 30 ? name[..30] : name;
            writer.WriteByte((byte)name.Length);
            writer.WriteChars(name.PadRight(30, '\0').ToCharArray());
            writer.WriteByte(0);
            writer.WriteDword(0);
            writer.WriteDword(0);
            writer.WriteDword(0); // we don't support creating links
            writer.WriteDword(0);
            writer.WriteDword(0);
            writer.WriteDword(0);
            writer.WriteDword(0);
            writer.WriteDword(0);

            writer.WriteDword(nextHashSector);
            writer.WriteDword(parentSector);
            writer.WriteDword(0); // we don't support directory caching
            writer.WriteDword(2); // ST_USERDIR

            uint checksum = RootBlock.CalculateChecksum(writer.ToArray());
            writer.Position = checksumPosition;
            writer.WriteDword(checksum);
            writer.Position = Size;
        }
    }
}
