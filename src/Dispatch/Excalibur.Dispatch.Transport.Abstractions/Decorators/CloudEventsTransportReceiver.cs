// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportReceiver"/> to detect and unwrap CloudEvents-encoded messages
/// after receiving them from the transport.
/// </summary>
/// <remarks>
/// Uses an <see cref="ICloudEventMapper{TransportReceivedMessage}"/> to detect CloudEvents mode and extract
/// the original message payload. Non-CloudEvents messages pass through unchanged.
/// </remarks>
public sealed class CloudEventsTransportReceiver : DelegatingTransportReceiver
{
	private readonly ICloudEventMapper<TransportReceivedMessage> _mapper;
	private readonly Func<TransportReceivedMessage, TransportReceivedMessage>? _unwrapper;

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventsTransportReceiver"/> class.
	/// </summary>
	/// <param name="innerReceiver">The inner receiver to decorate.</param>
	/// <param name="mapper">The CloudEvent mapper for detecting and unwrapping CloudEvents.</param>
	/// <param name="unwrapper">Optional function to transform the message after CloudEvent detection.
	/// If <see langword="null"/>, messages pass through with only CloudEvent metadata added to properties.</param>
	public CloudEventsTransportReceiver(
		ITransportReceiver innerReceiver,
		ICloudEventMapper<TransportReceivedMessage> mapper,
		Func<TransportReceivedMessage, TransportReceivedMessage>? unwrapper = null) : base(innerReceiver)
	{
		_mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
		_unwrapper = unwrapper;
	}

	/// <inheritdoc />
	public override async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		var messages = await base.ReceiveAsync(maxMessages, cancellationToken).ConfigureAwait(false);

		if (messages.Count == 0)
		{
			return messages;
		}

		var processed = new List<TransportReceivedMessage>(messages.Count);
		foreach (var message in messages)
		{
			var mode = await _mapper.TryDetectModeAsync(message, cancellationToken).ConfigureAwait(false);
			if (mode.HasValue && _unwrapper is not null)
			{
				processed.Add(_unwrapper(message));
			}
			else
			{
				processed.Add(message);
			}
		}

		return processed;
	}
}
