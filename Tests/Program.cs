#define READ

using Amiga.FileFormats.ADF;
using System;
using System.Text;

static class Program
{
    static void Main(string[] args)
    {
#if READ || EXTRACT
        string filename = "C:\\Projects\\Ambermoon\\Disks\\German\\Foo\\AMBER_FOO.adf";
        var adf = ADFReader.LoadADFFile(filename, false);
#else
        string filename = "C:\\Projects\\Ambermoon\\Disks\\German\\Foo\\Amber_A.adf";
        var outPath = "C:\\Projects\\Ambermoon\\Disks\\German\\Foo\\Bar";

        ADFWriter.WriteADFFile("C:\\Projects\\Ambermoon\\Disks\\German\\Foo\\AMBER_FOO.adf", "AMBER_FOO", outPath, false, null, FileSystem.OFS, true, true, false);
#endif

#if EXTRACT
        ExtractDir(outPath, adf.RootDirectory);

        void ExtractDir(string path, IDirectory directory)
        {
            Directory.CreateDirectory(path);

            foreach (var file in directory.GetFiles())
            {
                File.WriteAllBytes(Path.Combine(path, file.Name), file.Data);
            }

            foreach (var subDir in directory.GetDirectories())
            {
                ExtractDir(Path.Combine(path, subDir.Name), subDir);
            }
        }
#endif

#if READ
        void PrintDir(IDirectory directory, string indent)
        {
            foreach (var file in directory.GetFiles())
            {
                Console.WriteLine(indent + $" {file.Name}: {file.Size} bytes");
            }

            foreach (var subDir in directory.GetDirectories())
            {
                Console.WriteLine(indent + $" {subDir.Name}");
                PrintDir(subDir, new string(' ', indent.Length + 1) + "+-");
            }
        }

        Console.WriteLine("Disk");
        PrintDir(adf.RootDirectory, "+-");
#endif
    }
}
