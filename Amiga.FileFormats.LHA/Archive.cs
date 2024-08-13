using Amiga.FileFormats.Core;
using System.Text;
using TimestampAndComment = System.Tuple<System.DateTime, string>;

namespace Amiga.FileFormats.LHA
{
    internal class Archive : ILHA
    {
        private readonly Dictionary<string, ArchiveEntry> files = new();
        private readonly Dictionary<string, TimestampAndComment> emptyDirectories = new();

        public IDirectory RootDirectory { get; }

        public Archive(BinaryReader reader, FileInfo? fileInfo)
        {
            if ((reader.BaseStream.Length - reader.BaseStream.Position) < 21)
                throw new InvalidDataException("Not a valid LHA archive.");

            var rootCreationDate = fileInfo?.CreationTime ?? DateTime.Now;
            var rootLastModificationDate = fileInfo?.LastWriteTime ?? rootCreationDate;
            var directories = new Dictionary<string, TimestampAndComment>();

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                try
                {
                    var entry = new ArchiveEntry(reader);

                    if (!entry.Omit)
                    {
                        if (entry.IsDirectory)
                            directories.Add(entry.Path, Tuple.Create(entry.Timestamp, entry.Comment));
                        else
                            files.Add(entry.Path, entry);
                    }
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }

            foreach (var directory in directories)
            {
                string lowerName = directory.Key.ToLower();

                if (!files.Any(file => file.Key.ToLower().StartsWith(lowerName)))
                    emptyDirectories.Add(lowerName, directory.Value);
            }

            RootDirectory = new ArchiveDirectory("<root>", null, files, emptyDirectories, rootCreationDate, rootLastModificationDate);
        }

        private class ArchiveDirectory : IDirectory
        {
            private readonly Dictionary<string, ArchiveEntry> allFiles;
            private readonly Lazy<Dictionary<string, ArchiveDirectory>> directories;            
            private readonly Lazy<Dictionary<string, IDirectoryEntry>> entries;
            private readonly Lazy<Dictionary<string, ArchiveFile>> files;

            private class ArchiveFile : IFile
            {
                public ArchiveFile(IDirectory parent, string name, string path,
                    DateTime creationDate, DateTime lastChangeDate, string comment,
                    byte[] data)
                {
                    Parent = parent;
                    Name = name;
                    Path = path;
                    CreationDate = creationDate;
                    LastModificationDate = lastChangeDate;
                    Comment = comment;
                    Data = data;
                }

                public IDirectory? Parent { get; }

                public string Name { get; }

                public string Path { get; }

                public DateTime CreationDate { get; }

                public DateTime LastModificationDate { get; }

                public string Comment { get; }

                public byte[] Data { get; }

                public int Size => Data.Length;
            }

            public ArchiveDirectory(string name, IDirectory? parent, Dictionary<string, ArchiveEntry> files,
                Dictionary<string, TimestampAndComment> emptyDirectories, DateTime creationDate,
                DateTime? lastChangeDate = null)
            {
                Parent = parent;
                Name = name;
                CreationDate = creationDate;
                LastModificationDate = lastChangeDate ?? creationDate;
                allFiles = files;
                directories = new(() =>
                {
                    // Paths are case-insensitive on Amiga!
                    var directories = new Dictionary<string, ArchiveDirectory>();
                    var filesByRootDir = allFiles.Select(f =>
                    {
                        string rootDirectory = GetRootDirectory(f.Key, out var path);
                        return new
                        {
                            RootDirectory = rootDirectory,
                            Path = path,
                            Entry = (ArchiveEntry?)f.Value,
                            DirInfo = (TimestampAndComment?)null
                        };
                    }).Where(f => f.RootDirectory != "");
                    var emptyDirectoriesByRootDir = emptyDirectories.Select(d =>
                    {
                        string rootDirectory = GetRootDirectory(d.Key, out var path);
                        return new
                        {
                            RootDirectory = rootDirectory,
                            Path = path,
                            Entry = (ArchiveEntry?)null,
                            DirInfo = (TimestampAndComment?)d.Value
                        };
                    });
                    var groups = filesByRootDir.Concat(emptyDirectoriesByRootDir.Where(d => d.RootDirectory != ""))
                        .GroupBy(file => file.RootDirectory.ToLower());
                    var thisDir = emptyDirectoriesByRootDir.FirstOrDefault(d => d.RootDirectory == "" && d.Path.ToLower() == name.ToLower());

                    foreach (var group in groups)
                    {
                        var files = group.Where(e => e.Entry is not null).ToDictionary(e => e.Path, e => e.Entry!);
                        var emptyDirs = group.Where(e => e.DirInfo is not null).ToDictionary(e => e.Path, e => e.DirInfo!);
                        var creationDate = thisDir?.DirInfo?.Item1 ?? CreationDate;
                        var lastModificationDate = thisDir?.DirInfo?.Item1 ?? LastModificationDate;
                        directories.Add(group.Key, new ArchiveDirectory(group.Key, this, files, emptyDirs,
                            creationDate, lastModificationDate));
                    }

                    return directories;
                });
                this.files = new(() => allFiles.Where(f => !f.Key.Contains('/')).Select(f => new ArchiveFile
                (
                    this, f.Key, f.Value.Path, f.Value.Timestamp, f.Value.Timestamp, f.Value.Comment, f.Value.Data
                )).ToDictionary(f => f.Name, f => f));
                entries = new(() =>
                {
                    var allEntries = new Dictionary<string, IDirectoryEntry>();
                    foreach (var file in this.files.Value)
                        allEntries.Add(file.Key, file.Value);
                    foreach (var dir in directories.Value)
                        allEntries.Add(dir.Key, dir.Value);
                    return allEntries;
                });
            }

            public IDirectory? Parent { get; }

            public string Name { get; }

            public string Path => Parent is null ? "/" : Parent.Path + "/" + Name;

            public DateTime CreationDate { get; }

            public DateTime LastModificationDate { get; }

            public string Comment => "";

            private static string GetRootDirectory(string path, out string relativePath)
            {
                int pathSepIndex = path.IndexOf('/');

                if (pathSepIndex == -1)
                {
                    relativePath = path;
                    return "";
                }

                relativePath = path[(pathSepIndex + 1)..];
                return path[0..pathSepIndex];
            }

            public IEnumerable<IDirectory> GetDirectories() => directories.Value.Values;

            public IDirectory? GetDirectory(string name) =>
                directories.Value.TryGetValue(name, out var directory) ? directory : null;

            public IEnumerable<IDirectoryEntry> GetEntries() => entries.Value.Values;

            public IDirectoryEntry? GetEntry(string name) =>
                entries.Value.TryGetValue(name, out var entry) ? entry : null;

            public IFile? GetFile(string name) =>
                files.Value.TryGetValue(name, out var file) ? file : null;

            public IEnumerable<IFile> GetFiles() => files.Value.Values;
        }

        private class ArchiveEntry
        {
            public bool IsDirectory { get; }
            public Decompressor? Decompressor { get; }
            public string Path { get; }
            public bool Omit { get; } // "-lhd-" is often used to just store information, we may omit them in the file hierarchy
            public DateTime Timestamp { get; }
            public string Comment { get; }

            public ArchiveEntry(BinaryReader reader)
            {
                long position = reader.BaseStream.Position;
                var firstByte = reader.ReadByte();

                if (firstByte == 0)
                    throw new EndOfStreamException();

                if ((reader.BaseStream.Length - position) < 21)
                    throw new InvalidDataException("Not a valid LHA entry.");

                var header = new byte[20];
                header[0] = firstByte;
                reader.Read(header, 1, 19); // read the rest of the header
                int level = reader.ReadByte(); // read the level
                string method = Encoding.ASCII.GetString(header[2..7]);
                IsDirectory = method == "-lhd-";
                if (!IsDirectory && !Decompressor.SupportedCompressions.ContainsKey(method))
                    throw new NotSupportedException($"Compression method {method} is not supported.");
                string path;
                bool omit = false;
                uint compressedSize = Util.ReadLELong(header, 7);
                uint rawSize = Util.ReadLELong(header, 11);
                Timestamp = Util.ReadGenericTimestamp(header, 15);
                string comment = "";
                ushort? dataCrc;
                int osSpecifier;
                int extensionSize = 0;

                switch (level)
                {
                    case 0:
                        ParseLevel0Header(position, header, reader, out path, out dataCrc, out osSpecifier);
                        break;
                    case 1:
                        ParseLevel1Header(header, reader, out path, out dataCrc, out osSpecifier, out omit, out comment, out extensionSize);
                        break;
                    case 2:
                        ParseLevel2Header(header, reader, out path, out dataCrc, out osSpecifier, out omit, out comment, out extensionSize);
                        break;
                    default:
                        throw new InvalidDataException("Invalid LHA header level: " + level);
                }

                Path = path;
                Omit = omit;
                Comment = comment;

                if (!IsDirectory)
                {
                    Decompressor = new Decompressor
                    (
                        new BinaryReader
                        (
                            new MemoryStream(reader.ReadBytes((int)compressedSize - extensionSize)),
                            Encoding.UTF8, true
                        ),
                        Decompressor.SupportedCompressions[method], rawSize
                    );
                }
            }

            private static byte AddToHeaderCheckSum(byte checksum, params byte[] bytes)
            {
                for (int i = 0; i <  bytes.Length; ++i)
                {
                    checksum = unchecked((byte)(checksum + bytes[i]));
                }

                return checksum;
            }

            private void ParseLevel0Header(long position, byte[] header, BinaryReader reader, out string path,
                out ushort? dataCrc, out int osSpecifier)
            {
                // Note: For level 0, there can also be an OS specifier and even extensions.
                // In those cases the headerSize will be greater. Even the data CRC is optional.
                // Also note that the header size does not include the first two bytes!
                byte headerChecksum = 0;
                int headerSize = header[0];
                int checksum = header[1];
                int attribute = header[19]; // TODO: use later?
                int pathLength = reader.ReadByte();
                AddToHeaderCheckSum(headerChecksum, header);
                AddToHeaderCheckSum(headerChecksum, (byte)pathLength);
                var nameBytes = reader.ReadBytes(pathLength);
                AddToHeaderCheckSum(headerChecksum, nameBytes);
                path = Encoding.ASCII.GetString(nameBytes);
                int sizeDiff = (int)(reader.BaseStream.Position - position) - pathLength;

                if (sizeDiff != 20 && sizeDiff != 22 && sizeDiff < 23)
                    throw new InvalidDataException("Level 0 header size is invalid.");

                ushort ReadAndCrcWord()
                {
                    var bytes = reader.ReadBytes(2);
                    AddToHeaderCheckSum(headerChecksum, bytes);
                    return Util.ReadLEWord(bytes, 0);
                }

                byte ReadAndCrcByte()
                {
                    byte b = reader.ReadByte();
                    AddToHeaderCheckSum(headerChecksum, b);
                    return b;
                }

                dataCrc = sizeDiff >= 22 ? ReadAndCrcWord() : null;
                osSpecifier = sizeDiff >= 23 ? ReadAndCrcByte() : 0;

                if (sizeDiff > 23)
                {
                    var remainingHeaderBytes = reader.ReadBytes(sizeDiff - 24);
                    AddToHeaderCheckSum(headerChecksum, remainingHeaderBytes);
                }

                if (headerChecksum != checksum)
                    throw new InvalidDataException("File header checksum is wrong.");
            }

            private void ParseLevel1Header(byte[] header, BinaryReader reader, out string path, out ushort? dataCrc,
                out int osSpecifier, out bool omit, out string comment, out int extensionSize)
            {
                // Note that the header size does not include the first two bytes!
                byte headerChecksum = 0;
                int headerSize = header[0];
                int checksum = header[1];
                int attribute = header[19]; // TODO: use later?
                int pathLength = reader.ReadByte();
                AddToHeaderCheckSum(headerChecksum, header);
                AddToHeaderCheckSum(headerChecksum, (byte)pathLength);
                var nameBytes = reader.ReadBytes(pathLength);
                AddToHeaderCheckSum(headerChecksum, nameBytes);
                path = Encoding.ASCII.GetString(nameBytes);

                ushort ReadAndCrcWord()
                {
                    var bytes = reader.ReadBytes(2);
                    AddToHeaderCheckSum(headerChecksum, bytes);
                    return Util.ReadLEWord(bytes, 0);
                }

                byte ReadAndCrcByte()
                {
                    byte b = reader.ReadByte();
                    AddToHeaderCheckSum(headerChecksum, b);
                    return b;
                }

                dataCrc = sizeDiff >= 22 ? ReadAndCrcWord() : null;
                osSpecifier = sizeDiff >= 23 ? ReadAndCrcByte() : 0;
                extensionSize = 0;
                string directory = "";
                string filename = "";

                if (sizeDiff > 23)
                {
                    int nextExtensionSize = ReadAndCrcWord();

                    while (nextExtensionSize != 0)
                    {
                        extensionSize += nextExtensionSize;
                        nextExtensionSize = ReadExtension(reader, nextExtensionSize, out int type, out var value, out var crc);
                    }
                }

                if (headerChecksum != checksum)
                    throw new InvalidDataException("File header checksum is wrong.");
            }

            private void ParseLevel2Header(byte[] header, BinaryReader reader, out string path, out ushort? dataCrc,
                out int osSpecifier, out bool omit, out string comment, out int extensionSize)
            {

            }

            private int ReadExtension(BinaryReader reader, int size, out int type, out string? value, out ushort? crc)
            {
                type = reader.ReadByte();
                value = null;
                crc = null;
                var data = reader.ReadBytes(size - 3);
                size = Util.ReadLEWord(reader);

                switch (type)
                {
                    case 0: // CRC
                        crc = Util.ReadLEWord(data, 0);
                        // ignore the info byte for now
                        break;
                    case 1: // filename
                        value = Encoding.UTF8.GetString(data);
                        break;
                    case 2: // directory
                        value = Encoding.UTF8.GetString(data.Select(b => b == 0xff ? (byte)'/' : b).ToArray());
                        break;
                    case 3: // comment
                        value = Encoding.UTF8.GetString(data);
                        break;
                    default:
                        // ignore the rest
                        break;
                }

                return size;
            }
        }
    }
}
