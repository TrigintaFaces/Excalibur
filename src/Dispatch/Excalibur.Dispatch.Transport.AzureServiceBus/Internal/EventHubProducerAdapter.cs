// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Forwards the framework's Event Hubs send seam to the live Azure client.
/// </summary>
/// <remarks>
/// A pass-through by design: every method delegates and nothing else happens here. Behaviour added in
/// this adapter would be behaviour no substituted implementation has, so the seam would stop being a
/// faithful stand-in for the real client and tests would diverge from production in a way nothing
/// reports.
/// </remarks>
internal sealed class EventHubProducerAdapter(EventHubProducerClient producer) : IEventHubProducer
{
	private readonly EventHubProducerClient _producer =
		producer ?? throw new ArgumentNullException(nameof(producer));

	/// <inheritdoc />
	public ValueTask<EventDataBatch> CreateBatchAsync(CancellationToken cancellationToken) =>
		_producer.CreateBatchAsync(cancellationToken);

	/// <inheritdoc />
	public Task SendAsync(EventDataBatch batch, CancellationToken cancellationToken) =>
		_producer.SendAsync(batch, cancellationToken);

	/// <inheritdoc />
	public ValueTask DisposeAsync() => _producer.DisposeAsync();
}
