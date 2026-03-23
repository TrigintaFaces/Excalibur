// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware.Ordering;

/// <summary>
/// Optional middleware that detects out-of-order messages via sequence number tracking.
/// </summary>
/// <remarks>
/// <para>
/// This middleware logs warnings when messages arrive out of order but does NOT block processing.
/// It is opt-in via <c>builder.UseOrderingValidation()</c>.
/// </para>
/// <para>
/// Ordering guarantees vary by transport:
/// <list type="bullet">
/// <item>In-memory: FIFO within single dispatcher</item>
/// <item>RabbitMQ: FIFO per queue (single consumer)</item>
/// <item>Kafka: FIFO per partition</item>
/// <item>SQS FIFO: FIFO per message group</item>
/// <item>Azure Service Bus: FIFO per session</item>
/// <item>gRPC: FIFO per connection (streaming)</item>
/// <item>Google Pub/Sub: Best-effort (no strict ordering)</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class OrderingValidationMiddleware : IDispatchMiddleware
{
	private readonly ConcurrentDictionary<string, long> _lastSequenceBySource = new(StringComparer.Ordinal);
	private readonly ILogger<OrderingValidationMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderingValidationMiddleware"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public OrderingValidationMiddleware(ILogger<OrderingValidationMiddleware> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// Check for sequence number in context properties
		if (context.Items.TryGetValue("SequenceNumber", out var seqObj) && seqObj is long sequenceNumber)
		{
			var source = context.Items.TryGetValue("Source", out var srcObj) && srcObj is string src
				? src
				: "default";

			var lastSequence = _lastSequenceBySource.GetOrAdd(source, -1L);

			if (sequenceNumber <= lastSequence)
			{
				LogOutOfOrder(_logger, sequenceNumber, lastSequence, source);
			}

			_lastSequenceBySource[source] = sequenceNumber;
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(2215, LogLevel.Warning,
		"Out-of-order message detected: sequence {SequenceNumber} <= last {LastSequence} from source '{Source}'.")]
	private static partial void LogOutOfOrder(ILogger logger, long sequenceNumber, long lastSequence, string source);
}
