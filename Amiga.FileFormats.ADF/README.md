# Amiga ADF File Format

This package allows reading and writing ADF disk images.


# Requirements

This is all written in pure C#, so there are no dependencies beside .NET.


# Virtual File System

The `IADF` interface implements a virtual file system interface `IVirtualFileSystem` which allows
access to files (`IFile`) and directories (`IDirectory`) in a tree hierarchy.


# Usage

## Reading a disk image.

```cs
var disk = ADFReader.LoadADFFile("myDiskImage.adf");

var rootDir = disk.RootDirectory;
var myFile = rootDir.GetFile("internalFile.txt");

Console.WriteLine($"File Size: {myFile.Size}");

var myFileData = myFile.Data;
```

## Writing a disk image from a directory.

```cs
string name = "My Image";
bool includeEmptyDirectories = false;
bool bootable = false;
bool internationalMode = false;
bool hd = false;

var result = ADFWriter.WriteADFFile("myNewDiskImage.adf", name, "mySourceDir",
    includeEmptyDirectories, "*.*", FileSystem.OFS, bootable, internationalMode, hd);

if (result == ADFWriteResult.Success)
{
    Console.WriteLine("It worked!");
}
```
