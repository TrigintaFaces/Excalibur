// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// The shared contract every <see cref="IErasureRetentionDeclarationLedger"/> owes, derived once per real
/// provider.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this record exists at all.</b> A retention declaration — the statement that an aggregate type
/// must survive an erasure because the law requires the record kept — lives in the consumer's CODE, so
/// each instance holds only its own view and nothing compares one view against another. This ledger is
/// the deployment-wide record that lets an instance discover it is behind and withhold a destruction it
/// cannot perform correctly. Every arm below is a property that mechanism depends on.
/// </para>
/// <para>
/// <b>THE TWO PROVIDERS DISAGREE IN MECHANISM AND MUST AGREE IN BEHAVIOUR, which is the whole reason for
/// a shared kit rather than two hand-written suites.</b> PostgreSQL has a single-statement
/// insert-if-absent; SQL Server has none, so its primary key is the arbiter and the duplicate it raises
/// is caught per pair. Those are different enough that hand-duplicated arms would drift, and the drift
/// would be silent.
/// </para>
/// <para>
/// <b>NO IN-MEMORY DERIVER, deliberately, and the contract says why.</b> An in-memory deployment is one
/// process, so no other process can hold a view it is behind and there is nothing for a comparison to be
/// against. A satisfiable record there would report a staleness check that could never fail, which is
/// worse than having none.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class ErasureRetentionDeclarationLedgerConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates the ledger under test, over a real database this suite has provisioned.
	/// </summary>
	/// <returns>The ledger to exercise.</returns>
	protected abstract IErasureRetentionDeclarationLedger CreateLedger();

	/// <summary>
	/// Creates the withdrawal authority over the same database as <see cref="CreateLedger"/>.
	/// </summary>
	/// <returns>The withdrawal authority to exercise.</returns>
	protected abstract IErasureRetentionWithdrawalAuthority CreateWithdrawalAuthority();

	/// <summary>
	/// Verifies that recording the same declaration twice leaves one record and raises nothing.
	/// </summary>
	/// <remarks>
	/// A lost race is a SUCCESS, not an error to surface. Every instance but the first to start finds its
	/// declarations already recorded, so an implementation that surfaced the duplicate would turn the
	/// specified no-op into a startup failure on a correctly configured deployment.
	/// </remarks>
	public virtual async Task RecordDeclaredRetentionsAsync_RecordingTheSamePairTwice_ShouldBeANoOp()
	{
		var ledger = CreateLedger();
		var tenant = NewTenant();
		var pair = new DeclaredRetentionKey(tenant, "SalesRecord");

		await ledger.RecordDeclaredRetentionsAsync([pair], CancellationToken.None).ConfigureAwait(false);
		await ledger.RecordDeclaredRetentionsAsync([pair], CancellationToken.None).ConfigureAwait(false);

		var recorded = await ledger.ListDeclaredRetentionsAsync(tenant, CancellationToken.None)
			.ConfigureAwait(false);

		if (recorded.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"Recording one pair twice must leave exactly one record; found {recorded.Count}. An "
				+ "implementation that inserts a second row has made the record a multiset, and the "
				+ "staleness comparison counts declarations.");
		}
	}

	/// <summary>
	/// Verifies that a second advance containing an ALREADY-RECORDED pair still records the new ones.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>THE LOAD-BEARING ARM, and the one a batched implementation fails.</b> The normal case for every
	/// instance but the first is an advance whose pairs are MOSTLY already recorded. An implementation
	/// that sends the pairs as one command, or that stops at the first duplicate, silently skips every
	/// declaration after the collision — and a silently skipped advance is a declaration this deployment
	/// never records, which is the exact state the ledger exists to detect.
	/// </para>
	/// <para>
	/// The already-recorded pair is placed FIRST on purpose, so an implementation that aborts on the first
	/// duplicate loses the new one rather than getting away with it.
	/// </para>
	/// <para>
	/// Mutant: batch the pairs into a single command on the SQL Server ledger, or move the duplicate catch
	/// outside the per-pair loop — this arm goes RED and the idempotence arm above stays green.
	/// </para>
	/// </remarks>
	public virtual async Task RecordDeclaredRetentionsAsync_WithAnAlreadyRecordedPairFirst_ShouldStillRecordTheRest()
	{
		var ledger = CreateLedger();
		var tenant = NewTenant();
		var alreadyRecorded = new DeclaredRetentionKey(tenant, "SalesRecord");
		var newlyDeclared = new DeclaredRetentionKey(tenant, "TaxFiling");
		var alsoNew = new DeclaredRetentionKey(tenant, "AuditTrail");

		await ledger.RecordDeclaredRetentionsAsync([alreadyRecorded], CancellationToken.None)
			.ConfigureAwait(false);

		// The deploy that adds two declarations: this instance declares all three, one of which the
		// deployment already has.
		await ledger.RecordDeclaredRetentionsAsync(
			[alreadyRecorded, newlyDeclared, alsoNew], CancellationToken.None).ConfigureAwait(false);

		var recorded = await ledger.ListDeclaredRetentionsAsync(tenant, CancellationToken.None)
			.ConfigureAwait(false);
		var types = recorded.Select(r => r.Declaration.AggregateType).ToHashSet(StringComparer.Ordinal);

		foreach (var expected in new[] { "SalesRecord", "TaxFiling", "AuditTrail" })
		{
			if (!types.Contains(expected))
			{
				throw new TestFixtureAssertionException(
					$"'{expected}' is missing after an advance whose FIRST pair was already recorded. An "
					+ "advance must record every pair the deployment does not have, independently of the "
					+ "ones it does — an instance whose declaration is silently skipped is exactly the "
					+ "stale instance this ledger exists to detect, and it would read as current.");
			}
		}
	}

	/// <summary>
	/// Verifies that one tenant's declarations never appear in another tenant's read.
	/// </summary>
	/// <remarks>
	/// The tenant is the leading column of the primary key, so this is the key addressing the rows it
	/// owns rather than a predicate narrowing a wider set. The LIVENESS half is asserted first: a read
	/// that returned nothing to anybody would satisfy the isolation assertion and serve no caller.
	/// </remarks>
	public virtual async Task ListDeclaredRetentionsAsync_ShouldReturnOnlyTheRequestedTenantsDeclarations()
	{
		var ledger = CreateLedger();
		var mine = NewTenant();
		var theirs = NewTenant();

		await ledger.RecordDeclaredRetentionsAsync(
			[new DeclaredRetentionKey(mine, "SalesRecord")], CancellationToken.None).ConfigureAwait(false);
		await ledger.RecordDeclaredRetentionsAsync(
			[new DeclaredRetentionKey(theirs, "TaxFiling")], CancellationToken.None).ConfigureAwait(false);

		var forMine = await ledger.ListDeclaredRetentionsAsync(mine, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS first.
		if (!forMine.Any(r => string.Equals(r.Declaration.AggregateType, "SalesRecord", StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				"A tenant's own recorded declaration must be returned to it. A read that returns nothing "
				+ "would make every instance look stale and withhold every erasure in the deployment.");
		}

		// SAFETY.
		if (forMine.Any(r => string.Equals(r.Declaration.AggregateType, "TaxFiling", StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				"Another tenant's declaration reached this tenant's read. Only the requesting tenant's "
				+ "declarations decide whether a handle is spared, so a foreign one can only ever withhold "
				+ "an erasure that would have been performed correctly.");
		}
	}

	/// <summary>
	/// Verifies that a withdrawal TOMBSTONES the declaration — still returned, flagged withdrawn.
	/// </summary>
	/// <remarks>
	/// A caller that cannot see a tombstone cannot detect the mirror case: an instance whose own code
	/// still declares a retention the deployment has withdrawn, which will spare rows another actor is
	/// entitled to destroy. Deleting the row instead would make that case inexpressible.
	/// </remarks>
	public virtual async Task WithdrawDeclaredRetentionAsync_ShouldTombstoneRatherThanDelete()
	{
		var ledger = CreateLedger();
		var authority = CreateWithdrawalAuthority();
		var tenant = NewTenant();
		var pair = new DeclaredRetentionKey(tenant, "SalesRecord");

		await ledger.RecordDeclaredRetentionsAsync([pair], CancellationToken.None).ConfigureAwait(false);

		var outcome = await authority.WithdrawDeclaredRetentionAsync(
			pair, "operator-1", "the statutory period elapsed", CancellationToken.None).ConfigureAwait(false);

		if (outcome != RetentionWithdrawalOutcome.Withdrawn)
		{
			throw new TestFixtureAssertionException(
				$"Withdrawing a recorded, live declaration must report Withdrawn; reported {outcome}.");
		}

		var recorded = await ledger.ListDeclaredRetentionsAsync(tenant, CancellationToken.None)
			.ConfigureAwait(false);
		var row = recorded.FirstOrDefault(
			r => string.Equals(r.Declaration.AggregateType, "SalesRecord", StringComparison.Ordinal));

		if (row.Declaration.AggregateType is null)
		{
			throw new TestFixtureAssertionException(
				"A withdrawn declaration must still be RETURNED. A read that hides the tombstone makes "
				+ "'this deployment withdrew the obligation' and 'this deployment never recorded it' the "
				+ "same observation, and the mirror case undetectable.");
		}

		if (!row.IsWithdrawn)
		{
			throw new TestFixtureAssertionException(
				"A withdrawn declaration must be returned FLAGGED as withdrawn. Returning it as live "
				+ "would make a withdrawal invisible, so an instance would keep sparing rows the "
				+ "deployment has released.");
		}
	}

	/// <summary>
	/// Verifies that a repeat withdrawal changes nothing and says so, and that an unrecorded pair is a
	/// third, distinguishable answer.
	/// </summary>
	/// <remarks>
	/// Three outcomes rather than two, because withdrawing something already withdrawn and withdrawing
	/// something this deployment never recorded are different facts, and the second is the one an auditor
	/// asks about. A repeat must also PRESERVE the first withdrawal's recorded facts — the record names
	/// who decided, not who called last.
	/// </remarks>
	public virtual async Task WithdrawDeclaredRetentionAsync_ShouldDistinguishRepeatFromNeverDeclared()
	{
		var ledger = CreateLedger();
		var authority = CreateWithdrawalAuthority();
		var tenant = NewTenant();
		var pair = new DeclaredRetentionKey(tenant, "SalesRecord");

		await ledger.RecordDeclaredRetentionsAsync([pair], CancellationToken.None).ConfigureAwait(false);
		_ = await authority.WithdrawDeclaredRetentionAsync(
			pair, "operator-1", "first", CancellationToken.None).ConfigureAwait(false);

		var repeat = await authority.WithdrawDeclaredRetentionAsync(
			pair, "operator-2", "second", CancellationToken.None).ConfigureAwait(false);

		if (repeat != RetentionWithdrawalOutcome.AlreadyWithdrawn)
		{
			throw new TestFixtureAssertionException(
				$"Withdrawing an already-withdrawn declaration must report AlreadyWithdrawn; reported "
				+ $"{repeat}. Reporting Withdrawn again would also mean the second call overwrote the "
				+ "first withdrawal's actor and reason, and the record an auditor reads would name "
				+ "whoever withdrew it last rather than who decided.");
		}

		var never = await authority.WithdrawDeclaredRetentionAsync(
			new DeclaredRetentionKey(tenant, "NeverDeclaredType"),
			"operator-1",
			"nothing to withdraw",
			CancellationToken.None).ConfigureAwait(false);

		if (never != RetentionWithdrawalOutcome.NotDeclared)
		{
			throw new TestFixtureAssertionException(
				$"Withdrawing a pair this deployment never recorded must report NotDeclared; reported "
				+ $"{never}. Collapsing it onto AlreadyWithdrawn would record that an obligation ended "
				+ "when none was ever declared.");
		}
	}

	/// <summary>
	/// Verifies that an advance cannot RESURRECT a withdrawn declaration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is why the advance path contains no update.</b> The tombstone OCCUPIES the key, so an
	/// instance whose code still declares a withdrawn retention re-runs the advance and changes nothing.
	/// An implementation that used an upsert would clear the tombstone and the withdrawal would silently
	/// un-happen — on a schedule, because every such instance advances before its first erasure.
	/// </para>
	/// <para>
	/// Mutant: change either provider's advance to an upsert, or to clear the withdrawal columns — this
	/// arm goes RED and every other arm here stays green.
	/// </para>
	/// </remarks>
	public virtual async Task RecordDeclaredRetentionsAsync_ShouldNotResurrectAWithdrawnDeclaration()
	{
		var ledger = CreateLedger();
		var authority = CreateWithdrawalAuthority();
		var tenant = NewTenant();
		var pair = new DeclaredRetentionKey(tenant, "SalesRecord");

		await ledger.RecordDeclaredRetentionsAsync([pair], CancellationToken.None).ConfigureAwait(false);
		_ = await authority.WithdrawDeclaredRetentionAsync(
			pair, "operator-1", "the statutory period elapsed", CancellationToken.None).ConfigureAwait(false);

		// An instance whose code still declares it advances again, as it does before its first erasure.
		await ledger.RecordDeclaredRetentionsAsync([pair], CancellationToken.None).ConfigureAwait(false);

		var recorded = await ledger.ListDeclaredRetentionsAsync(tenant, CancellationToken.None)
			.ConfigureAwait(false);
		var row = recorded.FirstOrDefault(
			r => string.Equals(r.Declaration.AggregateType, "SalesRecord", StringComparison.Ordinal));

		if (!row.IsWithdrawn)
		{
			throw new TestFixtureAssertionException(
				"An advance resurrected a WITHDRAWN declaration. The advance path must contain no update: "
				+ "the tombstone occupies the key precisely so that an instance still declaring the "
				+ "retention cannot undo the withdrawal, and every such instance advances on every start.");
		}
	}

	/// <summary>
	/// Verifies that declarations differing only in case are DISTINCT records.
	/// </summary>
	/// <remarks>
	/// The framework compares both key terms ordinally, so the record must too. On a database whose
	/// default collation folds case, two distinct aggregate types would collapse onto one row and the
	/// comparison would report a declaration as recorded when the deployment had recorded a different
	/// one — a check that passes over a false premise.
	/// </remarks>
	public virtual async Task RecordDeclaredRetentionsAsync_ShouldTreatTypesDifferingOnlyInCaseAsDistinct()
	{
		var ledger = CreateLedger();
		var tenant = NewTenant();

		await ledger.RecordDeclaredRetentionsAsync(
			[new DeclaredRetentionKey(tenant, "SalesRecord"), new DeclaredRetentionKey(tenant, "salesrecord")],
			CancellationToken.None).ConfigureAwait(false);

		var recorded = await ledger.ListDeclaredRetentionsAsync(tenant, CancellationToken.None)
			.ConfigureAwait(false);

		if (recorded.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Two aggregate types differing only in case must be two records; found {recorded.Count}. "
				+ "A folding collation makes them one, and the staleness comparison then reports a "
				+ "declaration as recorded when the deployment recorded a different type.");
		}
	}

	/// <summary>
	/// Verifies that an empty advance is accepted and records nothing.
	/// </summary>
	/// <remarks>
	/// A deployment that declares no retentions is the common case, and it must not be a special path a
	/// provider gets wrong — an advance of nothing is a no-op, never an error and never a row.
	/// </remarks>
	public virtual async Task RecordDeclaredRetentionsAsync_WithNoDeclarations_ShouldRecordNothing()
	{
		var ledger = CreateLedger();
		var tenant = NewTenant();

		await ledger.RecordDeclaredRetentionsAsync([], CancellationToken.None).ConfigureAwait(false);

		var recorded = await ledger.ListDeclaredRetentionsAsync(tenant, CancellationToken.None)
			.ConfigureAwait(false);

		if (recorded.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"An advance with no declarations must record nothing; found {recorded.Count} record(s).");
		}
	}

	/// <summary>
	/// Verifies that an advance which cannot record EVERY pair records the ones it can AND REPORTS
	/// FAILURE, so a caller can never latch on a prefix.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A PREFIX IS NOT A WEAKER SUCCESS -- it is a hole with no tell.</b> The advance does not protect
	/// the advancing instance; it protects OTHER instances FROM it. So an unrecorded pair is a pair for
	/// which no stale instance can ever detect its own staleness, and nothing anywhere reports that. The
	/// caller latches on success, so an implementation that returned normally after a partial advance
	/// would make that protection silently absent for the life of the process.
	/// </para>
	/// <para>
	/// <b>AND IT MUST STILL RECORD WHAT IT CAN.</b> Every pair recorded is protection that now exists, so
	/// aborting at the first unrecordable pair throws away recordings that could have been made. The
	/// unrecordable pair is placed in the MIDDLE on purpose: an implementation that stops there loses the
	/// pair after it, and this arm fails.
	/// </para>
	/// <para>
	/// The unrecordable pair is a blank aggregate type, which every provider's own table refuses by a
	/// check constraint -- a substrate refusal rather than a fault this kit simulates, which is why this
	/// arm belongs against a real engine.
	/// </para>
	/// </remarks>
	public virtual async Task RecordDeclaredRetentionsAsync_WhenOnePairCannotBeRecorded_ShouldRecordTheRestAndFail()
	{
		var ledger = CreateLedger();
		var tenant = NewTenant();

		var before = new DeclaredRetentionKey(tenant, "SalesRecord");
		var unrecordable = new DeclaredRetentionKey(tenant, "   ");
		var after = new DeclaredRetentionKey(tenant, "TaxFiling");

		var reportedFailure = false;
		try
		{
			await ledger.RecordDeclaredRetentionsAsync(
				[before, unrecordable, after], CancellationToken.None).ConfigureAwait(false);
		}
#pragma warning disable CA1031 // Any failure is the point; the arm asserts that one was reported at all.
		catch (Exception)
#pragma warning restore CA1031
		{
			reportedFailure = true;
		}

		if (!reportedFailure)
		{
			throw new TestFixtureAssertionException(
				"An advance that could not record every declaration RETURNED NORMALLY. The caller latches "
				+ "on success, so this makes a prefix indistinguishable from a complete advance -- and every "
				+ "pair that never reached the record is one no stale instance can use to detect itself.");
		}

		var recorded = await ledger.ListDeclaredRetentionsAsync(tenant, CancellationToken.None)
			.ConfigureAwait(false);
		var types = recorded.Select(r => r.Declaration.AggregateType).ToHashSet(StringComparer.Ordinal);

		foreach (var expected in new[] { "SalesRecord", "TaxFiling" })
		{
			if (!types.Contains(expected))
			{
				throw new TestFixtureAssertionException(
					$"'{expected}' was not recorded, although it could have been. An advance must attempt "
					+ "EVERY pair: each one recorded is protection that now exists, and stopping at the "
					+ "first unrecordable pair discards the ones after it for no benefit.");
			}
		}
	}

	/// <summary>
	/// A tenant nothing else in this suite uses, so arms sharing one database cannot answer each other's
	/// predicates.
	/// </summary>
	/// <returns>A fresh tenant identifier.</returns>
	private static string NewTenant() => $"t-{Guid.NewGuid():N}"[..32];
}
