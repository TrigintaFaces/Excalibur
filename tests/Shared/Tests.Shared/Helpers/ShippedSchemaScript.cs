// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Tests.Shared.Helpers;

/// <summary>
/// Reads the DDL scripts the packages actually ship, so an integration fixture can provision its schema
/// from them instead of restating one.
/// </summary>
/// <remarks>
/// <para>
/// A fixture that carries its own <c>CREATE TABLE</c> can diverge from the shipped script, and it diverges
/// silently in the permissive direction, which is the direction that hides: a fixture whose column is
/// nullable where the shipped script says <c>NOT NULL</c>, or whose key is narrower than the shipped one,
/// makes every arm running against it structurally unable to detect the violation it was written to catch.
/// The guarantee then reads as enforced while nothing enforces it. The same defect has been repaired once
/// per provider, which is what a watched invariant looks like.
/// </para>
/// <para>
/// A fixture that holds no schema cannot diverge from one. That is why this reads the file rather than
/// comparing against it, and why a missing script throws instead of falling back to an inline copy: a
/// product package that ships no DDL is a defect to be filed, never a licence to invent a schema whose
/// only author is a test.
/// </para>
/// </remarks>
public static class ShippedSchemaScript
{
	private static readonly Regex SetVar =
		new(@"^[ \t]*:setvar[ \t]+(\w+)[ \t]+""([^""]*)""[ \t]*\r?$\n?", RegexOptions.Multiline, TimeSpan.FromSeconds(5));

	// Client meta-commands. sqlcmd interprets ':'-prefixed lines and psql interprets '\'-prefixed
	// lines; neither tool sends them to the server, so a driver handed the file as written rejects
	// them -- SqlCommand with "Incorrect syntax near ':'", Npgsql with 42601 at "\". Removing them
	// is what makes a shipped script executable through a driver, which is the same thing this type
	// already did for :setvar and for whole-line comments.
	//
	// Matched by directive NAME rather than by "any line starting with ':' or '\'". No statement in
	// either dialect can begin with those characters, so the broad rule would not drop a statement --
	// but it WOULD drop a wrapped line inside a string literal that happens to start with one, and a
	// helper that silently removes content is worse than the loud failure it replaces. An unknown
	// directive is left in place to fail loudly rather than removed on a guess.
	//
	// :setvar is deliberately absent: it carries the values ReadSqlCmdBatches substitutes, so that
	// method parses it first and strips it itself.
	private static readonly Regex ClientDirective =
		new(@"^[ \t]*(?::(?:on[ \t]+error|r|connect|exit|quit|out|error|listvar|list|serverlist|help|reset|ed|perftrace|xml)\b"
			+ @"|\\(?:set|unset|echo|qecho|warn|if|elif|else|endif|timing|pset|connect|conninfo|encoding|include_relative|include|ir|i|gexec|gset|copy|password|x|c|q)\b"
			+ @"|WHENEVER[ \t]+(?:SQLERROR|OSERROR)\b)",
			RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

	private static readonly Regex SqlCmdVariable =
		new(@"\$\((\w+)\)", RegexOptions.None, TimeSpan.FromSeconds(5));

	// SQL*Plus sends a PL/SQL block when it reaches a line holding only '/'. The block's own
	// statements are semicolon-terminated and so is its END, so splitting an Oracle script on ';'
	// Where a PL/SQL block BEGINS inside a segment. Anchored to a line start so the words are not
	// matched inside a string literal or a trailing comment on a plain statement.
	private static readonly Regex PlSqlBlockStart =
		new(@"^[ \t]*(?:DECLARE|BEGIN|CREATE[ \t]+OR[ \t]+REPLACE)\b",
			RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

	// shreds every DECLARE...END block into fragments no driver can execute. The '/' terminator is
	// what separates "a block, sent whole" from "plain statements, sent one at a time".
	private static readonly Regex OracleBlockTerminator =
		new(@"^[ \t]*/[ \t]*\r?$", RegexOptions.Multiline, TimeSpan.FromSeconds(5));

	private static readonly Regex GoSeparator =
		new(@"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

	/// <summary>
	/// Reads a shipped script, with whole-line comments and client meta-command directives removed.
	/// </summary>
	/// <param name="repoRelativePath">
	/// The script's path from the repository root, for example
	/// <c>src/Excalibur/Excalibur.Outbox.Postgres/Scripts/001_CreateOutboxSchema.sql</c>.
	/// </param>
	/// <returns>The script text.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="repoRelativePath"/> is null or blank.</exception>
	/// <exception cref="FileNotFoundException">Thrown when no such script exists above the test binary.</exception>
	public static string Read(string repoRelativePath) => StripNonServerLines(File.ReadAllText(Resolve(repoRelativePath)));

	/// <summary>
	/// Reads several shipped scripts in order, so a fixture can apply a package's initial schema followed by
	/// its migrations exactly as a consumer would.
	/// </summary>
	/// <param name="repoRelativePaths">The scripts, in the order they must be applied.</param>
	/// <returns>Each script's text, in the order given.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="repoRelativePaths"/> is null.</exception>
	/// <exception cref="FileNotFoundException">Thrown when any script does not exist above the test binary.</exception>
	public static IReadOnlyList<string> ReadAll(params string[] repoRelativePaths)
	{
		ArgumentNullException.ThrowIfNull(repoRelativePaths);

		return [.. repoRelativePaths.Select(Read)];
	}

	/// <summary>
	/// Reads a shipped script and splits it into single statements, for drivers that reject a batch.
	/// </summary>
	/// <param name="repoRelativePath">The script's path from the repository root.</param>
	/// <returns>The non-empty statements, in file order, without their terminating semicolons.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="repoRelativePath"/> is null or blank.</exception>
	/// <exception cref="FileNotFoundException">Thrown when no such script exists above the test binary.</exception>
	public static IReadOnlyList<string> ReadStatements(string repoRelativePath) =>
		[.. Read(repoRelativePath)
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(static statement => statement.Length > 0)];

	/// <summary>
	/// Reads a shipped Oracle script and returns its executable units, ready for a plain connection.
	/// </summary>
	/// <param name="repoRelativePath">The script's path from the repository root.</param>
	/// <returns>The non-empty units, in file order: PL/SQL blocks whole, plain statements individually.</returns>
	/// <remarks>
	/// <para>
	/// ODP.NET executes one unit per command. A PL/SQL block must be sent WHOLE — its inner statements
	/// and its <c>END;</c> are semicolon-terminated, so splitting the file on <c>;</c> would shred it.
	/// SQL*Plus marks the end of a block with a line holding only <c>/</c>, and that is the boundary
	/// used here: text between block terminators is split on <c>;</c>, and a segment that is itself a
	/// block is returned intact.
	/// </para>
	/// <para>
	/// This exists so an Oracle fixture can provision from the script the package SHIPS rather than
	/// restating it. A fixture that restates the schema can drift permissively — a nullable column
	/// where the script says NOT NULL — and every arm running against it is then structurally unable
	/// to detect the divergence it exists to catch, while still reporting green.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="repoRelativePath"/> is null or blank.</exception>
	/// <exception cref="FileNotFoundException">Thrown when no such script exists above the test binary.</exception>
	public static IReadOnlyList<string> ReadOracleUnits(string repoRelativePath)
	{
		var units = new List<string>();

		foreach (var segment in OracleBlockTerminator.Split(Read(repoRelativePath)))
		{
			var trimmed = segment.Trim();
			if (trimmed.Length == 0)
			{
				continue;
			}

			// A '/' ends a block, so a segment can hold plain statements FOLLOWED by one block --
			// splitting the whole segment on ';' would shred that block. Find where the block starts
			// and treat the two halves differently.
			var blockStart = PlSqlBlockStart.Match(trimmed);
			var statements = blockStart.Success ? trimmed[..blockStart.Index] : trimmed;

			units.AddRange(
				statements.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
					.Where(static statement => statement.Length > 0));

			if (blockStart.Success)
			{
				units.Add(trimmed[blockStart.Index..].Trim());
			}
		}

		return units;
	}

	/// <summary>
	/// Reads a shipped SQLCMD script and returns its batches, ready to execute against a plain connection.
	/// </summary>
	/// <param name="repoRelativePath">The script's path from the repository root.</param>
	/// <param name="overrides">
	/// Values for the script's SQLCMD variables. Any variable the script declares with <c>:setvar</c> and
	/// this does not name keeps the shipped default, so a fixture states only what it deliberately changes.
	/// </param>
	/// <returns>The non-empty batches, in file order, split on <c>GO</c>.</returns>
	/// <remarks>
	/// The SQL Server packages ship SQLCMD templates: object names are <c>$(Variables)</c> with
	/// <c>:setvar</c> defaults, and batches are separated by <c>GO</c>. Neither is T-SQL, so a driver
	/// rejects the file as written. Substituting the script's own declared defaults is what lets a fixture
	/// run the shipped file rather than a retyped copy of it — and it means a change to a shipped default
	/// reaches the fixture instead of drifting away from it.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="repoRelativePath"/> is null or blank.</exception>
	/// <exception cref="FileNotFoundException">Thrown when no such script exists above the test binary.</exception>
	/// <exception cref="InvalidOperationException">Thrown when a <c>$(Variable)</c> has no declared default and no override.</exception>
	public static IReadOnlyList<string> ReadSqlCmdBatches(
		string repoRelativePath,
		IReadOnlyDictionary<string, string>? overrides = null)
	{
		var text = Read(repoRelativePath);
		var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (Match declaration in SetVar.Matches(text))
		{
			variables[declaration.Groups[1].Value] = declaration.Groups[2].Value;
		}

		if (overrides is not null)
		{
			foreach (var (name, value) in overrides)
			{
				variables[name] = value;
			}
		}

		// Drop the :setvar directives themselves — they are client syntax, not T-SQL.
		text = SetVar.Replace(text, string.Empty);

		text = SqlCmdVariable.Replace(text, match =>
		{
			var name = match.Groups[1].Value;
			return variables.TryGetValue(name, out var value)
				? value
				: throw new InvalidOperationException(
					$"'{repoRelativePath}' references SQLCMD variable $({name}), which the script does not "
					+ "declare with :setvar and the caller did not supply. Executing the script with the "
					+ "token unresolved would create objects under a literal '$(...)' name.");
		});

		return
		[
			.. GoSeparator
				.Split(text)
				.Select(static batch => batch.Trim())
				.Where(static batch => batch.Length > 0)
		];
	}

	/// <summary>
	/// Locates a shipped script by walking up from the test binary to the repository root.
	/// </summary>
	/// <param name="repoRelativePath">The script's path from the repository root.</param>
	/// <returns>The absolute path to the script.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="repoRelativePath"/> is null or blank.</exception>
	/// <exception cref="FileNotFoundException">Thrown when no such script exists above the test binary.</exception>
	public static string Resolve(string repoRelativePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(repoRelativePath);

		var native = repoRelativePath.Replace('/', Path.DirectorySeparatorChar);

		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			var candidate = Path.Combine(directory.FullName, native);
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		throw new FileNotFoundException(
			$"The shipped script '{repoRelativePath}' was not found by walking up from "
			+ $"'{AppContext.BaseDirectory}'. Fixtures provision their schema from the script the package "
			+ "ships and deliberately carry no copy of their own, so a missing script is a defect in the "
			+ "package rather than a reason to invent a schema here.",
			repoRelativePath);
	}

	private static string StripNonServerLines(string sql) =>
		string.Join('\n', sql
			.Split('\n')
			.Select(static line => line.TrimEnd('\r'))
			.Where(static line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal))
			.Where(static line => !ClientDirective.IsMatch(line)));
}
