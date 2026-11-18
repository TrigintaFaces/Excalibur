// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the predicate that decides whether this instance may destroy data a deploy may have declared
/// retained, and the latch that lets other instances make the same decision about this one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these are unit arms and the ledger's own arms are not.</b> The provider arms bind SUBSTRATE
/// semantics — whether an insert-if-absent is atomic, whether a tombstone survives an advance — and need
/// real engines. What these bind is the PREDICATE and the LATCH, which are in-process logic over two
/// sets, so a hand-written record drives them exactly and instantly. The one thing a hand-written record
/// must not be used for is claiming the providers agree; that is what the real-engine arms are for.
/// </para>
/// <para>
/// <b>The fixture states here are the two a suite that advances fully from an empty record cannot
/// reach</b>, and without them the suite is green against both the sound predicate and the unsound one.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AStaleRetentionViewWithholdsDestructionShould
{
	private const string SalesRecord = "SalesRecord";
	private const string TaxFiling = "TaxFiling";
	private const string AuditTrail = "AuditTrail";

	// THE INCOMPARABLE STATE, and the arm that kills the predicate this design was first given. The
	// record holds {SalesRecord, AuditTrail}; this instance declares {SalesRecord, TaxFiling}. It is
	// simultaneously BEHIND on AuditTrail and AHEAD on TaxFiling.
	//
	// Reachable with no concurrency at all: a deploy dropped AuditTrail from the code and added TaxFiling,
	// and its advance recorded TaxFiling only partially -- which SQL Server's per-pair advance can leave.
	//
	// "Refuse iff local is a STRICT SUBSET of the record" is FALSE here, because TaxFiling is not in the
	// record -- so that predicate PASSES and the erasure destroys AuditTrail's key, which another version
	// declared retained. Containment refuses.
	//
	// Mutant: replace the containment check with a strict-subset check -- this arm goes RED and the two
	// below stay green.
	[Fact]
	public async Task Withhold_when_the_record_holds_a_declaration_this_instance_lacks_even_while_it_holds_one_the_record_lacks()
	{
		var gate = new ErasureRetentionStalenessGate(
			RegistryDeclaring(SalesRecord, TaxFiling),
			new RecordHolding(SalesRecord, AuditTrail),
			NullLogger.Instance);

		var refusal = await gate.DescribeRefusalAsync(null, CancellationToken.None).ConfigureAwait(false);

		refusal.ShouldNotBeNull(
			"the record holds a live declaration this instance does not hold, so this instance cannot "
			+ "establish that the data it is about to destroy is not legally required to be kept. Holding "
			+ "a declaration the record lacks does not make that safe -- it only means this instance is "
			+ "also ahead in one pair, which a strict-subset predicate wrongly treats as reassurance.");

		refusal.ShouldContain(
			AuditTrail,
			customMessage: "the refusal must name the declaration that is missing, or an operator cannot "
				+ "tell which deploy to finish rolling out");
	}

	// LIVENESS, and without it every arm here is satisfied by a gate that refuses everything -- which is
	// its own regulator's finding, because an erasure never performed is a subject never erased.
	//
	// This instance is AHEAD: it declares a pair the record has not yet recorded, which is the normal
	// state of a freshly deployed instance before its own advance. Holding MORE than the record is never
	// a hazard; its own reads already spare the extra type.
	[Fact]
	public async Task Proceed_when_this_instance_holds_every_recorded_declaration_and_more()
	{
		var gate = new ErasureRetentionStalenessGate(
			RegistryDeclaring(SalesRecord, TaxFiling),
			new RecordHolding(SalesRecord),
			NullLogger.Instance);

		var refusal = await gate.DescribeRefusalAsync(null, CancellationToken.None).ConfigureAwait(false);

		refusal.ShouldBeNull(
			"an instance that holds every declaration the deployment has recorded may erase, even when it "
			+ "declares more. A newly deployed instance is in exactly this state before its own advance, "
			+ "and refusing here would withhold every erasure in the deployment for a window.");
	}

	// A WITHDRAWN declaration is not a live one. The record returns tombstones flagged so a reader can
	// tell "the deployment withdrew this obligation" from "the deployment never recorded it" -- and a
	// tombstone must not make an instance look stale, or a single withdrawal would halt every erasure for
	// that tenant until someone deployed code dropping the declaration.
	//
	// Mutant: stop filtering on the withdrawn flag -- this arm goes RED.
	[Fact]
	public async Task Proceed_when_the_only_declaration_this_instance_lacks_has_been_withdrawn()
	{
		var gate = new ErasureRetentionStalenessGate(
			RegistryDeclaring(SalesRecord),
			new RecordHolding(SalesRecord).AndWithdrawn(AuditTrail),
			NullLogger.Instance);

		var refusal = await gate.DescribeRefusalAsync(null, CancellationToken.None).ConfigureAwait(false);

		refusal.ShouldBeNull(
			"a withdrawn declaration is not a live one, so it cannot make this instance stale. Treating a "
			+ "tombstone as live would mean one administrative withdrawal halts every erasure for the "
			+ "tenant until a deploy drops the declaration from code.");
	}

	// THE READ IS FAIL-CLOSED, and its failure must be RETURNED rather than thrown: a throw from the gate
	// enters the scheduler's fault budget and terminally fails a correct withholding within a few polls.
	[Fact]
	public async Task Withhold_rather_than_assume_it_is_current_when_the_record_cannot_be_read()
	{
		var gate = new ErasureRetentionStalenessGate(
			RegistryDeclaring(SalesRecord),
			new UnreadableRecord(),
			NullLogger.Instance);

		var refusal = await gate.DescribeRefusalAsync(null, CancellationToken.None).ConfigureAwait(false);

		refusal.ShouldNotBeNull(
			"an erasure that cannot read the deployment's declarations must not proceed. The alternative "
			+ "is to assume this instance is current, which is the assumption the record exists to stop "
			+ "anyone making.");
	}

	// A deployment with no record at all is ONE PROCESS, so no other process can hold a view it is behind
	// and there is nothing for a comparison to be against. Passing is correct rather than blind -- but it
	// must be asserted, because a gate that refused here would stop every in-memory deployment erasing.
	[Fact]
	public async Task Proceed_when_the_deployment_has_no_record_to_compare_against()
	{
		var gate = new ErasureRetentionStalenessGate(
			RegistryDeclaring(SalesRecord), ledger: null, NullLogger.Instance);

		var refusal = await gate.DescribeRefusalAsync(null, CancellationToken.None).ConfigureAwait(false);

		refusal.ShouldBeNull();
	}

	// A PARTIAL ADVANCE MUST NOT CLOSE THE LATCH, and the latch must retry on the next attempt. An
	// unrecorded pair is a pair for which no stale instance can ever detect its own staleness, with no
	// tell anywhere -- so a prefix is a hole, not a weaker success.
	//
	// Mutant: set the latch's flag before awaiting the record, or catch and swallow the failure -- this
	// arm goes RED, because the second attempt would not re-record.
	[Fact]
	public async Task Retry_the_advance_after_a_failure_rather_than_treating_a_prefix_as_recorded()
	{
		using var latch = new ErasureRetentionAdvanceLatch(
			RegistryDeclaring(SalesRecord, TaxFiling),
			NullLogger<ErasureRetentionAdvanceLatch>.Instance);

		var record = new FailsOnceRecord();

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => latch.EnsureAdvancedAsync(record, CancellationToken.None)).ConfigureAwait(false);

		record.Attempts.ShouldBe(1);

		// The latch stayed OPEN, so this instance tries again rather than behaving as though the
		// deployment could see its declarations.
		await latch.EnsureAdvancedAsync(record, CancellationToken.None).ConfigureAwait(false);

		record.Attempts.ShouldBe(
			2,
			"a failed advance must leave the latch open. A latch closed on a prefix means this instance "
			+ "never records the rest, so no other instance can ever detect that it holds fewer "
			+ "declarations -- silently, for the life of the process.");

		// And once it HAS succeeded, it does not run again: the advance is a per-process act.
		await latch.EnsureAdvancedAsync(record, CancellationToken.None).ConfigureAwait(false);
		record.Attempts.ShouldBe(2, "a successful advance is latched and must not repeat per erasure");
	}

	private static IErasureRetentionRegistry RegistryDeclaring(params string[] aggregateTypes)
	{
		var options = new AggregateRetentionOptions();
		foreach (var type in aggregateTypes)
		{
			options.Aggregates.Add(new ErasureRetention
			{
				AggregateType = type,
				Tenant = KeyedTenantPartition.Untenanted,
				Basis = LegalHoldBasis.LegalObligation,
				Justification = "statutory record-keeping",
				RetentionPeriod = TimeSpan.FromDays(3650)
			});
		}

		return new ErasureRetentionRegistry(Options.Create(options));
	}

	/// <summary>A record holding exactly the declarations it was given, all live unless withdrawn.</summary>
	private sealed class RecordHolding : IErasureRetentionDeclarationLedger
	{
		private readonly List<RecordedRetentionDeclaration> _rows = [];

		public RecordHolding(params string[] liveTypes)
		{
			foreach (var type in liveTypes)
			{
				_rows.Add(new RecordedRetentionDeclaration(
					new DeclaredRetentionKey(TenantScope.UntenantedSentinel, type), IsWithdrawn: false));
			}
		}

		public RecordHolding AndWithdrawn(params string[] types)
		{
			foreach (var type in types)
			{
				_rows.Add(new RecordedRetentionDeclaration(
					new DeclaredRetentionKey(TenantScope.UntenantedSentinel, type), IsWithdrawn: true));
			}

			return this;
		}

		public Task RecordDeclaredRetentionsAsync(
			IReadOnlyCollection<DeclaredRetentionKey> declarations, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<IReadOnlyCollection<RecordedRetentionDeclaration>> ListDeclaredRetentionsAsync(
			string tenantId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyCollection<RecordedRetentionDeclaration>>(_rows);
	}

	/// <summary>A record that cannot be read, which must withhold rather than be assumed current.</summary>
	private sealed class UnreadableRecord : IErasureRetentionDeclarationLedger
	{
		public Task RecordDeclaredRetentionsAsync(
			IReadOnlyCollection<DeclaredRetentionKey> declarations, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<IReadOnlyCollection<RecordedRetentionDeclaration>> ListDeclaredRetentionsAsync(
			string tenantId, CancellationToken cancellationToken) =>
			throw new TimeoutException("the compliance database did not answer");
	}

	/// <summary>
	/// A record whose first advance records a PREFIX and then fails, as a per-pair advance can.
	/// </summary>
	private sealed class FailsOnceRecord : IErasureRetentionDeclarationLedger
	{
		public int Attempts { get; private set; }

		public Task RecordDeclaredRetentionsAsync(
			IReadOnlyCollection<DeclaredRetentionKey> declarations, CancellationToken cancellationToken)
		{
			Attempts++;

			return Attempts == 1
				? throw new InvalidOperationException(
					"the deployment-wide retention record is INCOMPLETE: 1 declaration(s) could not be recorded")
				: Task.CompletedTask;
		}

		public Task<IReadOnlyCollection<RecordedRetentionDeclaration>> ListDeclaredRetentionsAsync(
			string tenantId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyCollection<RecordedRetentionDeclaration>>([]);
	}
}
