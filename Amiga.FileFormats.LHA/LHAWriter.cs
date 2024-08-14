namespace Amiga.FileFormats.LHA
{
    public enum LHAWriteResult
    {
		OmittedEmptyDirectories = -1, // warning
		Success, // ok
        DiskFullError, // errors ...
        WriteAccessError,
        UnsupportedCompressionMethod,
		InvalidLHAObject,
        FirstError = DiskFullError
    }

    public static class LHAWriter
    {
		private static LHAWriteResult WriteLHAFile(Stream stream, ILHA lha,
			CompressionMethod compressionMethod, bool includeEmptyDirectories, DriveInfo? driveInfo)
		{
			if (lha is not Archive archive)
				return LHAWriteResult.InvalidLHAObject;

			var result = archive.Write(stream, includeEmptyDirectories, compressionMethod, driveInfo);

			if (result == LHAWriteResult.Success && !includeEmptyDirectories && archive.GetAllEmptyDirectories().Length != 0)
				return LHAWriteResult.OmittedEmptyDirectories;

			return result;
		}

		public static LHAWriteResult WriteLHAFile(Stream stream, ILHA lha,
			CompressionMethod compressionMethod = CompressionMethod.LH5,
			bool includeEmptyDirectories = false)
		{
			return WriteLHAFile(stream, lha, compressionMethod, includeEmptyDirectories, null);
		}

		public static LHAWriteResult WriteLHAFile(string lhaFilePath, ILHA lha,
			CompressionMethod compressionMethod = CompressionMethod.LH5,
			bool includeEmptyDirectories = false)
		{
			using var stream = File.Create(lhaFilePath);

			return WriteLHAFile(stream, lha, compressionMethod, includeEmptyDirectories);
		}

		private static LHAWriteResult WriteLHAFile(Stream stream, string directoryPath,
			string? fileFilter, CompressionMethod compressionMethod,
			bool includeEmptyDirectories, DriveInfo? driveInfo)
        {
			if (!Compressor.SupportedCompressions.ContainsKey(compressionMethod))
				return LHAWriteResult.UnsupportedCompressionMethod;

			var files = Directory.GetFiles(directoryPath, fileFilter ?? "*", SearchOption.AllDirectories);
			var directories = includeEmptyDirectories
				? Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
					.Where(d => !files.Any(f => f.Contains(d, StringComparison.InvariantCultureIgnoreCase)))
					.ToArray()
				: null;
			var lha = Archive.CreateEmpty();

			int rootLength = directoryPath.Length;

			if (!directoryPath.EndsWith('/') && !directoryPath.EndsWith('\\'))
				++rootLength;

			string AsRelativePath(string path) => path[rootLength..];
			bool hasEmptyDirectories = directories != null;

			if (hasEmptyDirectories)
			{
				foreach (var directory in directories!)
				{
					lha.AddEmptyDirectory(AsRelativePath(directory), new DirectoryInfo(directory).CreationTime);
				}
			}

			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
				lha.AddFile(AsRelativePath(file), File.ReadAllBytes(file), fileInfo.CreationTime, fileInfo.LastWriteTime);
			}

			var result = lha.Write(stream, hasEmptyDirectories, compressionMethod, driveInfo);

			if (result == LHAWriteResult.Success && hasEmptyDirectories && !includeEmptyDirectories)
				return LHAWriteResult.OmittedEmptyDirectories;

			return result;
		}

		public static LHAWriteResult WriteLHAFile(Stream stream, string directoryPath,
			string? fileFilter = null, CompressionMethod compressionMethod = CompressionMethod.LH5,
			bool includeEmptyDirectories = false)
		{
			return WriteLHAFile(stream, directoryPath, fileFilter, compressionMethod, includeEmptyDirectories);
		}

		public static LHAWriteResult WriteLHAFile(string lhaFilePath, string directoryPath,
            string? fileFilter = null, CompressionMethod compressionMethod = CompressionMethod.LH5,
			bool includeEmptyDirectories = false)
        {
			using var stream = File.Create(lhaFilePath);

			return WriteLHAFile(stream, directoryPath, fileFilter, compressionMethod, includeEmptyDirectories, new DriveInfo(lhaFilePath));
		}
    }
}