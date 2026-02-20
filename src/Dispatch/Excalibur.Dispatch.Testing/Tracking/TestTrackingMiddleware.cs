// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Testing.Tracking;

/// <summary>
/// Internal middleware that records all dispatched messages into a <see cref="DispatchedMessageLog"/>.
/// Automatically registered at the <see cref="DispatchMiddlewareStage.Start"/> stage.
/// </summary>
internal sealed class TestTrackingMiddleware : IDispatchMiddleware
{
	private readonly DispatchedMessageLog _log;

	public TestTrackingMiddleware(DispatchedMessageLog log)
	{
		_log = log;
	}

	/// <summary>
	/// Executes at the very start of the pipeline to capture all messages.
	/// </summary>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		var timestamp = DateTimeOffset.UtcNow;
		IMessageResult? result = null;

		try
		{
			result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			return result;
		}
		finally
		{
			_log.Record(new DispatchedMessage(message, context, timestamp, result));
		}
	}
}
