using System.IO;
using System.Text;

namespace Amiga.FileFormats.LHA
{
    public enum LHAWriteResult
    {
        Success,
        OmittedEmptyDirectories,
        DiskFullError,
        WriteAccessError,
        FirstError = DiskFullError
    }

    public static class LHAWriter
    {
        public static LHAWriteResult WriteLHAFile(string lhaFilePath, string directoryPath,
            bool includeEmptyDirectories, string? fileFilter)
        {
            var files = Directory.GetFiles(directoryPath, fileFilter ?? "*", SearchOption.AllDirectories);
            var directories = includeEmptyDirectories
                ? Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                    .Where(d => !files.Any(f => f.Contains(d, StringComparison.InvariantCultureIgnoreCase)))
                    .ToArray()
                : null;
            var lha = Archive.CreateEmpty();

            int rootLength = directoryPath.Length;

            if (!directoryPath.EndsWith('/') && !directoryPath.EndsWith("\\"))
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

            using var stream = File.Create(lhaFilePath);
            lha.Write(stream, hasEmptyDirectories);
        }
    }
}