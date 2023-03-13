namespace Amiga.FileFormats.ADF
{
    public interface IDirectory : IDirectoryEntry
    {        
        IDirectoryEntry? GetEntry(string name);
        IFile? GetFile(string name);
        IDirectory? GetDirectory(string name);
        IEnumerable<IDirectoryEntry> GetEntries();
        IEnumerable<IFile> GetFiles();
        IEnumerable<IDirectory> GetDirectories();
    }
}
