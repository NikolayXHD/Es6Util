using System;

namespace Es6Util
{
	internal class Program
	{
		public static void Main(string[] argsArr)
		{
			Arguments args;
			try
			{
				args = Arguments.Parse(argsArr);
			}
			catch (FormatException)
			{
				Console.Write(Arguments.GetHelp());
				return;
			}

			if (args.IsDirectory)
			{
			}
			else
			{
				Es6Converter.CreateConvertedFile(args.Input, args.Output);
			}
		}

		internal class Arguments
		{
			public static Arguments Parse(string[] args)
			{
				var result = new Arguments();
				if (args.GetFlag("-f"))
					result.IsDirectory = false;
				else if (args.GetFlag("-d"))
					result.IsDirectory = true;
				else
					throw new ArgumentException();

				result.Input = args.GetParam("-i") ?? throw new ArgumentException();
				result.Output = args.GetParam("-o") ?? result.Input;

				return result;
			}

			public static string GetHelp()
			{
				return "Usage\r\n" +
					"\tEs6Util.exe -f -i input_file -o output_file\r\n" +
					"\tEs6Util.exe -d -i input_directory -o output_directory\r\n";
			}

			public bool IsDirectory { get; set; }
			public string Input { get; set; }
			public string Output { get; set; }
		}
	}
}