// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// A lightweight, read-only projection of a persisted saga instance for administrative and dashboard
/// listing, carrying identity, type, lifecycle state, and concurrency version without the full state
/// payload.
/// </summary>
/// <remarks>
/// Returned by <see cref="ISagaStoreAdmin"/> query operations. The full saga state is loaded, when
/// needed, via <see cref="ISagaStore.LoadAsync{TSagaState}"/>.
/// </remarks>
public sealed class SagaInstanceSummary
{
	/// <summary>Gets the unique identifier of the saga instance.</summary>
	/// <value>The saga id.</value>
	public required Guid SagaId { get; init; }

	/// <summary>Gets the saga state type name (the persisted <c>SagaType</c>, or the runtime type name).</summary>
	/// <value>The saga type name.</value>
	public required string SagaType { get; init; }

	/// <summary>Gets a value indicating whether the saga has reached its terminal (completed) state.</summary>
	/// <value><see langword="true"/> when the saga is completed; otherwise <see langword="false"/>.</value>
	public bool IsCompleted { get; init; }

	/// <summary>Gets the instant the saga completed, or <see langword="null"/> while it is still running.</summary>
	/// <value>The completion timestamp, or <see langword="null"/>.</value>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>Gets the owning tenant identifier, or <see langword="null"/> when the saga is not tenant-scoped.</summary>
	/// <value>The tenant id, or <see langword="null"/>.</value>
	public string? TenantId { get; init; }

	/// <summary>Gets the optimistic-concurrency version of the saga state.</summary>
	/// <value>The saga state version.</value>
	public long Version { get; init; }
}
