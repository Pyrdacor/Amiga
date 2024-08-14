# Amiga LHA File Format

This package allows reading and writing LHA archives.

Reading supports the following compression methods:

- -lh0- (uncompressed)
- -lh5-
- -lh6-
- -lh7-
- -lz5-
- -lhd- (directory)

Writing supports all of the above except for -lz5-.


# Requirements

This is all written in pure C#, so there are no dependencies beside .NET.


# Virtual File System

The `ILHA` interface implements a virtual file system interface `IVirtualFileSystem` which allows
access to files (`IFile`) and directories (`IDirectory`) in a tree hierarchy.


# Usage

## Reading an archive.

```cs
var archive = LHAReader.LoadLHAFile("myArchive.lha");

var rootDir = archive.RootDirectory;
var myFile = rootDir.GetFile("internalFile.txt");

Console.WriteLine($"File Size: {myFile.Size}");

var myFileData = myFile.Data;
```

## Writing an archive.

```cs
var result = LHAWriter.WriteLHAFile("myNewArchive.lha", archive, CompressionMethod.LH5);

if (result == LHAWriteResult.Success)
{
    Console.WriteLine("It worked!");
}
```

## Packing a directory of text files.

```cs
var result = LHAWriter.WriteLHAFile("myNewArchive.lha", "dirToMyFiles", "*.txt", CompressionMethod.LH5);

if (result == LHAWriteResult.Success)
{
    Console.WriteLine("It worked!");
}
```
