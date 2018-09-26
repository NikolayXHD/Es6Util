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
			Es6Converter.UpdateOriginalDirectory(convertedSourceDir, sourceDir);
			Es6Converter.UpdateOriginalDirectory(convertedTestDir, testDir);
		}

		[Test]
		public void Convert()
		{
			Es6Converter.UpdateOriginalFile(@"D:\temp\cart.js", @"D:\temp\cart.original.js");
		}

		const string sourceDir = @"D:\repo\Git\shopping-experience-develop-2\application\src";
		const string convertedSourceDir = @"D:\repo\Git\shopping-experience-develop\application\src";

		const string testDir = @"D:\repo\Git\shopping-experience-develop-2\application\tests";
		const string convertedTestDir = @"D:\repo\Git\shopping-experience-develop\application\tests";
	}
}