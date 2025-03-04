namespace AmigaConsole;

public class AmigaConsole
{
	private record Option(char ShortOption, string LongOption, string Description, bool HasParam, bool Optional)
	{
		public string? Parameter { get; set; }
	}
	private readonly List<Option> options = new();
	private readonly List<string> requiredArguments = new();
	private readonly List<string> optionalArguments = new();

	public void AddRequiredOption(char arg, string longArg, string description, bool hasParam = false)
	{
		options.Add(new Option(arg, longArg, description, hasParam, false));
	}

	public void AddOptionalOption(char arg, string longArg, string description, bool hasParam = false)
	{
		options.Add(new Option(arg, longArg, description, hasParam, true));
	}

	public void AddArguments(string[] required, string[] optional)
	{
		requiredArguments.AddRange(required.Select(arg => arg.Replace(' ', '_')));
		optionalArguments.AddRange(optional.Select(arg => arg.Replace(' ', '_')));
	}

	public void AddArguments(params string[] required)
	{
		AddArguments(required, Array.Empty<string>());
	}

	public void PrintHelp()
	{
		string required = string.Join(" ", requiredArguments.Select(arg => $"<{arg}>"));
		string optional = string.Join(" ", optionalArguments.Select(arg => $"[{arg}]"));
		string usage = "Usage: AmigaConsole";
		if (options.Count != 0)
			usage += " [options]";
		if (required.Length != 0)
			usage += " " + required;
		if (optional.Length != 0)
			usage += " " + optional;
		Console.WriteLine(usage);
		if (options.Count != 0)
		{
			Console.WriteLine();
			Console.WriteLine("Options:");
			foreach (var arg in options)
			{
				Console.WriteLine($"-{arg.ShortOption}, --{arg.LongOption}: {arg.Description}");
			}
		}
		Console.WriteLine();
	}

	public bool ParseArgs(string[] args, out Dictionary<string, string> arguments, out Dictionary<char, string?> options)
	{
		arguments = [];
		options = [];

		var foundArgs = new List<string>();
		var foundOptions = new List<Option>();
		bool waitingForOptionArg = false;

		foreach (string arg in args)
		{
			if (arg.StartsWith('-'))
			{
				if (arg.StartsWith("--"))
				{
					// Long arg
					var longArg = arg.Substring(2);
					var option = this.options.FirstOrDefault(a => a.LongOption == longArg);

					if (option == null)
					{
						Console.WriteLine($"Unknown option: {arg}");
						PrintHelp();
						return false;
					}

					if (option.HasParam)
					{
						waitingForOptionArg = true;
					}

					foundOptions.Add(option);
				}
				else
				{
					if (arg.Length > 2)
					{
						Console.WriteLine($"Unknown option: {arg}");
						PrintHelp();
						return false;
					}

					var shortArg = arg[1];
					var option = this.options.FirstOrDefault(a => a.ShortOption == shortArg);

					if (option == null)
					{
						Console.WriteLine($"Unknown option: {arg}");
						PrintHelp();
						return false;
					}

					if (option.HasParam)
					{
						waitingForOptionArg = true;
					}

					foundOptions.Add(option);
				}
			}
			else
			{
				if (waitingForOptionArg)
				{
					waitingForOptionArg = false;
					foundOptions.Last().Parameter = arg;
				}
				else
				{
					foundArgs.Add(arg);
				}
			}
		}

		if (waitingForOptionArg)
		{
			Console.WriteLine($"Missing argument for option -{foundOptions.Last().ShortOption}");
			PrintHelp();
			return false;
		}

		if (foundArgs.Count < requiredArguments.Count)
		{
			if (foundOptions.Any(option => option.ShortOption == 'h'))
			{
				options.Add('h', null);
				return true;
			}

			Console.WriteLine("Missing required arguments");
			PrintHelp();
			return false;
		}

		if (foundArgs.Count > requiredArguments.Count + optionalArguments.Count)
		{
			Console.WriteLine("Too many arguments");
			PrintHelp();
			return false;
		}

		if (this.options.Any(option => !option.Optional && foundOptions.All(found => found.ShortOption != option.ShortOption)))
		{
			Console.WriteLine($"Missing required option");
			PrintHelp();
			return false;
		}

		int requiredIndex = 0;
		int optionalIndex = 0;

		foreach (var argument in foundArgs)
		{
			if (requiredIndex < requiredArguments.Count)
			{
				arguments.Add(requiredArguments[requiredIndex], argument);
				requiredIndex++;
			}
			else
			{
				arguments.Add(optionalArguments[optionalIndex], argument);
				optionalIndex++;
			}
		}

		foreach (var option in foundOptions)
		{
			options.Add(option.ShortOption, option.Parameter);
		}

		return true;
	}
}
