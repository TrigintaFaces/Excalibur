// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

namespace Excalibur.Dispatch.Transport.Decorators;

/// <summary>
/// Decorates an <see cref="ITransportSender"/> to convert outgoing messages to CloudEvents format
/// using an <see cref="ICloudEventMapper{TransportMessage}"/>.
/// </summary>
public sealed class CloudEventsTransportSender : DelegatingTransportSender
{
	private readonly ICloudEventMapper<TransportMessage> _mapper;
	private readonly Func<TransportMessage, CloudEvent> _cloudEventFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventsTransportSender"/> class.
	/// </summary>
	/// <param name="innerSender">The inner sender to decorate.</param>
	/// <param name="mapper">The CloudEvent mapper for converting messages.</param>
	/// <param name="cloudEventFactory">A factory that creates a <see cref="CloudEvent"/> from a <see cref="TransportMessage"/>.</param>
	public CloudEventsTransportSender(
		ITransportSender innerSender,
		ICloudEventMapper<TransportMessage> mapper,
		Func<TransportMessage, CloudEvent> cloudEventFactory) : base(innerSender)
	{
		_mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
		_cloudEventFactory = cloudEventFactory ?? throw new ArgumentNullException(nameof(cloudEventFactory));
	}

	/// <inheritdoc />
	public override async Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		var cloudEvent = _cloudEventFactory(message);
		var encodedMessage = await _mapper.ToTransportMessageAsync(
			cloudEvent, _mapper.Options.DefaultMode, cancellationToken).ConfigureAwait(false);

		return await base.SendAsync(encodedMessage, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public override async Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		var encodedMessages = new List<TransportMessage>(messages.Count);
		foreach (var message in messages)
		{
			var cloudEvent = _cloudEventFactory(message);
			var encodedMessage = await _mapper.ToTransportMessageAsync(
				cloudEvent, _mapper.Options.DefaultMode, cancellationToken).ConfigureAwait(false);
			encodedMessages.Add(encodedMessage);
		}

		return await base.SendBatchAsync(encodedMessages, cancellationToken).ConfigureAwait(false);
	}
}
