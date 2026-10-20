// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Optional authoritative occurrence-admission capability on the configured schedule store.</summary>
/// <remarks>
/// Cast the configured <see cref="IScheduleStore"/> to this capability; a separately resolved store is not
/// the same authority. Admission, administrative mutation and progress must share one linearization boundary.
/// This capability does not by itself promise durable storage, distributed ownership, exactly-once effects,
/// lease expiry or recovery after a worker dies. Providers must document those guarantees separately.
/// </remarks>
public interface IScheduleExecutionStore : IScheduleStore
{
	/// <summary>Reads detached schedule snapshots with opaque administrative generations.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The versioned snapshots.</returns>
	Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken cancellationToken);
	/// <summary>Admits one unowned occurrence only if its generation and enabled stored occurrence are still current, and it is due.</summary>
	/// <remarks>
	/// <para>
	/// Admission validates stored identity, ownership, and wall-clock eligibility. The scheduler also
	/// checks due-ness with its own <c>TimeProvider</c> before asking, but that check is an optimisation
	/// and never the guarantee: an implementation MUST refuse an occurrence whose instant is still in the
	/// future according to the STORE's own clock.
	/// </para>
	/// <para>
	/// <b>Why the store is the time authority.</b> Admission is first-come, so if due-ness rests on each
	/// caller's clock the host running fastest wins every occurrence — not occasionally, but for the whole
	/// inventory, and it runs all of them early. One misconfigured host is enough, the records it writes
	/// carry its own skewed instants, and nothing downstream can distinguish the result from correct
	/// operation. A single comparison against the store's clock removes any need for hosts to agree.
	/// </para>
	/// <para>
	/// The clock MUST be the store's own (for a relational provider, the database server's), never a value
	/// the caller supplies — a caller-supplied "now" reinstates exactly the trust this refusal removes.
	/// </para>
	/// </remarks>
	/// <param name="scheduleId">Schedule identifier.</param>
	/// <param name="generation">Generation from the detached snapshot.</param>
	/// <param name="occurrenceUtc">Exact occurrence from that snapshot.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Bearer authority for the occurrence, or null when admission loses.</returns>
	Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid scheduleId, Guid generation, DateTimeOffset occurrenceUtc, CancellationToken cancellationToken);
	/// <summary>Commits at most one bounded progress result under a still-current lease.</summary>
	/// <remarks>Only last execution, next execution and disabling may change. Payload replacement and enabling require administrative storage and a fresh generation.</remarks>
	/// <param name="lease">Exact admitted bearer authority.</param>
	/// <param name="result">Bounded progress update.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True only when this is the first successful progress commit for this admission.</returns>
	Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, CancellationToken cancellationToken);
	/// <summary>Releases only matching authority, including after its progress commit.</summary>
	/// <param name="lease">Exact admitted bearer authority.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True when matching authority was released.</returns>
	Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken);
}
