// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Controls how integration events are staged to the outbox during aggregate save operations.
/// </summary>
/// <remarks>
/// <para>
/// This setting controls the trade-off between delivery guarantees and save latency.
/// Choose the strategy that matches your consistency and performance requirements.
/// </para>
/// <para>
/// <list type="bullet">
/// <item><see cref="Auto"/> -- Framework selects the best strategy based on available infrastructure (default)</item>
/// <item><see cref="Transactional"/> -- Atomic with event append, zero message loss, adds save latency</item>
/// <item><see cref="EventuallyConsistent"/> -- Staged after save in a separate call, tiny loss window</item>
/// <item><see cref="Deferred"/> -- No staging during save; background service processes events later, zero added latency</item>
/// </list>
/// </para>
/// </remarks>
public enum OutboxStagingStrategy
{
	/// <summary>
	/// The framework selects the best available strategy automatically.
	/// Uses <see cref="Transactional"/> when <see cref="ITransactionalOutboxWriter"/> and a transactional
	/// event store are available, falls back to <see cref="EventuallyConsistent"/> when only
	/// <see cref="IOutboxStore"/> is available, and <see cref="Deferred"/> when neither is registered.
	/// </summary>
	Auto = 0,

	/// <summary>
	/// Stages integration events within the same database transaction as the event append.
	/// Guarantees zero message loss but adds latency to the save operation.
	/// Requires <see cref="ITransactionalOutboxWriter"/> and a transactional event store.
	/// Falls back to <see cref="EventuallyConsistent"/> if transactional infrastructure is unavailable.
	/// </summary>
	Transactional = 1,

	/// <summary>
	/// Stages integration events to the outbox in a separate operation after the event append succeeds.
	/// Minimal latency impact with a tiny window for message loss if the process crashes between
	/// event append and outbox staging. Requires <see cref="IOutboxStore"/>.
	/// </summary>
	EventuallyConsistent = 2,

	/// <summary>
	/// Does not stage integration events during the save operation. Events are processed later
	/// by a background service (e.g., via inline projections or event store polling).
	/// Zero added latency to save operations. Suitable for high-throughput scenarios
	/// where minimal save latency is more important than immediate message delivery.
	/// </summary>
	Deferred = 3,
}
