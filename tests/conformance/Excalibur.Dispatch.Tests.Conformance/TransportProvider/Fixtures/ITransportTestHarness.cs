// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Provides the operations required by the transport conformance test suite.
/// </summary>
public interface ITransportTestHarness : IAsyncDisposable
{
	/// <summary>
	///     Initializes the harness and prepares the backing transport resources.
	/// </summary>
	ValueTask InitializeAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Removes any outstanding messages or metadata from the transport queues.
	/// </summary>
	ValueTask PurgeAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Publishes a message to the transport.
	/// </summary>
	ValueTask PublishAsync(TransportTestMessage message, CancellationToken cancellationToken = default);

	/// <summary>
	///     Publishes two messages with the same identifier to evaluate idempotency guarantees.
	/// </summary>
	ValueTask PublishDuplicateAsync(
		TransportTestMessage original,
		TransportTestMessage duplicate,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Attempts to receive a message within the provided timeout window.
	/// </summary>
	ValueTask<TransportTestReceiveContext?> ReceiveAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Acknowledges a message and removes it from the underlying transport.
	/// </summary>
	ValueTask AcknowledgeAsync(TransportTestReceiveContext context, CancellationToken cancellationToken = default);

	/// <summary>
	///     Rejects a message and optionally requeues it for delivery.
	/// </summary>
	ValueTask NegativeAcknowledgeAsync(
		TransportTestReceiveContext context,
		bool requeue,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Retrieves any messages present in the dead-letter queue.
	/// </summary>
	ValueTask<IReadOnlyList<TransportTestDeadLetterMessage>> ReadDeadLettersAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default);

	/// <summary>
	///     Publishes a CloudEvent to the transport channel.
	/// </summary>
	ValueTask PublishCloudEventAsync(CloudEvent cloudEvent, CancellationToken cancellationToken = default);

	/// <summary>
	///     Receives a CloudEvent from the transport channel.
	/// </summary>
	ValueTask<CloudEvent?> ReceiveCloudEventAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default);
}
