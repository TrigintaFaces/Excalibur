// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messages;

/// <summary>
/// Generic implementation of a dispatch message with a string payload.
/// </summary>
public sealed class GenericDispatchMessage : IDispatchMessage
{
	private readonly Dictionary<string, object> _headers = [];
	private readonly DefaultMessageFeatures _features = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="GenericDispatchMessage" /> class.
	/// </summary>
	/// <param name="messageType"> The message type. </param>
	/// <param name="payload"> The message payload. </param>
	public GenericDispatchMessage(string messageType, string payload)
	{
		MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
		Payload = payload ?? throw new ArgumentNullException(nameof(payload));
		MessageId = Guid.NewGuid().ToString();
		Timestamp = DateTimeOffset.UtcNow;
		Body = payload;
		Headers = new ReadOnlyDictionary<string, object>(_headers);
		Features = _features;
	}

	/// <summary>
	/// Gets the unique message identifier.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public string MessageId { get; }

	/// <summary>
	/// Gets the message timestamp.
	/// </summary>
	/// <value>The current <see cref="Timestamp"/> value.</value>
	public DateTimeOffset Timestamp { get; }

	/// <summary>
	/// Gets the message headers as a read-only dictionary.
	/// </summary>
	/// <value>The current <see cref="Headers"/> value.</value>
	public IReadOnlyDictionary<string, object> Headers { get; }

	/// <summary>
	/// Gets the message body.
	/// </summary>
	/// <value>The current <see cref="Body"/> value.</value>
	public object Body { get; }

	/// <summary>
	/// Gets the message type identifier.
	/// </summary>
	/// <value>The current <see cref="MessageType"/> value.</value>
	public string MessageType { get; }

	/// <summary>
	/// Gets additional features and extensions for this message.
	/// </summary>
	/// <value>The current <see cref="Features"/> value.</value>
	public IMessageFeatures Features { get; }

	/// <summary>
	/// Gets or sets the correlation ID.
	/// </summary>
	/// <value>
	/// The correlation ID.
	/// </value>
	public string? CorrelationId
	{
		get => _headers.TryGetValue("CorrelationId", out var value) ? value?.ToString() : null;
		set
		{
			if (value != null)
			{
				_headers["CorrelationId"] = value;
			}
			else
			{
				_ = _headers.Remove("CorrelationId");
			}
		}
	}

	/// <summary>
	/// Gets the message payload as a string.
	/// </summary>
	/// <value>The current <see cref="Payload"/> value.</value>
	public string Payload { get; }

	/// <inheritdoc />
	public Guid Id => Guid.TryParse(MessageId, out var guid) ? guid : Guid.Empty;

	/// <inheritdoc />
	public MessageKinds Kind => MessageKinds.Action;

	/// <summary>
	/// Adds a header to the message.
	/// </summary>
	/// <param name="key"> The header key. </param>
	/// <param name="value"> The header value. </param>
	public void AddHeader(string key, object value) => _headers[key] = value;

	/// <inheritdoc />
	public override string ToString() => $"GenericDispatchMessage [Type={MessageType}, MessageId={MessageId}, Timestamp={Timestamp:O}]";
}
