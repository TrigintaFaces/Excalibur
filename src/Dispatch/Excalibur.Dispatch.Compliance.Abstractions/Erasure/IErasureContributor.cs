// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

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
	/// Gets an error message if the operation failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Creates a successful result.
	/// </summary>
	/// <param name="recordsAffected">The number of records erased.</param>
	/// <returns>A successful contributor result.</returns>
	public static ErasureContributorResult Succeeded(int recordsAffected) => new()
	{
		Success = true,
		RecordsAffected = recordsAffected
	};

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
