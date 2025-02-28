using Amiga.FileFormats.ADF;
using Amiga.FileFormats.Core;

namespace AmigaAdf;

using AmigaConsole = AmigaConsole.AmigaConsole;

internal class Program
    {
        static void Main(string[] args)
        {
        var amigaConsole = new AmigaConsole();
		amigaConsole.AddArguments(["operation", "adfPath"], ["folderPath", "label"]);
		amigaConsole.AddOptionalOption('h', "help", "Show this help");
		amigaConsole.AddOptionalOption('b', "boot", "Create a bootable disk (only used for create)");
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
			Console.WriteLine("  list - List ADF contents");
			Console.WriteLine("  extract - Extract ADF contents");
			Console.WriteLine("  create - Create ADF from directory");
			Console.WriteLine("  add - Add/update files in ADF");
			Console.WriteLine();
		}

		void EnsureFolderPathArg()
		{
			if (!arguments.ContainsKey("folderPath"))
			{
				throw new ArgumentException("Missing folderPath argument");
			}
		}

		void EnsureLabelArg()
		{
			if (!arguments.ContainsKey("label"))
			{
				throw new ArgumentException("Missing label argument");
			}
		}

		try
		{
			var operation = arguments["operation"];

			switch (operation)
			{
				case "list":
					ListAdfContents(arguments["adfPath"]);
					break;
				case "extract":
					EnsureFolderPathArg();
					ExtractAdfContents(arguments["adfPath"], arguments["folderPath"]);
					break;
				case "create":
					EnsureFolderPathArg();
					EnsureLabelArg();
					CreateAdfFromDirectory(arguments["adfPath"], arguments["folderPath"], arguments["label"], options.ContainsKey('b'));
					break;
				case "add":
					EnsureFolderPathArg();
					AddFilesToAdf(arguments["adfPath"], arguments["folderPath"]);
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

        static void ListAdfContents(string adfPath)
        {
        var adf = ADFReader.LoadADFFile(adfPath, true);

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
        PrintDir(adf.RootDirectory, "+-");
	}

        static void ExtractAdfContents(string adfPath, string folderPath)
        {
        var adf = ADFReader.LoadADFFile(adfPath, true);
		ExtractAdfContents(adf, folderPath);
	}

        static void ExtractAdfContents(IADF adf, string folderPath)
        {
        ExtractDir(folderPath, adf.RootDirectory);

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

	static void CreateAdfFromDirectory(string adfPath, string folderPath, string label, bool bootable)
        {
        ADFWriter.WriteADFFile(adfPath, label, folderPath, false, null, FileSystem.OFS, bootable, false, false);
	}

        static void AddFilesToAdf(string adfPath, string folderPath)
        {
		var tempPath = Path.Combine(Path.GetTempPath(), "AmigaAdf");
		if (Directory.Exists(tempPath))
			Directory.Delete(tempPath, true);
		var adf = ADFReader.LoadADFFile(adfPath, true);
		ExtractAdfContents(adf, tempPath);
		ADFWriter.WriteADFFile(adfPath, adf.DiskName, folderPath, false, null, ADFWriterConfiguration.FromADF(adf));
	}
    }