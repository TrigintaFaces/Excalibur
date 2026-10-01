// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Erasure;

/// <summary>
/// Names the registered projections an erasure could not reach, so the certificate cannot say
/// Completed while a read model still carries the data subject's material.
/// </summary>
/// <remarks>
/// <para>
/// <b>This contributor erases nothing, on purpose.</b> It reports the RESIDUE of an erasure that does
/// now reach read models: a projection whose id is the aggregate id is cleared per aggregate as part of
/// the erasure, and this names the ones that could not be — a projection registering a <c>KeyedBy</c>
/// selector folds many aggregates into one row, so replaying a single aggregate could not produce a
/// correct row for such a key. That residue is a real gap, and the failure this type prevents is not
/// the gap — it is the gap being reported as success.
/// </para>
/// <para>
/// Superseded wording, quoted so a reader who inherited it recognises it: "It exists because the
/// framework cannot yet propagate an erasure to a materialized read model: erasure tombstones the event
/// rows in place and appends nothing, so a projection that already folded the subject's events is never
/// told and keeps the data indefinitely." That was true of every projection when written. It is now
/// true only of the keyed ones, which is why this report names them instead of reporting all of them.
/// </para>
/// <para>
/// <b>Why a failing contributor rather than a declared location.</b> The coverage gate is conservative
/// over the inventory it is GIVEN: an uncovered location forces a non-Completed outcome. But coverage
/// is judged over locations the consumer's own inventory DISCOVERED, and nothing declares a projection
/// store as one. So on a default composition there was no projection to be uncovered about, and the
/// certificate reported Completed with every read model untouched. A contributor that reports failure
/// reaches the same fail-closed path through machinery that already exists, needs no new abstraction,
/// and is replaced — not merely satisfied — by the real one when erasure learns to notify.
/// </para>
/// <para>
/// <b>It declares NO covered store kinds, and that is load-bearing.</b> Declaring
/// <c>DataStoreKind.Projection</c> would mark projection locations COVERED, which is the exact lie this
/// removes: a marker must never be separable from the wiring it attests.
/// </para>
/// <para>
/// <b>It stands down when a consumer has solved this themselves.</b> If any other registered
/// contributor declares coverage of <see cref="DataStoreKind.Projection"/>, this one reports success
/// having done nothing and discharges nothing — the consumer's contributor is then the one the gate
/// judges. Without that, a consumer who built projection erasure could never obtain a Completed
/// certificate, which converts a visible gap into a permanent stall.
/// </para>
/// </remarks>
internal sealed class ProjectionErasureGapContributor : IErasureContributor
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionErasureGapContributor"/> class.
	/// </summary>
	/// <param name="serviceProvider">
	/// Resolves the projection registry and the sibling contributors, both read at erasure time rather
	/// than at construction: registration order is not knowable here, and resolving the contributor
	/// enumerable during construction would re-enter the enumerable this instance belongs to.
	/// </param>
	public ProjectionErasureGapContributor(IServiceProvider serviceProvider) =>
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

	/// <inheritdoc/>
	public string Name => "ProjectionErasureGap";

	/// <inheritdoc/>
	/// <remarks>
	/// Deliberately EMPTY. This contributor covers nothing, and saying otherwise would mark projection
	/// locations covered by the very thing that declares it cannot cover them.
	/// </remarks>
	public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } = new HashSet<DataStoreKind>();

	/// <inheritdoc/>
	public Task<ErasureContributorResult> EraseAsync(
		ErasureContributorContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		// No PERSISTED projections registered: there is no read model holding anything, so nothing is
		// reported. This is the liveness half — without it, every host on this framework would be
		// unable to obtain a Completed certificate whether or not it had a single read model.
		//
		// EPHEMERAL projections are excluded deliberately and it is not a convenience. An ephemeral
		// projection is COMPUTED ON DEMAND from the stream and persists no row, so once the events are
		// tombstoned the next computation carries none of the subject's data — there is nothing left
		// for an erasure to miss. Reporting a gap for one would be a false partial, and a control that
		// fires when it should not is as wrong as one that stays silent when it should: an operator who
		// sees it on every erasure stops reading it.
		if (_serviceProvider.GetService(typeof(IProjectionRegistry)) is not IProjectionRegistry registry)
		{
			return Task.FromResult(ErasureContributorResult.Succeeded(0));
		}

		var unreached = UnreachedProjections(registry, context.Scope);

		// Nothing was left holding the subject: either no projection persists a row, or every persisted
		// one was cleared per aggregate when its events were tombstoned.
		if (unreached.Count == 0)
		{
			return Task.FromResult(ErasureContributorResult.Succeeded(0));
		}

		// A consumer has taken responsibility for projection erasure. Stand down rather than block them
		// forever; their contributor is what the coverage gate now judges.
		if (AnotherContributorCoversProjections())
		{
			return Task.FromResult(ErasureContributorResult.Succeeded(0));
		}

		// NAMED, never counted. A controller discharging an Article 17 request has to deal with these read
		// models by hand, so the one thing the report must carry is WHICH ones. A count tells them nothing
		// they can act on.
		var reason = context.Scope == ErasureScope.Selective
			? "This erasure names specific data categories, and event-store erasure is whole-aggregate, so "
				+ "it tombstoned nothing and no projection was cleared. Every persisted read model still "
				+ "holds whatever it folded."
			: "A projection keyed per aggregate is cleared as part of the erasure: its row is replayed from "
				+ "the tombstoned stream, so it keeps none of the subject's data. The projections named "
				+ "above cannot be, because each registers a KeyedBy selector — many aggregates fold into "
				+ "one row, and replaying a single aggregate could not produce a correct row for such a key. "
				+ "Rebuild those projections to clear the subject from them.";

		return Task.FromResult(new ErasureContributorResult
		{
			Success = false,
			RecordsAffected = 0,
			ErrorMessage =
				"Registered projection(s) still hold this data subject's material: "
				+ string.Join(", ", unreached)
				+ ". " + reason
				+ " This erasure is therefore PARTIAL, not complete. Register your own IErasureContributor "
				+ "declaring DataStoreKind.Projection to take over this responsibility and silence this "
				+ "report.",
		});
	}

	/// <summary>
	/// Names every registered projection that may still hold the subject's material after this erasure.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An EPHEMERAL projection is excluded: it is computed on demand from the stream and persists no row,
	/// so once the events are tombstoned the next computation carries none of the subject's data. A
	/// control that fires when it should not is as wrong as one that stays silent when it should — an
	/// operator who sees this on every erasure stops reading it.
	/// </para>
	/// <para>
	/// A persisted projection is excluded when it carries a clear delegate, because that delegate is what
	/// the erasure invoked per aggregate. Reading the SAME property the erasure dispatches on is what
	/// keeps this report inseparable from the wiring it attests: a projection that became unclearable
	/// would reappear here without anyone remembering to update a list.
	/// </para>
	/// <para>
	/// A SELECTIVE erasure names data categories, and event-store erasure is whole-aggregate, so it
	/// refuses the request outright and tombstones nothing. No projection was cleared, and reporting only
	/// the keyed ones would credit a clearing that never happened.
	/// </para>
	/// </remarks>
	private static List<string> UnreachedProjections(IProjectionRegistry registry, ErasureScope scope)
	{
		var unreached = new List<string>();

		foreach (var registration in registry.GetAll())
		{
			if (registration.Mode is not (ProjectionMode.Inline or ProjectionMode.Async))
			{
				continue;
			}

			if (scope == ErasureScope.Selective || registration.ClearForAggregate is null)
			{
				unreached.Add(registration.ProjectionType.Name);
			}
		}

		return unreached;
	}

	/// <summary>
	/// Whether some other registered contributor declares coverage of projection stores.
	/// </summary>
	/// <remarks>
	/// Resolved here rather than injected, and filtered by reference so this instance cannot answer for
	/// itself. By the time an erasure runs, the contributor enumerable has already been materialized by
	/// the erasure service, so this resolution is a cache read rather than a re-entrant construction.
	/// </remarks>
	private bool AnotherContributorCoversProjections()
	{
		if (_serviceProvider.GetService(typeof(IEnumerable<IErasureContributor>))
			is not IEnumerable<IErasureContributor> contributors)
		{
			return false;
		}

		foreach (var contributor in contributors)
		{
			if (!ReferenceEquals(contributor, this) && contributor.CoveredStoreKinds.Contains(DataStoreKind.Projection))
			{
				return true;
			}
		}

		return false;
	}
}
