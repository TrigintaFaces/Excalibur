// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Decides whether THIS instance holds every retention declaration the deployment has recorded for a
/// tenant, and therefore whether it may perform an irreversible erasure for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The asymmetry this closes.</b> Every other input to an irreversible erasure is durable — the
/// requests, the legal holds, the key ledgers, the token fingerprint. A retention declaration lived only
/// in each process's own memory, built in its constructor from code, with nothing comparing one
/// instance's view against another's. So during a rolling deploy that ADDS a declaration, an instance on
/// the previous version destroys the key, the escrow and the recovery tokens for a type the deploy
/// declares retained, and certifies it Completed — because by its own view it was right.
/// </para>
/// <para>
/// <b>THE PREDICATE IS CONTAINMENT, NOT STRICT SUBSET, and the difference is a real counter-example
/// rather than a preference.</b> Refusing when the local set is a strict SUBSET of the record conjoins
/// the actual hazard — the record holds a live declaration this instance does not hold — with a second
/// condition that is not a safety condition at all: that this instance knows nothing the record does
/// not. That second conjunct OPENS the gate precisely when an instance is simultaneously ahead in one
/// pair and behind in another:
/// </para>
/// <code>
/// v2 declares {A, C}   advances fully                 record = {A, C}
/// v3 declares {A, B}   advance records A, FAILS on B  record = {A, C}
/// strict subset: is {A,B} a subset of {A,C}? No  -&gt; gate PASSES -&gt; v3 destroys C's key
/// containment:   is C held by v3?            No  -&gt; gate REFUSES
/// </code>
/// <para>
/// A partial advance is reachable by construction on SQL Server, which has no single-statement
/// insert-if-absent and therefore records pair by pair. So the unsound predicate's counter-example is
/// reachable in this product, not merely in principle.
/// </para>
/// <para>
/// <b>Containment makes a PARTIALLY FILLED record sound to compare against — it does NOT make the
/// advance's completion irrelevant to safety.</b> A prefix never produces a false refusal and never makes
/// this comparison unsound with respect to what IS recorded, which is the part that matters because N
/// inserts cannot be made atomic. But an <em>unrecorded</em> pair is invisible to every instance, so for
/// that pair no stale instance can detect its own staleness. Completion is therefore safety-bearing for
/// the pairs that did not land, which is why <see cref="ErasureRetentionAdvanceLatch"/> closes only on
/// complete success. A model check refutes the latch-on-prefix design in three steps: an instance declares
/// a retention, its advance writes none of it, the latch closes anyway, and a second instance holding no
/// declarations passes containment against an empty record and destroys the retained type.
/// </para>
/// <para>
/// <b>STATELESS, and that is deliberate.</b> It caches nothing and latches nothing, so it can be built
/// per use and owns no disposable resource. Recording this instance's own declarations is a different
/// concern with a per-process lifetime, and it lives in <see cref="ErasureRetentionAdvanceLatch"/>.
/// </para>
/// </remarks>
internal sealed partial class ErasureRetentionStalenessGate
{
	private readonly IErasureRetentionRegistry _retentions;
	private readonly IErasureRetentionDeclarationLedger? _ledger;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureRetentionStalenessGate"/> class.
	/// </summary>
	/// <param name="retentions">This instance's own declarations.</param>
	/// <param name="ledger">
	/// The deployment-wide record, or <see langword="null"/> when the configured store provides none — an
	/// in-memory deployment is one process, so no other process can hold a view it is behind and there is
	/// nothing for a comparison to be against. The gate then passes everything, which is correct rather
	/// than blind.
	/// </param>
	/// <param name="logger">The logger.</param>
	public ErasureRetentionStalenessGate(
		IErasureRetentionRegistry retentions,
		IErasureRetentionDeclarationLedger? ledger,
		ILogger logger)
	{
		_retentions = retentions ?? throw new ArgumentNullException(nameof(retentions));
		_ledger = ledger;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Collapses every spelling of <em>untenanted</em> onto the one the record's key stores, so a
	/// single-tenant deployment matches its own declarations whether its requests name no tenant, an
	/// empty one, or the sentinel itself.
	/// </summary>
	/// <param name="tenantId">The tenant term as a request or a declaration recorded it.</param>
	/// <returns>The term to compare and to store.</returns>
	public static string TenantKeyOf(string? tenantId) =>
		string.IsNullOrWhiteSpace(tenantId) ? TenantScope.UntenantedSentinel : tenantId;

	/// <summary>
	/// Whether this instance may perform an irreversible erasure for <paramref name="tenantId"/>, and the
	/// reason when it may not.
	/// </summary>
	/// <param name="tenantId">The tenant the erasure request itself recorded.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="null"/> when this instance holds every recorded live declaration for that tenant;
	/// otherwise the reason it must withhold the destruction.
	/// </returns>
	/// <remarks>
	/// <para>
	/// A WITHDRAWN declaration is not a live one, so it never makes this instance stale. The read returns
	/// tombstones flagged and they are excluded here, which is what the record's live set means.
	/// </para>
	/// <para>
	/// <b>The read is FAIL-CLOSED, and its failure is RETURNED as a refusal rather than thrown.</b> An
	/// erasure that cannot read the deployment's declarations must not proceed — the alternative is to
	/// assume it is current, which is the assumption this gate exists to stop anyone making. It is
	/// returned rather than thrown because a throw from here enters the scheduler's fault budget and
	/// would terminally fail a correct withholding within a few polls.
	/// </para>
	/// </remarks>
	public async Task<string?> DescribeRefusalAsync(string? tenantId, CancellationToken cancellationToken)
	{
		if (_ledger is null)
		{
			return null;
		}

		var tenant = TenantKeyOf(tenantId);

		IReadOnlyCollection<RecordedRetentionDeclaration> recorded;
		try
		{
			recorded = await _ledger.ListDeclaredRetentionsAsync(tenant, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
#pragma warning disable CA1031 // Any failure to read the record withholds the destruction; see the remarks.
		catch (Exception ex)
#pragma warning restore CA1031
		{
			LogRetentionRecordUnreadable(tenant, ex);

			return "the deployment's recorded retention declarations could not be read, so this erasure "
				+ "cannot establish that this instance holds every declaration the deployment has made. "
				+ "Withholding the destruction is the only safe answer, and it is retried on a later pass.";
		}

		var held = _retentions.Declared
			.Where(r => string.Equals(TenantKeyOf(r.Tenant.TenantId), tenant, StringComparison.Ordinal))
			.Select(static r => r.AggregateType)
			.ToHashSet(StringComparer.Ordinal);

		// CONTAINMENT, not strict subset. A declaration this instance holds and the record does not is
		// HARMLESS here: it means this instance is AHEAD, and its own reads already spare that type.
		var missing = recorded
			.Where(static r => !r.IsWithdrawn)
			.Select(static r => r.Declaration.AggregateType)
			.Where(type => !held.Contains(type))
			.OrderBy(static type => type, StringComparer.Ordinal)
			.ToArray();

		if (missing.Length == 0)
		{
			return null;
		}

		LogRetentionViewStale(tenant, missing.Length, string.Join(", ", missing));

		return $"this instance does not hold {missing.Length} retention declaration(s) the deployment has "
			+ $"recorded for this tenant ({string.Join(", ", missing)}), so it cannot establish that the "
			+ "data it is about to destroy is not legally required to be kept. A rolling deploy that adds "
			+ "a retention declaration produces exactly this state on the instances that predate it.";
	}

	[LoggerMessage(
		ComplianceEventId.ErasureRetentionViewStale,
		LogLevel.Warning,
		"This instance does not hold {Count} retention declaration(s) the deployment recorded for tenant {Tenant} ({Missing}), so it withholds erasures for that tenant until it is updated")]
	private partial void LogRetentionViewStale(string tenant, int count, string missing);

	[LoggerMessage(
		ComplianceEventId.ErasureRetentionRecordUnreadable,
		LogLevel.Error,
		"The deployment-wide retention record could not be read for tenant {Tenant}, so erasures for it are withheld until it can be")]
	private partial void LogRetentionRecordUnreadable(string tenant, Exception exception);
}
