// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// A pluggable contributor that participates in GDPR erasure execution.
/// </summary>
/// <remarks>
/// <para>
/// Erasure contributors are invoked by <see cref="IErasureService"/> during
/// <c>ExecuteAsync</c> to erase data from additional stores (event stores,
/// snapshot stores, caches, etc.) beyond the core cryptographic key deletion.
/// </para>
/// <para>
/// This follows the Microsoft <c>IHealthCheckPublisher</c> pattern: a minimal
/// interface that allows frameworks to plug additional erasure logic into the
/// compliance pipeline without coupling the core service to specific stores.
/// </para>
/// <para>
/// Register implementations via DI as <c>IErasureContributor</c>. Multiple
/// contributors can be registered and will be invoked sequentially.
/// </para>
/// </remarks>
public interface IErasureContributor
{
	/// <summary>
	/// Gets the display name of this contributor for logging and diagnostics.
	/// </summary>
	/// <value>A human-readable name such as "EventStore" or "SnapshotStore".</value>
	string Name { get; }

	/// <summary>
	/// Gets the set of store kinds this contributor erases.
	/// </summary>
	/// <remarks>
	/// The erasure coverage gate uses this to mark a discovered <see cref="DataLocation"/> as
	/// <c>Covered</c> when its <see cref="DataLocation.StoreKind"/> is in this set.
	/// Must never contain <see cref="DataStoreKind.Unknown"/> — the unclassified kind is never coverable.
	/// </remarks>
	/// <value>The store kinds covered by this contributor; never <see langword="null"/>.</value>
	IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; }

	/// <summary>
	/// Erases data for the specified erasure request.
	/// </summary>
	/// <param name="context">The erasure context with request details.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of this contributor's erasure operation.</returns>
	Task<ErasureContributorResult> EraseAsync(
		ErasureContributorContext context,
		CancellationToken cancellationToken);
}

/// <summary>
/// Context information passed to <see cref="IErasureContributor"/> during erasure execution.
/// </summary>
public sealed record ErasureContributorContext
{
	/// <summary>
	/// Gets the erasure request tracking ID.
	/// </summary>
	public required Guid RequestId { get; init; }

	/// <summary>
	/// Gets the SHA-256 hash of the data subject identifier.
	/// </summary>
	public required string DataSubjectIdHash { get; init; }

	/// <summary>
	/// Gets the type of identifier used to identify the data subject.
	/// </summary>
	public required DataSubjectIdType IdType { get; init; }

	/// <summary>
	/// Gets the tenant context for multi-tenant scenarios.
	/// </summary>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the scope of the erasure operation.
	/// </summary>
	public required ErasureScope Scope { get; init; }

	/// <summary>
	/// Gets the declared table-and-field pairs registered against a store kind this contributor covers.
	/// </summary>
	/// <remarks>
	/// The erasure service filters these to the contributor's <see cref="IErasureContributor.CoveredStoreKinds"/>
	/// before the call, so a contributor names what it erased without having to guess which consumer-chosen
	/// table belongs to it. A pair registered with no store kind never appears here and therefore stays
	/// outstanding — silence fails closed.
	/// </remarks>
	public IReadOnlyList<DataLocationKey> DeclaredLocations { get; init; } = [];
}

/// <summary>
/// Result of an <see cref="IErasureContributor"/> erasure operation.
/// </summary>
public sealed record ErasureContributorResult
{
	/// <summary>
	/// Gets whether the contributor's erasure succeeded.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the number of records affected by this contributor.
	/// </summary>
	public int RecordsAffected { get; init; }

	/// <summary>
	/// Gets the table-and-field pairs this contributor erased for the subject.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is what discharges a registered obligation, and it is asserted per pair rather than per store
	/// kind. Declaring a store kind says what a contributor is ABLE to reach; this says what it actually
	/// erased for this subject, which is the only claim a certificate can rest on.
	/// </para>
	/// <para>
	/// The default is empty, and that is deliberate: a contributor that reports success without naming
	/// what it erased discharges NOTHING, so any registered obligation remains outstanding and the
	/// erasure cannot be reported complete. Failing closed on silence is the point — the alternative is
	/// a contributor that runs, deletes no rows, and satisfies the gate for every table in its store.
	/// </para>
	/// </remarks>
	public IReadOnlyList<DataLocationKey> DischargedLocations { get; init; } = [];

	/// <summary>
	/// Gets an error message if the operation failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Creates a successful result that discharges NO registered obligation.
	/// </summary>
	/// <param name="recordsAffected">The number of records erased.</param>
	/// <returns>A successful contributor result naming nothing it erased.</returns>
	/// <remarks>
	/// Prefer the overload that names the erased table-and-field pairs. This one reports success without
	/// evidence, so every registered obligation stays outstanding and the erasure cannot be reported
	/// complete — which is the safe direction, but it means a contributor using this overload can never
	/// satisfy the coverage gate on its own.
	/// </remarks>
	public static ErasureContributorResult Succeeded(int recordsAffected) => new()
	{
		Success = true,
		RecordsAffected = recordsAffected
	};

	/// <summary>
	/// Creates a successful result that names the table-and-field pairs the contributor erased.
	/// </summary>
	/// <param name="recordsAffected">The number of records erased.</param>
	/// <param name="dischargedLocations">The pairs erased for this subject.</param>
	/// <returns>A successful result carrying its discharged obligations.</returns>
	public static ErasureContributorResult Succeeded(
		int recordsAffected,
		IReadOnlyList<DataLocationKey> dischargedLocations)
	{
		ArgumentNullException.ThrowIfNull(dischargedLocations);

		return new ErasureContributorResult
		{
			Success = true,
			RecordsAffected = recordsAffected,
			DischargedLocations = dischargedLocations,
		};
	}

	/// <summary>
	/// Creates a failed result.
	/// </summary>
	/// <param name="errorMessage">The error message.</param>
	/// <returns>A failed contributor result.</returns>
	public static ErasureContributorResult Failed(string errorMessage) => new()
	{
		Success = false,
		ErrorMessage = errorMessage
	};
}
