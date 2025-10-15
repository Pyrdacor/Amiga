using Amiga.FileFormats.Core;

namespace Amiga.FileFormats.LHA;

public interface ILHA : IVirtualFileSystem
{
    IFile[] GetAllFiles();
    IDirectory[] GetAllEmptyDirectories();

    public static ILHA FromFileDictionary(Dictionary<string, byte[]> files)
    {
        var lha = Archive.CreateEmpty();

        foreach (var file in files)
        {
            lha.AddFile(file.Key, file.Value);
        }

        return lha;
    }
}
