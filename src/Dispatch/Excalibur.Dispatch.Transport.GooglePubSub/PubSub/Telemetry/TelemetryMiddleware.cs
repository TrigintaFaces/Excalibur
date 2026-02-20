// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Telemetry middleware for message processing pipeline.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TelemetryMiddleware" /> class. </remarks>
/// <param name="telemetryProvider"> The telemetry provider. </param>
/// <param name="subscription"> The subscription name. </param>
public sealed class TelemetryMiddleware(PubSubTelemetryProvider telemetryProvider, string subscription)
{
	private readonly PubSubTelemetryProvider _telemetryProvider =
		telemetryProvider ?? throw new ArgumentNullException(nameof(telemetryProvider));

	private readonly string _subscription = subscription ?? throw new ArgumentNullException(nameof(subscription));

	/// <summary>
	/// Processes a message with telemetry tracking.
	/// </summary>
	/// <param name="message"> The message to process. </param>
	/// <param name="next"> The next handler in the pipeline. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task representing the operation. </returns>
	public async Task ProcessAsync(
		PubsubMessage message,
		Func<PubsubMessage, CancellationToken, Task> next,
		CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();
		using var activity = _telemetryProvider.RecordMessageReceived(message, _subscription);

		try
		{
			await next(message, cancellationToken).ConfigureAwait(false);

			_telemetryProvider.RecordMessageAcknowledged(
				message.MessageId,
				_subscription,
				stopwatch.Elapsed);
		}
		catch (Exception ex)
		{
			_telemetryProvider.RecordMessageNacked(
				message.MessageId,
				_subscription,
				ex.GetType().Name);

			_ = (activity?.SetTag("exception.type", ex.GetType().FullName));
			_ = (activity?.SetTag("exception.message", ex.Message));
			_ = activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}
}
