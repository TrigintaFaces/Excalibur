// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportReceiver"/> to route rejected messages (with <c>requeue: false</c>)
/// to a dead letter handler. Records <c>dispatch.transport.messages.dead_lettered</c> metric.
/// </summary>
/// <remarks>
/// Uses a delegate for dead-letter routing to decouple from the specific <c>IDeadLetterQueueManager</c> interface,
/// allowing transport DI to wire the appropriate handler.
/// </remarks>
public sealed class DeadLetterTransportReceiver : DelegatingTransportReceiver
{
	private readonly Func<TransportReceivedMessage, string?, CancellationToken, Task> _deadLetterHandler;
	private readonly string _transportName;
	private readonly TagCardinalityGuard _sourceGuard;
	private readonly Counter<long>? _deadLetteredCounter;

	/// <summary>
	/// Initializes a new instance of the <see cref="DeadLetterTransportReceiver"/> class.
	/// </summary>
	/// <param name="innerReceiver">The inner receiver to decorate.</param>
	/// <param name="deadLetterHandler">A delegate that routes a message to the dead letter queue.
	/// Parameters: the message, the rejection reason, and a cancellation token.</param>
	/// <param name="transportName">The transport provider name for metric tagging (e.g., "Kafka").</param>
	/// <param name="meter">Optional meter for recording dead-letter metrics.</param>
	public DeadLetterTransportReceiver(
		ITransportReceiver innerReceiver,
		Func<TransportReceivedMessage, string?, CancellationToken, Task> deadLetterHandler,
		string transportName,
		Meter? meter = null) : base(innerReceiver)
	{
		_deadLetterHandler = deadLetterHandler ?? throw new ArgumentNullException(nameof(deadLetterHandler));
		_transportName = transportName ?? throw new ArgumentNullException(nameof(transportName));
		_sourceGuard = new TagCardinalityGuard(maxCardinality: 100);
		_deadLetteredCounter = meter?.CreateCounter<long>(
			TransportTelemetryConstants.MetricNames.MessagesDeadLettered,
			"messages",
			"Total messages routed to dead letter queue");
	}

	/// <inheritdoc />
	public override async Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		if (!requeue)
		{
			await _deadLetterHandler(message, reason, cancellationToken).ConfigureAwait(false);

			var tags = new TagList
			{
				{ TransportTelemetryConstants.Tags.TransportName, _transportName },
				{ TransportTelemetryConstants.Tags.Source, _sourceGuard.Guard(Source) },
			};
			_deadLetteredCounter?.Add(1, tags);
		}

		await base.RejectAsync(message, reason, requeue, cancellationToken).ConfigureAwait(false);
	}
}
