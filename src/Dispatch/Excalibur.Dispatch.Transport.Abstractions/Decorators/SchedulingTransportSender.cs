// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportSender"/> to set the scheduled delivery time on outgoing messages.
/// The transport implementation reads <see cref="TransportTelemetryConstants.PropertyKeys.ScheduledTime"/>
/// and maps it to the native scheduling concept (Azure ScheduledEnqueueTime, Google publish time, etc.).
/// </summary>
public sealed class SchedulingTransportSender : DelegatingTransportSender
{
	private readonly Func<TransportMessage, DateTimeOffset?> _timeSelector;

	/// <summary>
	/// Initializes a new instance of the <see cref="SchedulingTransportSender"/> class.
	/// </summary>
	/// <param name="innerSender">The inner sender to decorate.</param>
	/// <param name="timeSelector">A function that determines the scheduled delivery time from a message.</param>
	public SchedulingTransportSender(ITransportSender innerSender, Func<TransportMessage, DateTimeOffset?> timeSelector)
		: base(innerSender) =>
		_timeSelector = timeSelector ?? throw new ArgumentNullException(nameof(timeSelector));

	/// <inheritdoc />
	public override Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ApplyScheduledTime(message);
		return base.SendAsync(message, cancellationToken);
	}

	/// <inheritdoc />
	public override Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		foreach (var message in messages)
		{
			ApplyScheduledTime(message);
		}

		return base.SendBatchAsync(messages, cancellationToken);
	}

	private void ApplyScheduledTime(TransportMessage message)
	{
		var scheduledTime = _timeSelector(message);
		if (scheduledTime.HasValue)
		{
			message.Properties[TransportTelemetryConstants.PropertyKeys.ScheduledTime] =
				scheduledTime.Value.ToString("O");
		}
	}
}
