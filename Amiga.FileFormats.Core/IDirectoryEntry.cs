namespace Amiga.FileFormats.Core;

public interface IDirectoryEntry
{
    IDirectory? Parent { get; }
    string Name { get; }
    string Path { get; }
    DateTime? CreationDate { get; }
    DateTime? LastModificationDate { get; }
    string Comment { get; }
}
