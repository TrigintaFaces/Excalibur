// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Excalibur.Dispatch.Tests.CodeQuality;

/// <summary>
/// Structural guard ensuring every <c>AddOptions&lt;T&gt;()</c> call site in the
/// <c>Excalibur.Dispatch</c> core project chains <c>.ValidateOnStart()</c> in the same statement.
/// </summary>
/// <remarks>
/// <para>
/// The Microsoft-first design standard mandates fail-fast options validation: each
/// <c>AddOptions&lt;T&gt;()</c> registration must pair with <c>.ValidateOnStart()</c> so
/// misconfiguration surfaces at startup rather than on first use. This guard makes the gap
/// inexpressible going forward — any new <c>AddOptions&lt;T&gt;()</c> that omits
/// <c>.ValidateOnStart()</c> fails this test.
/// </para>
/// <para>
/// Detection is statement-level (brace/paren-aware) so fluent chains spanning multiple lines,
/// where <c>.ValidateOnStart()</c> appears on a different line than <c>AddOptions</c>, are handled.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "CodeQuality")]
public sealed class OptionsValidateOnStartGuardShould
{
	/// <summary>
	/// Verifies that every <c>AddOptions&lt;T&gt;()</c> site in Dispatch core chains <c>.ValidateOnStart()</c>.
	/// </summary>
	[Fact]
	public void Every_AddOptions_Call_Site_Chains_ValidateOnStart()
	{
		var srcPath = FindDispatchCoreDirectory();
		srcPath.ShouldNotBeNull(
			"Could not locate src/Dispatch/Excalibur.Dispatch from the test assembly base directory.");

		var violations = ScanForUnvalidatedAddOptions(srcPath);

		violations.Count.ShouldBe(0,
			$"Found {violations.Count} AddOptions<T>() call site(s) in Excalibur.Dispatch core without a paired " +
			$".ValidateOnStart(). Every options registration must fail-fast at startup. Violations:\n" +
			string.Join("\n", violations));
	}

	/// <summary>
	/// Scans the directory for <c>AddOptions&lt;</c> statements lacking <c>.ValidateOnStart(</c>.
	/// </summary>
	private static List<string> ScanForUnvalidatedAddOptions(string directory)
	{
		var violations = new List<string>();

		foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
		{
			if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
				file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
				file.EndsWith(".g.cs", StringComparison.Ordinal))
			{
				continue;
			}

			var originalContent = File.ReadAllText(file);
			var cleanContent = StripComments(originalContent);

			var pos = 0;
			while (pos < cleanContent.Length)
			{
				var idx = cleanContent.IndexOf("AddOptions<", pos, StringComparison.Ordinal);
				if (idx == -1)
				{
					break;
				}

				var stmtEnd = FindStatementEnd(cleanContent, idx);
				if (stmtEnd == -1)
				{
					pos = idx + "AddOptions<".Length;
					continue;
				}

				var stmtStart = FindStatementStart(cleanContent, idx);
				var statement = cleanContent[stmtStart..(stmtEnd + 1)];

				// (a) ValidateOnStart chained inline in the same statement, OR
				// (b) the AddOptions builder is assigned to a local that is later
				//     terminated with .ValidateOnStart() in a separate statement.
				var validated = statement.Contains(".ValidateOnStart(", StringComparison.Ordinal)
					|| IsValidatedViaBuilderVariable(statement, idx - stmtStart, cleanContent);

				if (!validated)
				{
					var lineNumber = CountNewlines(originalContent, idx) + 1;
					var relativePath = Path.GetRelativePath(directory, file);
					violations.Add($"  {relativePath}:{lineNumber}");
				}

				pos = stmtEnd + 1;
			}
		}

		return violations;
	}

	/// <summary>
	/// Determines whether an <c>AddOptions&lt;T&gt;()</c> builder assigned to a local variable is
	/// later terminated with <c>.ValidateOnStart()</c> in a separate statement within the same file.
	/// </summary>
	/// <param name="statement">The full assignment statement (LHS through the terminating semicolon).</param>
	/// <param name="addOptionsOffset">Offset of <c>AddOptions&lt;</c> within <paramref name="statement"/>.</param>
	/// <param name="cleanContent">The comment-stripped file content.</param>
	private static bool IsValidatedViaBuilderVariable(string statement, int addOptionsOffset, string cleanContent)
	{
		// Look at the LHS portion (before AddOptions<) for an assignment "... <name> = ... AddOptions<".
		var beforeAdd = statement[..addOptionsOffset];
		var eq = beforeAdd.LastIndexOf('=');
		if (eq <= 0)
		{
			return false;
		}

		var lhs = beforeAdd[..eq].Trim();
		var lastSpace = lhs.LastIndexOfAny([' ', '\t', '\n', '\r']);
		var varName = lastSpace >= 0 ? lhs[(lastSpace + 1)..] : lhs;

		if (varName.Length == 0 || varName == "_" || !IsIdentifier(varName))
		{
			return false;
		}

		// A separate statement that references the variable and ends a fluent chain with
		// .ValidateOnStart( — [^;{}] keeps the match inside a single statement.
		var pattern = $@"\b{Regex.Escape(varName)}\b[^;{{}}]*\.ValidateOnStart\(";
		return Regex.IsMatch(cleanContent, pattern);
	}

	private static bool IsIdentifier(string value)
	{
		if (!(char.IsLetter(value[0]) || value[0] == '_'))
		{
			return false;
		}

		foreach (var c in value)
		{
			if (!(char.IsLetterOrDigit(c) || c == '_'))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Scans backward from <paramref name="from"/> to the start of the enclosing statement
	/// (the position just after the previous <c>;</c>, <c>{</c>, or <c>}</c>).
	/// </summary>
	private static int FindStatementStart(string content, int from)
	{
		var i = from - 1;
		while (i >= 0)
		{
			var c = content[i];
			if (c is ';' or '{' or '}')
			{
				return i + 1;
			}

			i--;
		}

		return 0;
	}

	/// <summary>
	/// Finds the statement-ending semicolon starting at <paramref name="start"/>, tracking
	/// brace/paren depth and string literals so nested semicolons (e.g. in lambda bodies) are skipped.
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

	/// <summary>
	/// Strips single-line and block comments while preserving line structure for line counting.
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
							result[w++] = source[++i];
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
							result[w++] = source[++i];
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

	private static string? FindDispatchCoreDirectory()
	{
		var dir = AppContext.BaseDirectory;
		for (var i = 0; i < 10; i++)
		{
			var candidate = Path.Combine(dir, "src", "Dispatch", "Excalibur.Dispatch");
			if (Directory.Exists(candidate))
			{
				return candidate;
			}

			var parent = Directory.GetParent(dir);
			if (parent is null)
			{
				break;
			}

			dir = parent.FullName;
		}

		return null;
	}
}
