// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Saga.Outbox;

/// <summary>
/// Mediates the publishing of saga events through an outbox for reliable delivery.
/// </summary>
/// <remarks>
/// <para>
/// This interface bridges sagas with the outbox pattern, ensuring that events
/// produced by saga steps are published atomically with saga state changes.
/// </para>
/// <para>
/// Register via <c>.WithOutbox()</c> on <see cref="DependencyInjection.ISagaBuilder"/>.
/// </para>
/// </remarks>
public interface ISagaOutboxMediator
{
	/// <summary>
	/// Publishes events through the configured outbox store.
	/// </summary>
	/// <param name="events">The domain events to publish through the outbox.</param>
	/// <param name="sagaId">The saga instance identifier for correlation.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task PublishThroughOutboxAsync(
		IReadOnlyList<IDomainEvent> events,
		string sagaId,
		CancellationToken cancellationToken);
}
