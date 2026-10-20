// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

/// <summary>Optional renewable, expiring occurrence ownership on the configured schedule store.</summary>
/// <remarks>
/// Providers measure expiry using their authoritative clock, at the acquisition or renewal linearization
/// point. Progress and release validate the same ownership tuple. This capability does not guarantee
/// exactly-once handler effects; a handler that ignores cancellation can continue after ownership is lost.
/// </remarks>
public interface IScheduleExecutionLeaseRenewal : IScheduleExecutionStore
{
	/// <summary>Gets the fixed ownership duration granted by acquisition and successful renewal.</summary>
	/// <remarks>The duration must be positive. Local timer cadence is clamped to system timer granularity independently of the authoritative grant; longer durations use bounded timer slices.</remarks>
	TimeSpan LeaseDuration { get; }

	/// <summary>Extends current, unexpired ownership without changing its bearer tuple.</summary>
	/// <remarks>
	/// Validate the entire tuple at the same atomic boundary as expiry and administrative mutation.
	/// False changes nothing. Expired, replaced, released or foreign authority cannot be resurrected.
	/// Renewal remains available during progress settlement, including after a progress commit until release.
	/// </remarks>
	/// <param name="lease">Exact admitted bearer authority.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True only when current unexpired ownership was extended.</returns>
	Task<bool> TryRenewExecutionAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken);
}
