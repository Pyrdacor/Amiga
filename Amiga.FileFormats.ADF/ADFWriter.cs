namespace Amiga.FileFormats.ADF;

public enum ADFWriteResult
{
    Success,
    OmittedEmptyDirectories,
    DiskFullError,
    WriteAccessError,
    FirstError = DiskFullError
}

// Note: The reader may detect long filenames or directory cache settings but we won't support
// them when writing. To ease the data creation but also as it isn't that valuable in my opinion.
public static class ADFWriter
{
    public static readonly byte[] DefaultBootCode =
    [
        0x43, 0xFA, 0x00, 0x3E, // lea      exp(pc),a1  ; Lib name
        0x70, 0x25,             // moveq    #37,d0      ; Lib version
        0x4E, 0xAE, 0xFD, 0xD8, // jsr      -552(a6)    ; OpenLibrary()
        0x4A, 0x80,             // tst.l    d0          ; error == 0
        0x67, 0x0C,             // beq.b    error1
        0x22, 0x40,             // move.l   d0,a1       ; lib pointer
        0x08, 0xE9, 0x00, 0x06, // bset     #6,34(a1)
        0x00, 0x22,
        0x4E, 0xAE, 0xFE, 0x62, // jsr      -414(a6)    ; CloseLibrary()
        // error1:
        0x43, 0xFA, 0x00, 0x18, // lea      dos(PC),a1  ; name
        0x4E, 0xAE, 0xFF, 0xA0, // jsr      -96(a6)     ; FindResident()
        0x4A, 0x80,             // tst.l    d0
        0x67, 0x0A,             // beq.b    error2      ; not found
        0x20, 0x40,             // move.l   d0,a0
        0x20, 0x68, 0x00, 0x16, // move.l   22(a0),a0   ; DosInit sub
        0x70, 0x00,             // moveq    #0,d0
        0x4E, 0x75,             // rts
        // error2:
        0x70, 0xFF,             // moveq    #-1,d0
        0x4E, 0x75,             // rts
        // "dos.library"
        0x64, 0x6F, 0x73, 0x2E,
        0x6C, 0x69, 0x62, 0x72,
        0x61, 0x72, 0x79, 0x00,
        // "expansion.library"
        0x65, 0x78, 0x70, 0x61,
        0x6E, 0x73, 0x69, 0x6F,
        0x6E, 0x2E, 0x6C, 0x69,
        0x62, 0x72, 0x61, 0x72,
        0x79, 0x00
    ];

    internal delegate void WriteSectors(int index, int count, Action<DataWriter> writeHandler);

    public static ADFWriteResult WriteADFFile(string adfFilePath, string name, string directoryPath,
        string? fileFilter = null, bool includeEmptyDirectories = false)
    {
        return WriteADFFile(adfFilePath, name, directoryPath, includeEmptyDirectories, fileFilter, new ADFWriterConfiguration());
    }

    public static ADFWriteResult WriteADFFile(string adfFilePath, string name, string directoryPath,
        bool includeEmptyDirectories, string? fileFilter, FileSystem fileSystem,
        bool bootable, bool internationalMode, bool hd)
    {
        var configuration = new ADFWriterConfiguration
        {
            FileSystem = fileSystem,
            InternationalMode = internationalMode,
            HD = hd,
            DiskFullBehavior = DiskFullBehavior.Error,
            BootCode = bootable ? DefaultBootCode : null
        };

        return WriteADFFile(adfFilePath, name, directoryPath, includeEmptyDirectories, fileFilter, configuration);
    }

    public static ADFWriteResult WriteADFFile(string adfFilePath, string name, string directoryPath,
        bool includeEmptyDirectories, string? fileFilter, ADFWriterConfiguration configuration)
    {
        var files = Directory.GetFiles(directoryPath, fileFilter ?? "*", SearchOption.AllDirectories);
        var directories = includeEmptyDirectories
            ? Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                .Where(d => !files.Any(f => f.Contains(d, StringComparison.InvariantCultureIgnoreCase)))
                .ToArray()
            : null;

        var directory = Path.GetDirectoryName(adfFilePath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var stream = File.Create(adfFilePath);
        int rootLength = directoryPath.Length;

        if (!directoryPath.EndsWith('/') && !directoryPath.EndsWith('\\'))
            ++rootLength;

        string GetRelativePath(string path)
        {
            return path[rootLength..].Replace('\\', '/').TrimEnd('/');
        }

        return WriteADFFile(stream, name, configuration,
            files.ToDictionary(f => GetRelativePath(f), f => File.ReadAllBytes(f)),
            directories?.Select(d => GetRelativePath(d))?.ToList());
    }

    public static ADFWriteResult WriteADFFile(Stream stream, string name, FileSystem fileSystem,
        bool bootable, bool internationalMode, bool hd, Dictionary<string,
            Stream> files, List<string>? emptyDirectoryPaths = null)
    {
        var configuration = new ADFWriterConfiguration
        {
            FileSystem = fileSystem,
            InternationalMode = internationalMode,
            HD = hd,
            DiskFullBehavior = DiskFullBehavior.Error,
            BootCode = bootable ? DefaultBootCode : null
        };

        return WriteADFFile(stream, name, configuration, files, emptyDirectoryPaths);
    }

    public static ADFWriteResult WriteADFFile(Stream stream, string name,
        Dictionary<string, Stream> files, List<string>? emptyDirectoryPaths = null)
    {
        return WriteADFFile(stream, name, new ADFWriterConfiguration(), files, emptyDirectoryPaths);
    }

    public static ADFWriteResult WriteADFFile(Stream stream, string name, ADFWriterConfiguration configuration,
        Dictionary<string, Stream> files, List<string>? emptyDirectoryPaths = null)
    {
        static byte[] DataFromStream(Stream stream)
        {
            byte[] data = new byte[stream.Length - stream.Position];
            stream.Read(data, 0, data.Length);
            return data;
        }

        return WriteADFFile(stream, name, configuration,
            files.ToDictionary(d => d.Key, d => DataFromStream(d.Value)),
            emptyDirectoryPaths);
    }

    public static ADFWriteResult WriteADFFile(Stream stream, string name,
        Dictionary<string, byte[]> files, List<string>? emptyDirectoryPaths = null)
    {
        return WriteADFFile(stream, name, new ADFWriterConfiguration(), files, emptyDirectoryPaths);
    }

    public static ADFWriteResult WriteADFFile(Stream stream, string name, ADFWriterConfiguration configuration,
        Dictionary<string, byte[]> files, List<string>? emptyDirectoryPaths = null)
    {
        if (name.Contains('/') || name.Contains(':'))
            throw new ArgumentException("Volume names must not contain ':' or '/'.");

        foreach (var file in files.Keys)
        {
            if (file.Contains(':') || file.Contains('\\'))
                throw new ArgumentException($"File names must not contain ':' or '\\'. File name was: '{file}'.");
        }

        emptyDirectoryPaths ??= [];

        foreach (var dir in emptyDirectoryPaths)
        {
            if (dir.Contains(':') || dir.Contains('\\'))
                throw new ArgumentException($"Directory names must not contain ':' or '\\'. Directory name was: '{dir}'.");
        }

        byte[] data = new byte[configuration.HD ? ADF.SizeHD : ADF.SizeDD];

        void WriteSectors(int index, int count, Action<DataWriter> writeHandler)
        {
            var dataWriter = new DataWriter();
            writeHandler(dataWriter);

            if (dataWriter.Size != count * SectorDataProvider.SectorSize)
                throw new InvalidDataException($"Sector size must be {SectorDataProvider.SectorSize} bytes.");

            dataWriter.CopyTo(data, index * SectorDataProvider.SectorSize, count * SectorDataProvider.SectorSize);
        }

        BootBlock.Write(WriteSectors, configuration);
        var result = RootBlock.Write(WriteSectors, configuration, name, files, emptyDirectoryPaths!);

        if (result >= ADFWriteResult.FirstError)
            return result;

        try
        {
            stream.Write(data);
            return result;
        }
        catch
        {
            return ADFWriteResult.WriteAccessError;
        }
    }
}