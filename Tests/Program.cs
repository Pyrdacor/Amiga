using Amiga.FileFormats.ADF;
using System.Text;

static class Program
{
    static void Main(string[] args)
    {
        string filename = "C:\\Projects\\Ambermoon\\Disks\\German\\Foo\\Amber_A.adf";
        var adf = ADFReader.LoadADFFile(filename, false);

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
    }
}
