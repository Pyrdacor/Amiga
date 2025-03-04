using Amiga.FileFormats.LHA;
using Amiga.FileFormats.Core;

namespace AmigaLha;

using AmigaConsole = AmigaConsole.AmigaConsole;

internal class Program
{
    static readonly Dictionary<string, CompressionMethod> SupportedCompressionMethods =
        new()
        {
            { "lh0", CompressionMethod.None },
            { "lh5", CompressionMethod.LH5 },
            { "lh6", CompressionMethod.LH6 },
            { "lh7", CompressionMethod.LH7 },
            //{ "lz5", CompressionMethod.LZ5 }
        };

    static void Main(string[] args)
    {
        var nl = Environment.NewLine;
        var amigaConsole = new AmigaConsole();
        amigaConsole.AddArguments(["operation", "lhaPath"], ["folderPath"]);
        amigaConsole.AddOptionalOption('h', "help", "Show this help");
        amigaConsole.AddOptionalOption('c', "compression-method", $"One of the following compression methods{nl} lh0 (no compression){nl} lh5 (default){nl} lh6{nl} lh7", true);
        if (!amigaConsole.ParseArgs(args, out var arguments, out var options) || options.ContainsKey('h'))
        {
            PrintHelp(options.ContainsKey('h'));
            return;
        }

        void PrintHelp(bool withUsage)
        {
            if (withUsage)
                amigaConsole.PrintHelp();
            Console.WriteLine("Operations:");
            Console.WriteLine("  list - List LHA contents");
            Console.WriteLine("  extract - Extract LHA contents");
            Console.WriteLine("  create - Create LHA from directory");
            Console.WriteLine("  add - Add/update files in LHA");
            Console.WriteLine();
        }

        void EnsureFolderPathArg()
        {
            if (!arguments.ContainsKey("folderPath"))
            {
                throw new ArgumentException("Missing folderPath argument");
            }
        }

        CompressionMethod GetCompressionMethod()
        {
            string originalCompressionMethod = options.TryGetValue('c', out var value) && value != null ? value : "lh5";

            if (!SupportedCompressionMethods.TryGetValue(originalCompressionMethod.Trim('-').ToLower(), out var compressionMethod))
            {
                throw new ArgumentException("Unknown compression method: " + originalCompressionMethod);
            }

            return compressionMethod;
        }

        try
        {
            var operation = arguments["operation"];

            switch (operation)
            {
                case "list":
                    ListLhaContents(arguments["lhaPath"]);
                    break;
                case "extract":
                    EnsureFolderPathArg();
                    ExtractLhaContents(arguments["lhaPath"], arguments["folderPath"]);
                    break;
                case "create":
                    EnsureFolderPathArg();
                    CreateLhaFromDirectory(arguments["lhaPath"], arguments["folderPath"], GetCompressionMethod());
                    break;
                case "add":
                    EnsureFolderPathArg();
                    AddFilesToLha(arguments["lhaPath"], arguments["folderPath"], GetCompressionMethod());
                    break;
                default:
                    Console.WriteLine("Unknown operation: " + operation);
                    PrintHelp(true);
                    break;
            }
        }
        catch (ArgumentException e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine();
            PrintHelp(true);
        }
        catch (Exception e)
        {
            Console.WriteLine("ERROR: " + e.Message);
        }
    }

    static void ListLhaContents(string lhaPath)
    {
        var lha = LHAReader.LoadLHAFile(lhaPath);

        static void PrintDir(IDirectory directory, string indent)
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
        PrintDir(lha.RootDirectory, "+-");
    }

    static void ExtractLhaContents(string lhaPath, string folderPath)
    {
        var lha = LHAReader.LoadLHAFile(lhaPath);
        ExtractLhaContents(lha, folderPath);
    }

    static void ExtractLhaContents(ILHA lha, string folderPath)
    {
        ExtractDir(folderPath, lha.RootDirectory);

        static void ExtractDir(string path, IDirectory directory)
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
    }

    static void CreateLhaFromDirectory(string lhaPath, string folderPath, CompressionMethod compressionMethod)
    {
        LHAWriter.WriteLHAFile(lhaPath, folderPath, null, compressionMethod);
    }

    static void AddFilesToLha(string lhaPath, string folderPath, CompressionMethod compressionMethod)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "AmigaLha");
        if (Directory.Exists(tempPath))
            Directory.Delete(tempPath, true);
        var lha = LHAReader.LoadLHAFile(lhaPath);
        ExtractLhaContents(lha, tempPath);
        LHAWriter.WriteLHAFile(lhaPath, folderPath, null, compressionMethod);
    }
}