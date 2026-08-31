// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;

using Excalibur.Outbox.SqlServer;

namespace Excalibur.Outbox.Tests.SqlServer;

/// <summary>
/// Binds the shape of the SQL Server outbox DDL the package ships to consumers.
/// </summary>
/// <remarks>
/// <para>
/// The script both CREATES a table when it is absent and UPGRADES it when it is present, and every table
/// name it touches is consumer-overridable through <see cref="SqlServerOutboxOptions"/>. Because every
/// block is guarded on whether its object already exists, a deployment that renamed a table but ran a
/// half-edited script gets neither behaviour: the create guard looks for a name that was not renamed, does
/// not find it, and creates a SECOND, EMPTY table beside the real one. The upgrade guard then finds that
/// empty table and alters it. The real table is never touched and nothing reports a problem.
/// </para>
/// <para>
/// That is worse than a silent no-op because it manufactures evidence of its own correctness — a later
/// audit asking "does the tenant column exist?" is answered YES, from the decoy. Row-level security is
/// installed by DDL that names tables, so a policy created against the default name attaches to the decoy
/// and succeeds while the table holding the rows carries no policy.
/// </para>
/// <para>
/// The script defends against that WITHOUT client meta-commands, and that portability is itself a
/// contract: the file is plain T-SQL that any client understanding the GO batch separator runs unchanged —
/// sqlcmd, SSMS, DbUp, Flyway, or a hand-rolled connection loop. A single <c>:setvar</c> or <c>$(...)</c>
/// substitution would make the file unrunnable everywhere except sqlcmd, so this suite proves there are
/// none and goes red the moment one is reintroduced.
/// </para>
/// <para>
/// The two properties the no-directive contract has to carry on its own are therefore: every object is
/// addressed through exactly one spelling, so the documented "replace EVERY occurrence in one pass" rename
/// is a well-defined single-token edit that cannot be half-done by accident; and the verification block at
/// the end RAISERRORs on any object the run did not create, so a rename that WAS half-done fails loudly
/// instead of silently building a table nobody reads.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ShippedSqlServerOutboxSchemaShould
{
	// The EmbeddedResource Link alias in the test project, not a path in the tree. The file on disk is
	// src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql; the item element renames
	// it on the way in so the resource is unambiguous among the per-provider scripts that share that leaf.
	private const string ScriptFileName = "001_CreateSqlServerOutboxSchema.sql";

	// The default object names. Each is a value of a SqlServerOutboxOptions.Tables /
	// SqlServerDeadLetterQueueOptions property, so each can differ in a consumer's deployment.
	private static readonly string[] DefaultObjectNames =
	[
		"OutboxMessages",
		"OutboxFence",
		"OutboxMessageTransports",
		"DeadLetterQueue",
	];

	private static readonly SqlServerOutboxOptions OutboxDefaults = new();

	private static readonly SqlServerDeadLetterQueueOptions DeadLetterDefaults = new();

	private static string Script { get; } = LoadShipped();

	// The executable half of the script: everything that is not a comment line. The header deliberately
	// names the defaults in prose and explains the rename procedure, so testing the raw file would fail on
	// its own documentation. Nothing else is filtered — in particular a reintroduced client directive stays
	// visible here, because a filter that swallowed it would hide the very regression this suite binds.
	private static string ExecutableScript { get; } = string.Join(
		'\n',
		Script.Split('\n')
			.Select(static line => line.TrimEnd('\r'))
			.Where(static line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));

	/// <summary>
	/// SAFETY, and the property that makes the documented rename procedure well-defined. Every executable
	/// reference to an object is the fully bracket-qualified <c>[schema].[table]</c> spelling — never a bare
	/// or unbracketed one. A single unqualified occurrence would survive a one-pass replace of the qualified
	/// form, which is precisely the half-rename that creates the decoy table.
	/// </summary>
	[Theory]
	[InlineData("OutboxMessages")]
	[InlineData("OutboxFence")]
	[InlineData("OutboxMessageTransports")]
	[InlineData("DeadLetterQueue")]
	public void AddressEveryObjectThroughOneBracketQualifiedSpelling(string defaultName)
	{
		// Occurrences that are part of a larger identifier are not references to the table: index,
		// constraint and foreign-key names embed the table name but are scoped to the object they hang off,
		// so a renamed deployment is not harmed by an index whose name reads oddly. The lookbehind excludes
		// both those (preceded by a word character) and the qualified form itself (preceded by '[').
		var unqualified = new Regex(
			@"(?<![\[\w])" + Regex.Escape(defaultName) + @"(?!\w)",
			RegexOptions.None,
			TimeSpan.FromSeconds(1));

		var strays = unqualified.Matches(ExecutableScript);

		strays.Count.ShouldBe(
			0,
			$"'{defaultName}' is addressed somewhere without its [schema].[table] brackets. The script "
			+ "documents renaming as replacing every occurrence of the qualified name in one pass, so an "
			+ "unqualified occurrence is left behind by that edit: the create guard then makes an empty "
			+ $"table under the default name and the upgrade guard alters it. Found {strays.Count} stray "
			+ $"reference(s) to '{defaultName}'.");

		var qualified = new Regex(
			@"\[(?<schema>[^\]]+)\]\.\[" + Regex.Escape(defaultName) + @"\]",
			RegexOptions.None,
			TimeSpan.FromSeconds(1));

		var schemas = qualified.Matches(ExecutableScript)
			.Select(static m => m.Groups["schema"].Value)
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		// LIVENESS for the arm above: with no qualified references at all, "no stray references" is
		// satisfied by a script that never names the object and therefore never creates it.
		schemas.ShouldHaveSingleItem(
			$"'{defaultName}' must be addressed, and always under one schema. Found: "
			+ $"[{string.Join(", ", schemas)}]");
	}

	/// <summary>
	/// SAFETY. The script performs no SQLCMD variable substitution. A <c>$(Name)</c> reference is a client
	/// meta-construct: sqlcmd expands it, and every other client sends it to the server verbatim, where it
	/// is a syntax error. One reference makes the shipped file unrunnable outside sqlcmd.
	/// </summary>
	[Fact]
	public void PerformNoSqlcmdVariableSubstitution()
	{
		var substitutions = new Regex(@"\$\([^)]*\)", RegexOptions.None, TimeSpan.FromSeconds(1))
			.Matches(Script)
			.Select(static m => m.Value)
			.ToArray();

		substitutions.ShouldBeEmpty(
			"the shipped script is plain T-SQL so that DbUp, Flyway, SSMS and a hand-rolled connection loop "
			+ "all run it unchanged. A $(...) substitution is expanded only by sqlcmd; every other client "
			+ "sends it to the server as-is and the batch fails to parse. Found: "
			+ string.Join(", ", substitutions));
	}

	/// <summary>
	/// SAFETY, per variable. None of the SQLCMD variables the script used to declare has come back — neither
	/// as a declaration nor as a reference. Locked one at a time because a reintroduction typically restores
	/// a single name, and a blanket sweep reports that as one anonymous failure.
	/// </summary>
	[Theory]
	[InlineData("OutboxSchema")]
	[InlineData("OutboxTable")]
	[InlineData("OutboxFenceTable")]
	[InlineData("OutboxTransportsTable")]
	[InlineData("DeadLetterSchema")]
	[InlineData("DeadLetterTable")]
	public void ReintroduceNoneOfTheRemovedSqlcmdVariables(string name)
	{
		Script.ShouldNotContain(
			$":setvar {name}",
			Case.Sensitive,
			customMessage: $"':setvar {name}' is a client meta-command, not T-SQL. Restoring it makes the "
				+ "shipped script fail to parse on every client except sqlcmd.");

		Script.ShouldNotContain(
			$"$({name})",
			Case.Sensitive,
			customMessage: $"'$({name})' is substituted only by sqlcmd. Every other client sends it to the "
				+ "server verbatim, where it is a syntax error.");
	}

	/// <summary>
	/// LIVENESS. Each name the script hard-codes is still the default the options type produces, so a
	/// consumer who has NOT renamed anything gets a schema the store can actually read. Bound against the
	/// live options objects rather than a copied literal: if an options default is changed and the script is
	/// not, the store addresses one table while the DDL creates another, and this goes red.
	/// </summary>
	[Theory]
	[InlineData("Tables.SchemaName", "dbo")]
	[InlineData("Tables.OutboxTableName", "OutboxMessages")]
	[InlineData("Tables.FenceTableName", "OutboxFence")]
	[InlineData("Tables.TransportsTableName", "OutboxMessageTransports")]
	[InlineData("DeadLetterQueue.SchemaName", "dbo")]
	[InlineData("DeadLetterQueue.TableName", "DeadLetterQueue")]
	public void HardCodeTheOptionsDefaultForEveryObjectName(string optionsMember, string expected)
	{
		var live = optionsMember switch
		{
			"Tables.SchemaName" => OutboxDefaults.Tables.SchemaName,
			"Tables.OutboxTableName" => OutboxDefaults.Tables.OutboxTableName,
			"Tables.FenceTableName" => OutboxDefaults.Tables.FenceTableName,
			"Tables.TransportsTableName" => OutboxDefaults.Tables.TransportsTableName,
			"DeadLetterQueue.SchemaName" => DeadLetterDefaults.SchemaName,
			"DeadLetterQueue.TableName" => DeadLetterDefaults.TableName,
			_ => throw new ArgumentOutOfRangeException(nameof(optionsMember), optionsMember, "unmapped"),
		};

		live.ShouldBe(
			expected,
			$"'{optionsMember}' no longer defaults to '{expected}'. The shipped DDL hard-codes the name, so "
			+ "changing the option default without editing the script leaves the store reading a table the "
			+ "script never creates.");

		ExecutableScript.ShouldContain(
			$"[{live}]",
			Case.Sensitive,
			customMessage: $"the script never names '{live}', so an unmodified deployment configured by "
				+ $"'{optionsMember}' has no object to address.");
	}

	/// <summary>
	/// LIVENESS, and the arm that answers the question directly. Performing the rename the script's header
	/// documents — replace every occurrence of a qualified name in one pass — leaves NO reference to the
	/// default name anywhere, so the create guard cannot create a second default-named table for the upgrade
	/// guard to find.
	/// </summary>
	[Fact]
	public void CarryARenamedTableToEveryReference()
	{
		var renamed = ExecutableScript
			.Replace("[dbo].[OutboxMessages]", "[app].[MyOutbox]", StringComparison.Ordinal)
			.Replace("[dbo].[OutboxFence]", "[app].[MyFence]", StringComparison.Ordinal)
			.Replace("[dbo].[OutboxMessageTransports]", "[app].[MyTransports]", StringComparison.Ordinal)
			.Replace("[dbo].[DeadLetterQueue]", "[app].[MyDeadLetters]", StringComparison.Ordinal);

		foreach (var defaultName in DefaultObjectNames)
		{
			var addressed = new Regex(
				@"(?<!\w)" + Regex.Escape(defaultName) + @"(?!\w)",
				RegexOptions.None,
				TimeSpan.FromSeconds(1));

			addressed.IsMatch(renamed).ShouldBeFalse(
				$"after renaming every object in one pass, '{defaultName}' is still addressed somewhere. "
				+ "That reference is the phantom: the create guard makes an empty table under the default "
				+ "name and the upgrade guard alters it, leaving the consumer's real table untouched.");
		}

		// The substitution is only meaningful if it actually replaced something.
		renamed.ShouldContain("[app].[MyOutbox]", Case.Sensitive);
		renamed.ShouldContain("[app].[MyFence]", Case.Sensitive);
		renamed.ShouldContain("[app].[MyTransports]", Case.Sensitive);
		renamed.ShouldContain("[app].[MyDeadLetters]", Case.Sensitive);
	}

	/// <summary>
	/// SAFETY, and the arm that keeps the script runnable by anything. It carries no executable client
	/// meta-command of any dialect — no sqlcmd <c>:setvar</c> / <c>:r</c> / <c>:on error exit</c>, no psql
	/// <c>\set</c> / <c>\i</c>. GO is not one of these: it is a batch separator every SQL Server client
	/// understands, and the upgrade blocks add a column and then read it, which the server rejects inside a
	/// single batch.
	/// </summary>
	[Fact]
	public void CarryNoExecutableClientDirective()
	{
		var directives = new Regex(
			@"(?m)^[ \t]*(?<directive>[:\\]\w[^\r\n]*)",
			RegexOptions.None,
			TimeSpan.FromSeconds(1))
			.Matches(Script)
			.Select(static m => m.Groups["directive"].Value.Trim())
			.ToArray();

		directives.ShouldBeEmpty(
			"a client meta-command is not T-SQL: only the client that owns the dialect strips it, and every "
			+ "other one sends it to the server, where the batch fails to parse. The shipped script is run "
			+ "by DbUp, Flyway, SSMS and hand-rolled connection loops as well as sqlcmd. Found: "
			+ string.Join(" | ", directives));
	}

	/// <summary>
	/// SAFETY. The verification block is the backstop that replaces what the removed variables used to
	/// provide: with names written out, a rename can be half-done, so the run ends by asserting every object
	/// it named actually exists and RAISERRORing if one does not. Without it a half-renamed run is silent,
	/// and the failure surfaces at the first drain instead of at deployment.
	/// </summary>
	[Fact]
	public void RaiseOnAnyObjectTheRunDidNotCreate()
	{
		foreach (var defaultName in DefaultObjectNames)
		{
			var existenceCheck = new Regex(
				@"OBJECT_ID\(\s*N'\[[^\]]+\]\.\[" + Regex.Escape(defaultName) + @"\]'\s*,\s*N'U'\s*\)\s+IS NULL",
				RegexOptions.None,
				TimeSpan.FromSeconds(1));

			existenceCheck.IsMatch(ExecutableScript).ShouldBeTrue(
				$"the verification block never checks whether '{defaultName}' was created, so a run that "
				+ "silently built a decoy under a half-applied rename still reports success.");
		}

		// The check is only worth anything if a missing object aborts the run. Severity must be at least 16
		// for the client to see an error rather than an informational message.
		// Lazy across everything between the open paren and the argument triple: the message is a free-text
		// literal that may contain any punctuation, so the match has to be anchored on the shape of the
		// severity/state arguments rather than on what the message does not contain.
		var raise = new Regex(
			@"RAISERROR\s*\([\s\S]*?,\s*(?<severity>\d+)\s*,\s*\d+\s*[,)]",
			RegexOptions.None,
			TimeSpan.FromSeconds(1))
			.Match(ExecutableScript);

		raise.Success.ShouldBeTrue(
			"the verification block collects the missing objects but never raises, so the run reports "
			+ "success while the outbox has no table to drain.");

		int.Parse(raise.Groups["severity"].Value, System.Globalization.CultureInfo.InvariantCulture)
			.ShouldBeGreaterThanOrEqualTo(
				16,
				"a severity below 16 is informational: the client prints it and carries on, so a deployment "
				+ "pipeline treats the incomplete run as a success.");
	}

	private static string LoadShipped()
	{
		var assembly = Assembly.GetExecutingAssembly();

		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(ScriptFileName, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped script '{ScriptFileName}' is not embedded in {assembly.GetName().Name}. It is "
				+ "linked in by the test project's EmbeddedResource item; if that item was removed, this "
				+ "suite would silently stop looking at the script a consumer actually runs.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}
}
