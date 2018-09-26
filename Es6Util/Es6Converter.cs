using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Es6Util
{
	public static class Es6Converter
	{
		public static void UpdateConvertedDirectory(string inputPath, string outputPath) =>
			SyncDirectories(inputPath, outputPath, CreateConvertedFile);

		public static void UpdateOriginalDirectory(string convertedPath, string originalPath) =>
			SyncDirectories(convertedPath, originalPath, UpdateOriginalFile);

		private static void SyncDirectories(string inputPath, string outputPath, Action<string, string> syncFiles)
		{
			var inputDir = new DirectoryInfo(inputPath);
			if (!inputDir.Exists)
				return;

			string searchPattern = "*.js";
			foreach (var inputFileInfo in inputDir.EnumerateFiles(searchPattern, SearchOption.AllDirectories))
			{
				var inputFile = inputFileInfo.FullName;
				var outputFile = outputPath + inputFile.Substring(inputPath.Length);

				string outputSubdir = Path.GetDirectoryName(outputFile);
				if (outputSubdir == null)
					throw new Exception($"Failed to get parent dir for: {outputFile}");

				Directory.CreateDirectory(outputSubdir);

				syncFiles(inputFile, outputFile);
			}

			var outputDir = new DirectoryInfo(outputPath);

			foreach (var outputFileInfo in outputDir.EnumerateFiles(searchPattern, SearchOption.AllDirectories))
			{
				var outputFile = outputFileInfo.FullName;
				var inputFile = inputPath + outputFile.Substring(outputPath.Length);

				if (!File.Exists(inputFile))
					File.Delete(outputFile);
			}
		}

		public static void CreateConvertedFile(string inputFile, string outputFile)
		{
			if (!File.Exists(inputFile))
				return;

			string content = File.ReadAllText(inputFile);
			string convertedContent;
			try
			{
				convertedContent = ConvertToEs6(content, inputFile);
			}
			catch (FormatException)
			{
				convertedContent = content;
			}

			File.WriteAllText(outputFile, convertedContent);
		}

		public static void UpdateOriginalFile(string convertedFile, string originalFile)
		{
			var convertedContent = new Lazy<string>(() =>
				File.ReadAllText(convertedFile));

			var originalContent = new Lazy<string>(() =>
				File.ReadAllText(originalFile));

			var originalIsEs6 = new Lazy<bool>(() =>
				!_oldModulePattern.IsMatch(originalContent.Value));

			var convertedIsEs6 = new Lazy<bool>(() =>
				!_oldModulePattern.IsMatch(convertedContent.Value));

			bool canSaveAsIs = !File.Exists(originalFile) || originalIsEs6.Value || !convertedIsEs6.Value;
			if (canSaveAsIs)
				File.Copy(convertedFile, originalFile, overwrite: true);
			else
			{
				Match oldModuleMatch = _oldModulePattern.Match(originalContent.Value);
				string content = ConvertFromEs6(convertedContent.Value, eol: GetEol(originalContent.Value), oldModuleMatch, originalFile);

				var trailingWhitespaces = new Regex(@"[\s\n]+$").Match(originalContent.Value);
				if (trailingWhitespaces.Success)
					content += trailingWhitespaces.Value;

				var bytes = File.ReadAllBytes(originalFile);
				var bom = Encoding.UTF8.GetPreamble();

				if (bytes.Take(bom.Length).SequenceEqual(bom))
					File.WriteAllBytes(originalFile, bom.Concat(Encoding.UTF8.GetBytes(content)).ToArray());
				else
					File.WriteAllText(originalFile, content);
			}
		}

		public static string ConvertToEs6(string content, string fileName = null)
		{
			var match = _oldModulePattern.Match(content);

			if (!match.Success)
				return content;

			var result = new StringBuilder();

			var moduleNameCaptures = match.Groups["mname"].Captures;
			var variableNameCaptures = match.Groups["pname"].Captures;

			if (variableNameCaptures.Count > moduleNameCaptures.Count)
				// can be < because of modules like 'aps/ready!' used without any mapped variable
				throw new FormatException();

			int variablesCount = variableNameCaptures.Count;
			int modulesCount = moduleNameCaptures.Count;

			for (int i = 0; i < variablesCount; i++)
			{
				var moduleNameCapture = moduleNameCaptures[i];
				var variableNameCapture = variableNameCaptures[i];

				result.AppendLine($"import {variableNameCapture.Value} from \'{moduleNameCapture}\';");

				if (i < modulesCount - 1)
				{
					var nextModuleNameCapture = moduleNameCaptures[i + 1];
					int moduleSeparatorStart = moduleNameCapture.Index + moduleNameCapture.Length;
					int moduleSeparatorEnd = nextModuleNameCapture.Index;

					string moduleSeparator = content.Substring(moduleSeparatorStart, moduleSeparatorEnd - moduleSeparatorStart);
					int emptyLinesCount = moduleSeparator.Count(c => c == '\n') - 1;

					if (emptyLinesCount > 0)
						result.AppendLine();
				}
			}

			for (int i = variablesCount; i < modulesCount; i++)
			{
				var moduleNameCapture = moduleNameCaptures[i];
				result.AppendLine($"import \'{moduleNameCapture}\';");
			}

			if (moduleNameCaptures.Count > 0)
				result.AppendLine();

			string body = match.Groups["body"].Value;
			body = body.Trim();
			body = _useStrictPattern.Replace(body, string.Empty);
			body = Unindent(body);

			var lastReturnStatement = FindLastReturnStatementInOutermostScope(body);
			if (lastReturnStatement != null)
			{
				var prefix = body.Substring(0, lastReturnStatement.Index);
				var suffix = body.Substring(lastReturnStatement.Index + lastReturnStatement.Length);

				result.Append(prefix);

				if (_functionDeclarationPattern.IsMatch(suffix))
				{
					var exportVarName = Path.GetFileNameWithoutExtension(fileName);
					result.Append($"const {exportVarName} =");
					result.Append(suffix);
					result.AppendLine($"export default {exportVarName};");
				}
				else
				{
					result.Append("export default");
					result.Append(suffix);
				}
			}
			else
			{
				// unit tests do not have export
				result.Append(body);
			}

			return result.ToString();
		}

		private static string ConvertFromEs6(string content, string eol, Match oldModuleMatch, string originalFileName)
		{
			bool useStrict = ContainsUseStrictDirective(oldModuleMatch);
			bool eolAfterUseStrict = ContainsEolAfterUseStrict(oldModuleMatch);
			bool multilineParams = IsMultiLineParams(oldModuleMatch);
			bool whitespaceBeforeParams = HasWhitespaceBeforeParams(oldModuleMatch);
			bool blankLineBeforeBody = HasBlankLineBeforeBody(oldModuleMatch);
			bool blankLineAfterBody = HasBlankLineAfterBody(oldModuleMatch);
			bool hasModuleList = HasModuleList(oldModuleMatch);
			bool multilineModuleList = IsMultilineModuleList(oldModuleMatch);
			bool isEolAfterLastParam = IsEolAfterLastParam(oldModuleMatch);

			var importMatches = _es6ImportsPattern.Matches(content);
			//modules like 'aps/ready!' are used without variable
			IList<Match> matchesWithVariable = importMatches.Cast<Match>()
				.TakeWhile(m => m.Groups["pname"].Success)
				.ToArray();

			var result = new StringBuilder();

			result.Append("define(");

			if (hasModuleList)
			{
				result.Append("[");

				if (multilineModuleList)
					result.Append(eol);

				for (int i = 0; i < importMatches.Count; i++)
				{
					Match match = importMatches[i];

					if (multilineModuleList)
						result.Append("\t");

					result.Append("'").Append(match.Groups["mname"].Value).Append("'");

					if (i < importMatches.Count - 1)
					{
						result.Append(",");
						if (multilineModuleList)
							result.Append(eol);
						else
							result.Append(" ");
					}

					if (match.Groups["emptyline"].Success && multilineModuleList)
						result.Append(eol);
				}

				result.Append("], ");
			}

			result.Append("function");
			if (whitespaceBeforeParams)
				result.Append(" ");

			result.Append("(");

			if (multilineParams)
				result.Append(eol);

			for (int i = 0; i < matchesWithVariable.Count; i++)
			{
				Match match = matchesWithVariable[i];

				if (!match.Groups["pname"].Success)
					break;

				if (multilineParams)
					result.Append("\t");

				result.Append(match.Groups["pname"].Value);
				if (i < matchesWithVariable.Count - 1)
				{
					result.Append(",");

					if (multilineParams)
					{
						result.Append(eol);
						if (match.Groups["emptyline"].Success)
							result.Append(eol);
					}
					else
						result.Append(" ");
				}
			}

			if (matchesWithVariable.Count > 0 && isEolAfterLastParam)
				result.Append(eol);

			result.Append(") {").Append(eol);

			if (blankLineBeforeBody)
				result.Append(eol);

			if (useStrict)
			{
				result.Append("\t'use strict';").Append(eol);

				if (eolAfterUseStrict)
					result.Append(eol);
			}

			string body;
			if (importMatches.Count > 0)
			{
				var lastMatch = importMatches[importMatches.Count - 1];
				body = content.Substring(lastMatch.Index + lastMatch.Length);
			}
			else
			{
				body = content;
			}

			body = body.TrimEnd();
			body = ReplaceExportWithReturn(body, eol, oldModuleMatch, originalFileName);

			var bodyLines = body.Split(new[] {"\r\n", "\n"}, StringSplitOptions.None);

			foreach (string line in bodyLines)
			{
				if (!string.IsNullOrWhiteSpace(line))
					result.Append('\t').Append(line).Append(eol);
				else
					result.Append(eol);
			}

			if (blankLineAfterBody)
				result.Append(eol);

			result.Append("});");

			return result.ToString();
		}

		private static string ReplaceExportWithReturn(string body, string eol, Match oldModuleMatch, string originalFile)
		{
			string exportDefaultPattern = "export default";
			int exportLocation = body.IndexOf(exportDefaultPattern, StringComparison.InvariantCulture);

			if (exportLocation < 0)
				return body;

			var exportFunctionInOriginalFile = FindExportFunction(oldModuleMatch);
			if (exportFunctionInOriginalFile != null)
			{
				var exportVarName = Path.GetFileNameWithoutExtension(originalFile);
				string extractedVarDeclaration = $"const {exportVarName} = ";
				string exportStatement = $"export default {exportVarName};";

				body = body
					.Replace(exportStatement, string.Empty).TrimEnd()
					.Replace(extractedVarDeclaration, "return ");

				return body;
			}

			body = body.Replace(exportDefaultPattern, "return");
			return body;
		}

		private static bool ContainsUseStrictDirective(Match oldModuleMatch) =>
			oldModuleMatch.Groups["body"].Value.Contains("'use strict';");

		private static bool ContainsEolAfterUseStrict(Match oldModuleMatch)
		{
			var body = oldModuleMatch.Groups["body"].Value;
			return body.Contains("'use strict';\n\n") || body.Contains("'use strict';\r\n\r\n");
		}

		private static bool IsMultiLineParams(Match oldModuleMatch) =>
			oldModuleMatch.Groups["pnames"].Value.Contains('\n');

		private static bool HasWhitespaceBeforeParams(Match oldModuleMatch) =>
			!string.IsNullOrEmpty(oldModuleMatch.Groups["wsbeforeparams"].Value);

		private static bool HasBlankLineBeforeBody(Match oldModuleMatch)
		{
			string body = oldModuleMatch.Groups["body"].Value;
			return body.StartsWith("\r\n\r\n") || body.StartsWith("\n\n");
		}

		private static bool HasBlankLineAfterBody(Match oldModuleMatch)
		{
			string body = oldModuleMatch.Groups["body"].Value;
			return body.EndsWith("\r\n\r\n") || body.EndsWith("\n\n");
		}

		private static bool HasModuleList(Match oldModuleMatch) =>
			oldModuleMatch.Groups["mnames"].Success;

		private static bool IsMultilineModuleList(Match oldModuleMatch) =>
			oldModuleMatch.Groups["mnames"].Value.Contains("\n");

		private static bool IsEolAfterLastParam(Match oldModuleMatch)
		{
			var grp = oldModuleMatch.Groups["pnames"];
			return grp.Success && grp.Value.EndsWith("\n");
		}

		private static Match FindExportFunction(Match oldModuleMatch)
		{
			if (!oldModuleMatch.Success)
				return null;

			string oldBody = oldModuleMatch.Groups["body"].Value;
			var lastReturnStatement = FindLastReturnStatementInOutermostScope(oldBody);

			if (!lastReturnStatement.Success)
				return null;

			var oldSuffix = oldBody.Substring(
				lastReturnStatement.Index + lastReturnStatement.Length);

			var functionDeclarationMatch = _functionDeclarationPattern.Match(oldSuffix);

			if (!functionDeclarationMatch.Success)
				return null;

			return functionDeclarationMatch;
		}

		private static string GetEol(string originalContent) =>
			originalContent.Contains("\r\n")
				? "\r\n"
				: "\n";

		private static string Unindent(string body)
		{
			const char indentChar = '\t';
			var lines = body.Split(new[] {"\r\n", "\n"}, StringSplitOptions.None);

			var result = new StringBuilder();
			foreach (string line in lines)
			{
				if (!string.IsNullOrEmpty(line))
				{
					if (line[0] == indentChar)
						result.AppendLine(line.Substring(1));
					else if (line.Length >= 4 && line.Substring(0, 4) == "    ")
						result.AppendLine(line.Substring(4));
					else
						result.AppendLine(line);
				}
				else
					result.AppendLine(line);
			}

			return result.ToString();
		}

		private static Match FindLastReturnStatementInOutermostScope(string body)
		{
			var normalCodePositions = new HashSet<int>(Enumerable.Range(0, body.Length));

			var literalAndCommentMatches = _commentOrStringLiteralPattern.Matches(body);

			foreach (Match match in literalAndCommentMatches)
				for (int i = match.Index; i < match.Index + match.Length; i++)
					normalCodePositions.Remove(i);

			var scopeOpenerPositions = new HashSet<int>(
				_scopeOpenerPattern.Matches(body)
					.Cast<Match>()
					.Select(m => m.Index)
					.Where(normalCodePositions.Contains));

			var scopeCloserPositions = new HashSet<int>(
				_scopeCloserPattern.Matches(body)
					.Cast<Match>()
					.Select(m => m.Index)
					.Where(normalCodePositions.Contains));

			var returnStatementMatches = _returnStatementPattern.Matches(body)
				.Cast<Match>()
				.ToArray();

			var returnStatementPositions = new HashSet<int>(
				returnStatementMatches
					.Select(m => m.Index)
					.Where(normalCodePositions.Contains));

			var allPositions = scopeOpenerPositions
				.Concat(scopeCloserPositions)
				.Concat(returnStatementPositions)
				.OrderBy(_ => _)
				.ToArray();

			int nesting = 0;
			for (int i = allPositions.Length - 1; i >= 0; i--)
			{
				var position = allPositions[i];

				if (scopeCloserPositions.Contains(position))
					nesting++;
				else if (scopeOpenerPositions.Contains(position))
					nesting--;
				else if (returnStatementPositions.Contains(position) && nesting == 0)
					return returnStatementMatches.First(m => m.Index == position);
			}

			return null;
		}

		private static readonly Regex _commentOrStringLiteralPattern = new Regex(@"(?:\/\*(?:[^*]|\*(?!\/))*\*\/|\/\/.*(?:\n|$)|'(?:[^']|(?<=\\)')*'|""(?:[^""]|(?<=\\)"")*"")");

		private static readonly Regex _scopeOpenerPattern = new Regex("{");
		private static readonly Regex _scopeCloserPattern = new Regex("}");

		private static readonly Regex _returnStatementPattern = new Regex(@"\breturn\b");
		private static readonly Regex _functionDeclarationPattern = new Regex(@"^\s*function\b");

		private static readonly Regex _useStrictPattern = new Regex(@"^[\s\n]*('use strict'|""use strict"");[\s\n]*\n");

		private static readonly Regex _oldModulePattern = new Regex(
			@"^\s*define\s*\(\s*(?<mnames>\[\s*(?:'(?<mname>[^']+)'\s*(,|(?=\s*\]))\s*)*\]\s*,\s*)?function(?<wsbeforeparams>\s*)\((?<pnames>\s*(?:(?<pname>[^\s]+)\s*(,|(?=\s*\)))\s*)*\s*)?\)\s*\{(?<body>(.*\n)*)\s*\}\s*\)\s*;\s*$");

		private static readonly Regex _es6ImportsPattern = new Regex(@"import (?:(?<pname>[^\s]+) from )?'(?<mname>[^']+)';(?:\r?\n)(?<emptyline>\r?\n)?");
	}
}