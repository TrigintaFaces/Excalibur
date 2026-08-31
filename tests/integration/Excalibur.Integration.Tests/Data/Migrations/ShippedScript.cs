// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Integration.Tests.Data.Migrations;

/// <summary>
/// Reads a shipped script as the bytes a consumer receives.
/// </summary>
/// <remarks>
/// This duplicates the path walk in <c>ShippedSchemaScript</c> on purpose, and the duplication is the
/// point rather than an oversight. That helper strips client meta-commands and whole-line comments
/// before returning, which is correct for a fixture provisioning a schema and wrong for a test asking
/// whether the shipped file runs — a directive is gone before such a test can see it. These suites need
/// the file exactly as it is packed, so they must not share a reader that edits it.
/// </remarks>
internal static class ShippedScript
{
	/// <summary>
	/// Returns the unaltered contents of a shipped script, located by its repository-relative path.
	/// </summary>
	/// <param name="repoRelativePath">Forward-slash path from the repository root.</param>
	/// <returns>The file's text, with nothing removed or substituted.</returns>
	public static string RawBytesOf(string repoRelativePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(repoRelativePath);

		var native = repoRelativePath.Replace('/', Path.DirectorySeparatorChar);

		for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
		{
			var candidate = Path.Combine(dir.FullName, native);

			if (File.Exists(candidate))
			{
				return File.ReadAllText(candidate);
			}
		}

		throw new FileNotFoundException(
			$"Shipped script '{repoRelativePath}' was not found walking up from "
			+ $"'{AppContext.BaseDirectory}'. The path is relative to the repository root.",
			repoRelativePath);
	}
	/// <summary>
	/// Returns a shipped Oracle script with its SQL*Plus client directives removed, for a test that
	/// executes the file through a driver.
	/// </summary>
	/// <param name="script">The script text, as shipped.</param>
	/// <returns>The same text with every WHENEVER line dropped.</returns>
	/// <remarks>
	/// A script that can REFUSE carries WHENEVER SQLERROR EXIT FAILURE so SQL*Plus exits non-zero on the
	/// refusal instead of reporting a declined migration as applied. Oracle offers no invocation flag for
	/// that, so it can only live in the file. SQL*Plus interprets it and never sends it; ODP.NET has no
	/// such notion and answers ORA-00900. Removing it here is the same driver adaptation these suites
	/// already make when they strip the trailing block terminator.
	/// </remarks>
	public static string WithoutClientDirectives(string script)
	{
		ArgumentNullException.ThrowIfNull(script);

		return string.Join(
			'\n',
			script.Split('\n')
				.Where(static line => !line.TrimStart().StartsWith("WHENEVER ", StringComparison.OrdinalIgnoreCase)));
	}
}
