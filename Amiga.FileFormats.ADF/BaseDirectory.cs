using Amiga.FileFormats.Core;

namespace Amiga.FileFormats.ADF
{
    internal abstract class BaseDirectory : IDirectory
    {
        private readonly Dictionary<string, IDirectoryEntry> entryCache = new();
        private readonly Dictionary<uint, List<IDirectoryEntry>> hashCache = new();
        private readonly SectorDataProvider sectorDataProvider;
        private readonly bool dirCache;
        private readonly bool allowInvalidChecksum = false;
        private readonly bool internationalMode = false;
        private protected uint[]? HashTable { get; init; }
        public abstract IDirectory? Parent { get; }
        internal RootBlock RootBlock { get; }
        public abstract string Name { get; }
        public abstract string Path { get; }
        public abstract DateTime? CreationDate { get; }
        public abstract DateTime? LastModificationDate { get; }
        public abstract string Comment { get; }
        internal abstract uint Sector { get; }

        private protected BaseDirectory(RootBlock rootBlock, SectorDataProvider sectorDataProvider,
            bool dirCache, bool internationalMode, bool allowInvalidChecksum)
        {
            RootBlock = rootBlock;
            this.sectorDataProvider = sectorDataProvider;
            this.dirCache = dirCache;
            this.allowInvalidChecksum = allowInvalidChecksum;
            this.internationalMode = internationalMode;
        }

        private protected BaseDirectory(SectorDataProvider sectorDataProvider,
            bool dirCache, bool internationalMode, bool allowInvalidChecksum)
        {
            RootBlock = (RootBlock)this;
            this.sectorDataProvider = sectorDataProvider;
            this.dirCache = dirCache;
            this.allowInvalidChecksum = allowInvalidChecksum;
            this.internationalMode = internationalMode;
        }

        public IEnumerable<IDirectory> GetDirectories() => GetEntries().OfType<IDirectory>();
        public IDirectory? GetDirectory(string name) => GetEntry(name) as IDirectory;
        public IFile? GetFile(string name) => GetEntry(name) as IFile;
        public IEnumerable<IFile> GetFiles() => GetEntries().OfType<IFile>();

        public IDirectoryEntry? GetEntry(string name)
        {
            if (entryCache.TryGetValue(name, out var entry))
                return entry;

            var sector = HashName(name);
            entry = GetEntries(sector).FirstOrDefault(e => e.Name.ToUpper() == name.ToUpper());

            if (entry != null)
                entryCache.Add(name, entry);

            return entry;
        }

        public IEnumerable<IDirectoryEntry> GetEntries()
        {
            foreach (var sector in HashTable!)
            {
                if (sector != 0)
                {
                    foreach (var entry in GetEntries(sector))
                        yield return entry;
                }
            }
        }

        private IEnumerable<IDirectoryEntry> GetEntries(uint firstSector)
        {
            if (hashCache.TryGetValue(firstSector, out var entries))
            {
                foreach (var entry in entries)
                    yield return entry;
            }
            else
            {
                entries = LoadEntries(sectorDataProvider, firstSector, this);

                hashCache.Add(firstSector, entries);

                foreach (var entry in entries)
                    yield return entry;
            }
        }

        internal List<IDirectoryEntry> LoadEntries(SectorDataProvider sectorDataProvider, uint firstSector, IDirectory parent)
        {
            uint sector = firstSector;
            var loadedSectors = new HashSet<uint>();
            var entries = new List<IDirectoryEntry>();

            while (true)
            {
                if (sector == 0 || loadedSectors.Contains(sector)) // avoid circular links
                    return entries;

                loadedSectors.Add(sector);

                var data = sectorDataProvider.GetSectorData((int)sector, 1);
                var reader = new DataReader(data);
                reader.Position = data.Length - 4;
                int entryType = unchecked((int)reader.ReadDword());

                if (entryType == 2) // directory
                {
                    var directoryBlock = new DirectoryBlock(sector, RootBlock, parent, data,
                        sectorDataProvider, dirCache, internationalMode, allowInvalidChecksum);
                    entries.Add(directoryBlock);
                    sector = directoryBlock.NextHashSector;
                }
                else if (entryType == -3) // file
                {
                    var fileBlock = new FileBlock(sector, RootBlock, parent, data,
                        sectorDataProvider, allowInvalidChecksum);
                    entries.Add(fileBlock);
                    sector = fileBlock.NextHashSector;
                }
                // TODO: links?
                else
                {
                    throw new InvalidDataException("Unsupported ADF entry sub type: " + entryType);
                }
            }
        }

        private protected static uint HashName(string name, bool internationalMode)
        {
            uint hash = (uint)name.Length;
            uint l = hash;

            for (int i = 0; i < l; ++i)
            {
                hash *= 13;
                hash += (uint)ToUpper(name[i], internationalMode);
                hash &= 0x7ff;
            }
            hash %= 72;

            return hash;
        }

        private uint HashName(string name) => HashName(name, internationalMode);

        private static int ToUpper(char c, bool internationalMode)
        {
            if (internationalMode)
            {
                return (c >= 'a' && c <= 'z') || (c >= 224 && c <= 254 && c != 247) ? c - ('a' - 'A') : c;
            }
            else
            {
                return (c >= 'a' && c <= 'z') ? c - ('a' - 'A') : c;
            }
        }
    }
}
