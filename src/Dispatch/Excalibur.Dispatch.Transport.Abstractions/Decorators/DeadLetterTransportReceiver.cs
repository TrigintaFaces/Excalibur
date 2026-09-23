// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Diagnostics;
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
internal sealed class DeadLetterTransportReceiver : DelegatingTransportReceiver
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
			"{messages}",
			"Total messages routed to dead letter queue");
	}

	/// <summary>
	/// Routes a non-requeued message to the dead-letter handler, then rejects it on the transport.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The dead-letter write happens <b>before</b> the rejection, so that a message is never discarded
	/// from the broker without a copy having been taken first.
	/// </para>
	/// <para>
	/// The consequence is that a rejection which fails leaves the message in the dead-letter store
	/// <b>and</b> owned by the broker, so it is delivered again and dead-lettered again on the next
	/// attempt. <b>The dead-letter handler must therefore be idempotent, keyed on the message id.</b> That
	/// is required regardless of this ordering — delivery is at-least-once, so the handler can see the
	/// same message twice for several reasons — but this is the path most likely to produce it.
	/// </para>
	/// </remarks>
	/// <param name="message">The message to reject.</param>
	/// <param name="reason">The reason for rejection, passed to the dead-letter handler.</param>
	/// <param name="requeue">Whether to requeue the message; the dead-letter write happens only when this is <see langword="false"/>.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>Task representing the reject operation.</returns>
	/// <exception cref="TransportSettlementException">
	/// The underlying transport could not settle the rejection. The dead-letter write has already
	/// happened by this point, so the message is both stored and still owned by the broker.
	/// </exception>
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
