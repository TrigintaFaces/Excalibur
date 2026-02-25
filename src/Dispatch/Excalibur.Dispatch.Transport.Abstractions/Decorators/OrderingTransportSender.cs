// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportSender"/> to set the ordering key on outgoing messages.
/// The transport implementation reads <see cref="TransportTelemetryConstants.PropertyKeys.OrderingKey"/>
/// and maps it to the native ordering concept (Kafka message key, AWS MessageGroupId, Azure SessionId, etc.).
/// </summary>
public sealed class OrderingTransportSender : DelegatingTransportSender
{
	private readonly Func<TransportMessage, string?> _keySelector;

	/// <summary>
	/// Initializes a new instance of the <see cref="OrderingTransportSender"/> class.
	/// </summary>
	/// <param name="innerSender">The inner sender to decorate.</param>
	/// <param name="keySelector">A function that extracts the ordering key from a message.</param>
	public OrderingTransportSender(ITransportSender innerSender, Func<TransportMessage, string?> keySelector)
		: base(innerSender) =>
		_keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

	/// <inheritdoc />
	public override Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ApplyOrderingKey(message);
		return base.SendAsync(message, cancellationToken);
	}

	/// <inheritdoc />
	public override Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		foreach (var message in messages)
		{
			ApplyOrderingKey(message);
		}

		return base.SendBatchAsync(messages, cancellationToken);
	}

	private void ApplyOrderingKey(TransportMessage message)
	{
		var key = _keySelector(message);
		if (key is not null)
		{
			message.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey] = key;
		}
	}
}
