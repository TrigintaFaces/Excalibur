// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware.Outbox;

/// <summary>
/// Stages the follow-up messages a handler opts to cascade (by returning an <see cref="ICascade"/> result)
/// to the outbox, so they are dispatched reliably after the handler completes.
/// </summary>
/// <remarks>
/// Runs at <see cref="DispatchMiddlewareStage.Cascade"/> — inside <c>OutboxStagingMiddleware</c> — so the
/// cascaded messages join the handler's outbound set and inherit its delivery semantics (at-least-once via
/// the outbox). Cascading is opt-in per handler result: a result that does not implement
/// <see cref="ICascade"/> produces no cascade, so the step is a no-op for every existing handler. Being
/// unconditional (no gating attribute) it composes with the compile-time pipeline generator.
/// </remarks>
internal sealed partial class CascadeMiddleware : IDispatchMiddleware
{
	private readonly ILogger<CascadeMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CascadeMiddleware"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public CascadeMiddleware(ILogger<CascadeMiddleware> logger) =>
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cascade;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		if (result.UntypedReturnValue is not ICascade cascade || cascade.Messages.Count == 0)
		{
			return result;
		}

		var outboxContext = context.GetItem<OutboxContext>("OutboxContext");
		if (outboxContext is null)
		{
			// The handler opted into cascading but no outbox is staged in this pipeline. Do not silently
			// drop the messages: surface it loudly so the misconfiguration is visible, and do not throw
			// (a diagnostics/config problem must not fail the handler that already succeeded).
			// Both cascade logs report the same quantity: messages this middleware could act on,
			// which excludes nulls. A null entry is never dispatchable, so counting it here would
			// overstate what the warning says went undispatched.
			var undispatched = 0;
			foreach (var cascaded in cascade.Messages)
			{
				if (cascaded is not null)
				{
					undispatched++;
				}
			}

			LogCascadeWithoutOutbox(message.GetType().Name, undispatched);
			return result;
		}

		var staged = 0;
		foreach (var cascaded in cascade.Messages)
		{
			if (cascaded is null)
			{
				continue;
			}

			outboxContext.AddOutboundMessage(cascaded);
			staged++;
		}

		// The count of messages actually handed to the outbox, not the raw list length: the loop
		// above skips nulls, so the raw length over-reports staging exactly when the handler
		// result is malformed -- the case where an accurate count matters most.
		LogCascadeStaged(message.GetType().Name, staged);
		return result;
	}

	[LoggerMessage(
		EventId = MiddlewareEventId.CascadeStaged,
		Level = LogLevel.Debug,
		Message = "Cascade: staged {Count} follow-up message(s) from handler result for {MessageType}")]
	private partial void LogCascadeStaged(string messageType, int count);

	[LoggerMessage(
		EventId = MiddlewareEventId.CascadeWithoutOutbox,
		Level = LogLevel.Warning,
		Message = "Cascade: handler for {MessageType} returned {Count} cascaded message(s) but no outbox is staged in the pipeline; messages were not dispatched. Enable outbox staging to use cascading.")]
	private partial void LogCascadeWithoutOutbox(string messageType, int count);
}
