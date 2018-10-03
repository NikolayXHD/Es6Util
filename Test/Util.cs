using NUnit.Framework;

namespace Es6Util.Test
{
	[TestFixture]
	public class Util
	{
		[Test]
		public void Update_converted_directories()
		{
			Es6Converter.UpdateConvertedDirectory(sourceDir, convertedSourceDir);
			//Es6Converter.UpdateConvertedDirectory(testDir, convertedTestDir);
		}

		[Test]
		public void Update_original_directories()
		{

			Es6Converter.UpdateOriginalDirectory(convertedDir, dir, excludes);
		}

		[Test]
		public void Convert()
		{
			Es6Converter.CreateConvertedFile(
				@"D:\temp\customizeMarketplaceRepresentation.original.js",
				@"D:\temp\customizeMarketplaceRepresentation.js");
		}

		const string sourceDir = @"D:\repo\Git\shopping-experience-develop-2\application\src";
		const string convertedSourceDir = @"D:\repo\Git\shopping-experience-develop\application\src";

		const string dir = @"D:\repo\Git\shopping-experience-develop-2\application";
		const string convertedDir = @"D:\repo\Git\shopping-experience-develop\application";

		private static readonly string[] excludes =
		{
			".idea",
			".vs",
			".vscode",
			"dist",
			"gulp",
			"node_modules",
			"target"
		};
	}
}