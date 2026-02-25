// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.CodeQuality;

/// <summary>
/// Boundary test ensuring all production <c>await</c> calls use <c>ConfigureAwait(false)</c>.
/// Libraries must use ConfigureAwait(false) to avoid deadlocks when called from synchronization contexts.
/// </summary>
/// <remarks>
/// <para>Sprint 530: zero bare <c>await</c> calls in <c>src/Dispatch/</c> and <c>src/Excalibur/</c>.</para>
/// <para>
/// Uses statement-level detection (brace/paren-aware) so multi-line await expressions
/// where ConfigureAwait(false) appears on a different line than <c>await</c> are correctly handled.
/// </para>
/// <para>
/// Legitimate exceptions (not counted as violations):
/// <list type="bullet">
/// <item><c>await foreach</c> / <c>await using</c> — different ConfigureAwait syntax</item>
/// <item><c>await Task.Yield()</c> — <c>YieldAwaitable</c> does not support ConfigureAwait</item>
/// <item>XML doc comments (<c>///</c> and <c>//</c>) — not executable code</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "CodeQuality")]
public sealed class ConfigureAwaitBoundaryShould
{
	/// <summary>
	/// Ceiling for bare await violations in src/Dispatch/. Set to 0 — all violations resolved.
	/// </summary>
	private const int DispatchViolationCeiling = 0;

	/// <summary>
	/// Ceiling for bare await violations in src/Excalibur/. Set to 0 — all violations resolved.
	/// </summary>
	private const int ExcaliburViolationCeiling = 0;

	/// <summary>
	/// Verifies that Dispatch production code has zero bare <c>await</c> violations.
	/// </summary>
	[Fact]
	public void Dispatch_Bare_Await_Count_Does_Not_Exceed_Ceiling()
	{
		var srcPath = FindSrcDirectory("Dispatch");
		if (srcPath is null)
		{
			return; // Skip if running from a different working directory
		}

		var violations = ScanForBareAwaits(srcPath);

		violations.Count.ShouldBeLessThanOrEqualTo(DispatchViolationCeiling,
			$"Bare 'await' violations in src/Dispatch/ increased to {violations.Count} " +
			$"(ceiling: {DispatchViolationCeiling}). " +
			$"New code must use ConfigureAwait(false). First 10 violations:\n" +
			string.Join("\n", violations.Take(10)));
	}

	/// <summary>
	/// Verifies that Excalibur production code has zero bare <c>await</c> violations.
	/// </summary>
	[Fact]
	public void Excalibur_Bare_Await_Count_Does_Not_Exceed_Ceiling()
	{
		var srcPath = FindSrcDirectory("Excalibur");
		if (srcPath is null)
		{
			return; // Skip if running from a different working directory
		}

		var violations = ScanForBareAwaits(srcPath);

		violations.Count.ShouldBeLessThanOrEqualTo(ExcaliburViolationCeiling,
			$"Bare 'await' violations in src/Excalibur/ increased to {violations.Count} " +
			$"(ceiling: {ExcaliburViolationCeiling}). " +
			$"New code must use ConfigureAwait(false). First 10 violations:\n" +
			string.Join("\n", violations.Take(10)));
	}

	/// <summary>
	/// Scans for bare await statements using statement-level detection.
	/// Tracks brace/paren depth to correctly handle multi-line await expressions
	/// where ConfigureAwait(false) appears on a different line than the await keyword.
	/// </summary>
	private static List<string> ScanForBareAwaits(string directory)
	{
		var violations = new List<string>();

		foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
		{
			// Skip build output and generated files
			if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
				file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
				file.EndsWith(".g.cs", StringComparison.Ordinal))
			{
				continue;
			}

			var content = File.ReadAllText(file);
			var cleanContent = StripComments(content);
			var fileViolations = FindBareAwaitStatements(cleanContent, content);

			foreach (var (lineNumber, lineText) in fileViolations)
			{
				var relativePath = Path.GetRelativePath(directory, file);
				violations.Add($"  {relativePath}:{lineNumber}: {lineText}");
			}
		}

		return violations;
	}

	/// <summary>
	/// Strips single-line and block comments from C# source, preserving line structure.
	/// </summary>
	private static string StripComments(string source)
	{
		var result = new char[source.Length];
		var inBlockComment = false;
		var inLineComment = false;
		var inString = false;
		var inVerbatimString = false;
		var i = 0;
		var w = 0;

		while (i < source.Length)
		{
			if (inBlockComment)
			{
				if (i + 1 < source.Length && source[i] == '*' && source[i + 1] == '/')
				{
					result[w++] = ' ';
					result[w++] = ' ';
					i += 2;
					inBlockComment = false;
				}
				else
				{
					// Preserve newlines for line counting
					result[w++] = source[i] == '\n' ? '\n' : ' ';
					i++;
				}

				continue;
			}

			if (inLineComment)
			{
				if (source[i] == '\n')
				{
					result[w++] = '\n';
					i++;
					inLineComment = false;
				}
				else
				{
					result[w++] = ' ';
					i++;
				}

				continue;
			}

			if (inString)
			{
				result[w++] = source[i];
				if (inVerbatimString)
				{
					if (source[i] == '"')
					{
						if (i + 1 < source.Length && source[i + 1] == '"')
						{
							result[w++] = source[++i]; // escaped quote
						}
						else
						{
							inString = false;
							inVerbatimString = false;
						}
					}
				}
				else
				{
					if (source[i] == '\\')
					{
						if (i + 1 < source.Length)
						{
							result[w++] = source[++i]; // skip escaped char
						}
					}
					else if (source[i] == '"')
					{
						inString = false;
					}
				}

				i++;
				continue;
			}

			// Check for string start
			if (source[i] == '@' && i + 1 < source.Length && source[i + 1] == '"')
			{
				result[w++] = source[i];
				result[w++] = source[i + 1];
				i += 2;
				inString = true;
				inVerbatimString = true;
				continue;
			}

			if (source[i] == '"')
			{
				result[w++] = source[i++];
				inString = true;
				continue;
			}

			// Check for comments
			if (i + 1 < source.Length && source[i] == '/')
			{
				if (source[i + 1] == '/')
				{
					result[w++] = ' ';
					result[w++] = ' ';
					i += 2;
					inLineComment = true;
					continue;
				}

				if (source[i + 1] == '*')
				{
					result[w++] = ' ';
					result[w++] = ' ';
					i += 2;
					inBlockComment = true;
					continue;
				}
			}

			result[w++] = source[i++];
		}

		return new string(result, 0, w);
	}

	/// <summary>
	/// Finds await statements that lack ConfigureAwait, tracking brace/paren depth
	/// to correctly locate statement-ending semicolons across multi-line expressions.
	/// </summary>
	private static List<(int LineNumber, string LineText)> FindBareAwaitStatements(
		string cleanContent,
		string originalContent)
	{
		var violations = new List<(int LineNumber, string LineText)>();
		var pos = 0;

		while (pos < cleanContent.Length)
		{
			var awaitIdx = cleanContent.IndexOf("await ", pos, StringComparison.Ordinal);
			if (awaitIdx == -1)
			{
				break;
			}

			// Get text after "await "
			var afterAwait = cleanContent[(awaitIdx + 6)..].TrimStart();

			// Skip await foreach / await using
			if (afterAwait.StartsWith("foreach", StringComparison.Ordinal) ||
				afterAwait.StartsWith("using", StringComparison.Ordinal))
			{
				pos = awaitIdx + 6;
				continue;
			}

			// Find statement-ending semicolon with brace/paren tracking
			var stmtEnd = FindStatementEnd(cleanContent, awaitIdx + 6);
			if (stmtEnd == -1)
			{
				pos = awaitIdx + 6;
				continue;
			}

			var statement = cleanContent[awaitIdx..(stmtEnd + 1)];

			// Check for ConfigureAwait in the full statement
			if (!statement.Contains("ConfigureAwait", StringComparison.Ordinal))
			{
				// Skip Task.Yield() — YieldAwaitable doesn't support ConfigureAwait
				if (!statement.Contains("Task.Yield()", StringComparison.Ordinal))
				{
					var lineNumber = CountNewlines(originalContent, awaitIdx) + 1;
					var lineText = GetLineAt(originalContent, awaitIdx).Trim();
					violations.Add((lineNumber, lineText));
				}
			}

			pos = stmtEnd + 1;
		}

		return violations;
	}

	/// <summary>
	/// Finds the position of the statement-ending semicolon, tracking brace and paren depth
	/// so that semicolons inside lambda bodies or nested expressions are skipped.
	/// </summary>
	private static int FindStatementEnd(string content, int start)
	{
		var depthBrace = 0;
		var depthParen = 0;
		var inString = false;
		var inVerbatim = false;
		var i = start;

		while (i < content.Length)
		{
			var c = content[i];

			if (inString)
			{
				if (inVerbatim)
				{
					if (c == '"')
					{
						if (i + 1 < content.Length && content[i + 1] == '"')
						{
							i += 2;
							continue;
						}

						inString = false;
						inVerbatim = false;
					}
				}
				else
				{
					if (c == '\\')
					{
						i += 2;
						continue;
					}

					if (c == '"')
					{
						inString = false;
					}
				}

				i++;
				continue;
			}

			if (c == '@' && i + 1 < content.Length && content[i + 1] == '"')
			{
				inString = true;
				inVerbatim = true;
				i += 2;
				continue;
			}

			if (c == '"')
			{
				inString = true;
				i++;
				continue;
			}

			switch (c)
			{
				case '{':
					depthBrace++;
					break;
				case '}':
					depthBrace--;
					break;
				case '(':
					depthParen++;
					break;
				case ')':
					depthParen--;
					break;
				case ';' when depthBrace <= 0 && depthParen <= 0:
					return i;
			}

			i++;
		}

		return -1;
	}

	private static int CountNewlines(string content, int upTo)
	{
		var count = 0;
		for (var i = 0; i < upTo && i < content.Length; i++)
		{
			if (content[i] == '\n')
			{
				count++;
			}
		}

		return count;
	}

	private static string GetLineAt(string content, int position)
	{
		var lineStart = content.LastIndexOf('\n', position) + 1;
		var lineEnd = content.IndexOf('\n', position);
		if (lineEnd == -1) lineEnd = content.Length;
		return content[lineStart..lineEnd];
	}

	private static string? FindSrcDirectory(string subDirectory)
	{
		var dir = AppContext.BaseDirectory;
		for (var i = 0; i < 10; i++)
		{
			var candidate = Path.Combine(dir, "src", subDirectory);
			if (Directory.Exists(candidate))
			{
				return candidate;
			}

			var parent = Directory.GetParent(dir);
			if (parent is null) break;
			dir = parent.FullName;
		}

		return null;
	}
}
