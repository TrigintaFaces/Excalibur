// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportSender"/> to set the deduplication ID on outgoing messages.
/// The transport implementation reads <see cref="TransportTelemetryConstants.PropertyKeys.DeduplicationId"/>
/// and maps it to the native deduplication concept (AWS MessageDeduplicationId, Azure dedup, etc.).
/// </summary>
public sealed class DeduplicationTransportSender : DelegatingTransportSender
{
	private readonly Func<TransportMessage, string?> _idSelector;

	/// <summary>
	/// Initializes a new instance of the <see cref="DeduplicationTransportSender"/> class.
	/// </summary>
	/// <param name="innerSender">The inner sender to decorate.</param>
	/// <param name="idSelector">A function that generates the deduplication ID from a message.</param>
	public DeduplicationTransportSender(ITransportSender innerSender, Func<TransportMessage, string?> idSelector)
		: base(innerSender) =>
		_idSelector = idSelector ?? throw new ArgumentNullException(nameof(idSelector));

	/// <inheritdoc />
	public override Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ApplyDeduplicationId(message);
		return base.SendAsync(message, cancellationToken);
	}

	/// <inheritdoc />
	public override Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		foreach (var message in messages)
		{
			ApplyDeduplicationId(message);
		}

		return base.SendBatchAsync(messages, cancellationToken);
	}

	private void ApplyDeduplicationId(TransportMessage message)
	{
		var id = _idSelector(message);
		if (id is not null)
		{
			message.Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId] = id;
		}
	}
}
