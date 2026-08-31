// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

namespace Boundary.Tests;

/// <summary>
/// Structural guard: every <c>MERGE</c> emitted by a SQL Server provider takes BOTH lock hints on its
/// target — <c>UPDLOCK</c> and <c>HOLDLOCK</c>. Either hint alone is a defect in one direction.
/// </summary>
/// <remarks>
/// <para>
/// <b>This guard checks statement SHAPE, not runtime behaviour.</b> It reads the emitted SQL and asserts the
/// hints are present. It does not execute a MERGE, does not open a connection, and cannot observe a lock, a
/// block, or a deadlock. A pass means the text is right; it is not evidence that the engine serialized
/// anything.
/// </para>
/// <para>
/// <b>Why not a behavioural arm.</b> The property at stake is that concurrent upserts of one key serialize by
/// BLOCKING rather than by one of them being killed as a deadlock victim (error 1205). That is engine
/// behaviour under contention, and a concurrent arm has been found not to discriminate on it. That finding
/// is not this guard's own: it is recorded, with its experiment shape, against the equivalent saga upsert —
/// see <c>SagaUpsertLockHintShould</c> in the unit tier and
/// <c>SqlServerSagaStoreIntegrationShould.ResolveConcurrentCreatesOfOneSagaKey_AsExactlyOneWinner</c> in the
/// integration tier (real SQL Server, never skipped). Both record that with the shared-lock-only form of the
/// upsert restored, the race produced zero deadlocks over 200 attempts and again over 7,200 — that is, the
/// concurrent arm is GREEN on the broken code. The conversion window inside a single autocommit MERGE is too
/// narrow to hit reliably from one client process; production load finds it, a test loop does not.
/// </para>
/// <para>
/// <b>This guard measured none of that itself</b> and asserts no behavioural result of its own. It inherits
/// the conclusion from those two arms, which are the primary source and where the numbers should be checked
/// before they are relied on. If a concurrent arm is ever built for these providers, it has to establish
/// that it CAN produce a deadlock on the unhinted form before its green on the hinted form means anything —
/// otherwise it reproduces exactly the tautology this guard exists to avoid.
/// </para>
/// <para>
/// <b>The reasoning being locked.</b>
/// <list type="bullet">
/// <item><description><c>HOLDLOCK</c> alone — the MERGE matches under a SHARED range lock and must convert it
/// to exclusive for the write. Two sessions upserting the same key each hold S and each wait for the other to
/// drop it; the engine breaks the cycle by killing one. On the inbox that is a lost or retried exactly-once
/// state transition under exactly the concurrent duplicate load the inbox exists to absorb; on the outbox
/// fence, or the CDC checkpoint, it is a leader failing to advance its own high-water.</description></item>
/// <item><description><c>UPDLOCK</c> alone — no range is held, so the phantom-insert race reopens: two
/// sessions both evaluate <c>WHEN NOT MATCHED</c> and both INSERT.</description></item>
/// <item><description>Both — an UPDATE lock on the read (not mutually compatible, so the second session
/// blocks briefly) AND the range held (so the phantom stays closed).</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Scope: every <c>*.SqlServer</c> package under <c>src/</c>, discovered by directory name rather than
/// enumerated.</b> A new SQL Server provider is therefore covered the day it is added, without this file
/// being edited — a forgotten sibling goes RED instead of silently escaping the sweep. Oracle, Postgres and
/// SQLite providers also emit MERGE and are correctly outside the scan: <c>UPDLOCK</c>/<c>HOLDLOCK</c> are
/// T-SQL table hints with no equivalent there, and their statements carry their own guards.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class SqlServerMergeLockHintGuardTests
{
	/// <summary>Matches the opening line of a MERGE statement, not a prose mention of the word.</summary>
	private static readonly Regex MergeStatementLine = new(@"^\s*MERGE\b", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

	/// <summary>
	/// The target clause may wrap: at least one provider writes <c>MERGE</c> and its target on separate
	/// lines, which is how that statement escaped an earlier single-line sweep. The hint check therefore runs
	/// over the text from <c>MERGE</c> up to the <c>USING</c> that ends the target clause, not over one line.
	/// </summary>
	private const int MaxTargetClauseLines = 6;

	/// <summary>
	/// The population as measured when this guard was widened. Asserted as a FLOOR so the guard cannot pass
	/// vacuously: a scan that silently matched nothing (wrong root, renamed provider, MERGE replaced by a
	/// hand-rolled read-then-write) fails here instead of reporting a clean sweep over an empty set.
	/// </summary>
	private const int KnownMergeStatementCount = 21;

	[Fact]
	public void EveryMergeInASqlServerProvider_TakesBothLockHints()
	{
		var repoRoot = TestHelpers.GetRepositoryRoot();
		var sourceRoot = Path.Combine(repoRoot, "src");
		Directory.Exists(sourceRoot).ShouldBeTrue($"Expected the source root at '{sourceRoot}'.");

		var providerDirectories = Directory
			.EnumerateDirectories(sourceRoot, "*.SqlServer", SearchOption.AllDirectories)
			.Where(path => !IsGeneratedArtifactPath(path))
			.ToList();

		providerDirectories.ShouldNotBeEmpty(
			$"no '*.SqlServer' provider directories were found under '{sourceRoot}'. The scan found nothing to "
			+ "check, which reads identically to a clean pass.");

		var statements = providerDirectories
			.SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
			.Where(path => !IsGeneratedArtifactPath(path))
			.SelectMany(ReadMergeStatements)
			.ToList();

		statements.Count.ShouldBeGreaterThanOrEqualTo(
			KnownMergeStatementCount,
			$"the guard found only {statements.Count} MERGE statements across the SQL Server providers; "
			+ $"{KnownMergeStatementCount} were present when it was written. A shrinking population means the scan "
			+ "stopped finding what it is meant to check, which reads identically to a clean pass. Confirm the "
			+ "statements really are gone before lowering this floor.");

		var unhinted = statements
			.Where(entry => !HasBothHints(entry.TargetClause))
			.Select(entry => $"{entry.Path}:{entry.Number}: {entry.TargetClause}")
			.ToList();

		unhinted.ShouldBeEmpty(
			"STATEMENT-SHAPE CHECK (this guard reads the emitted SQL; it does not execute a MERGE and cannot "
			+ "observe a lock or a deadlock at runtime). Every MERGE in a SQL Server provider must take both "
			+ "UPDLOCK and HOLDLOCK on its target. HOLDLOCK alone reads under a SHARED lock and converts to "
			+ "exclusive for the write, which is the conversion-deadlock shape under concurrent upsert of one "
			+ "key; UPDLOCK alone stops holding the range and reopens the phantom-insert race. Offending "
			+ "statements:" + Environment.NewLine + string.Join(Environment.NewLine, unhinted));
	}

	/// <summary>Both hints must appear, in any order, anywhere in the target clause.</summary>
	private static bool HasBothHints(string targetClause) =>
		targetClause.Contains("UPDLOCK", StringComparison.Ordinal)
		&& targetClause.Contains("HOLDLOCK", StringComparison.Ordinal);

	private static IEnumerable<(string Path, int Number, string TargetClause)> ReadMergeStatements(string path)
	{
		var lines = File.ReadAllLines(path);

		for (var i = 0; i < lines.Length; i++)
		{
			if (!MergeStatementLine.IsMatch(lines[i]))
			{
				continue;
			}

			// Accumulate up to and including the line that opens USING — that span is the target clause,
			// and it is where the hints must appear.
			var clause = new List<string>();
			for (var j = i; j < lines.Length && j < i + MaxTargetClauseLines; j++)
			{
				clause.Add(lines[j].Trim());
				if (lines[j].Contains("USING", StringComparison.Ordinal))
				{
					break;
				}
			}

			yield return (path, i + 1, string.Join(' ', clause));
		}
	}

	private static bool IsGeneratedArtifactPath(string path)
	{
		var normalized = path.Replace('\\', '/');
		return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
			   || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
	}
}
