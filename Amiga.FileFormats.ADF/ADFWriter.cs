namespace Amiga.FileFormats.ADF
{
    public enum ADFWriteResult
    {
        Success,
        OmittedEmptyDirectories,
        DiskFullError,
        WriteAccessError,
        FirstError = DiskFullError
    }

    // Note: The reader may detect long filenames or directory cache settings but we won't support
    // them when writing. To ease the data creation but also as it isn't the valuable in my opinion.
    public static class ADFWriter
    {
        internal delegate void WriteSectors(int index, int count, Action<DataWriter> writeHandler);

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

            emptyDirectoryPaths ??= new();

            foreach (var dir in emptyDirectoryPaths)
            {
                if (dir.Contains(':') || file.Contains('\\'))
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

            // TODO: write
            // TODO: if no write error occurs, return result, otherwise the write error

            return result;
        }
    }
}