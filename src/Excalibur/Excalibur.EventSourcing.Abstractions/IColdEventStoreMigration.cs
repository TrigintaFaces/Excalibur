// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>Optional administrative capability for an offline cold archive namespace cutover.</summary>
/// <remarks>
/// <para>
/// Obtain this capability from the same captured <see cref="IColdEventStore"/> instance used by the
/// application. Decorators must explicitly mediate this capability and preserve their tenant restrictions;
/// resolving a second provider or unwrapping a decorator does not establish the same storage binding.
/// This optional administrative capability is not required for ordinary tiered-store startup.
/// A tenant-confined decorator must deny namespace activation unless the entire namespace belongs to
/// that tenant or the caller has explicit namespace-administrative authorization; checking only the
/// current tenant cannot authorize an operation affecting other tenants.
/// </para>
/// <para>
/// Before activation, the operator must externally fence and drain every legacy operation and retry,
/// including updated binaries. Old binaries must remain fenced afterwards. These methods do not acquire
/// that fence. Retain the namespace marker, migration receipts, legacy source archives and typed archives;
/// prohibit their deletion or replacement outside the protocol, including bucket/container recreation.
/// </para>
/// <para>
/// Activation prevents legacy access to the entire namespace but does not certify that any stream was
/// migrated. A failed or cancelled operation may leave a durable marker, copied archive or receipt.
/// Retry validates that state and resumes; deleting it to roll back is unsafe. Successful migration
/// does not authorize deleting retained source archives or provide a cold-data erasure capability.
/// </para>
/// </remarks>
public interface IColdEventStoreMigration
{
	/// <summary>Durably activates typed storage for the captured namespace after all legacy work is drained.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task completed only after the immutable activation marker has been verified.</returns>
	/// <remarks>
	/// This namespace-wide administrative operation precedes every typed object write, including migration
	/// copies. It is not performed implicitly by ordinary reads. Unknown or conflicting markers fail closed.
	/// A crash after activation leaves legacy access blocked; resume migration rather than removing the marker.
	/// Activation does not change this instance's frozen layout selection. Ordinary typed operations require
	/// an instance configured with <see cref="ColdArchiveLayout.TypedV2"/>.
	/// </remarks>
	Task ActivateTypedLayoutAsync(CancellationToken cancellationToken);

	/// <summary>Copies and verifies one complete legacy archive into its typed location after activation.</summary>
	/// <param name="tenant">The exact tenant partition owning the archive.</param>
	/// <param name="aggregateId">The case-sensitive aggregate identifier.</param>
	/// <param name="aggregateType">The case-sensitive aggregate type proven by the complete legacy archive.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task completed after the retained source, typed archive and durable receipt have been verified.</returns>
	/// <remarks>
	/// Missing, empty, ambiguous or malformed legacy archives are not guessed into a type. Published
	/// migrations can be retried after typed appends; the original baseline must still be preserved in full.
	/// Sparse versions are preserved. Existing conflicting typed data is never overwritten to force migration.
	/// </remarks>
	Task MigrateAsync(KeyedTenantPartition tenant, string aggregateId, string aggregateType, CancellationToken cancellationToken);
}
