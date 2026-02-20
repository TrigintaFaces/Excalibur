// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Simple message wrapper for CloudEvent data conversion.
/// </summary>
public sealed class CloudEventMessage : IDispatchMessage
{
	private readonly Dictionary<string, object> _headers = [];
	private readonly DefaultMessageFeatures _features = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudEventMessage" /> class.
	/// </summary>
	public CloudEventMessage()
	{
		MessageId = Guid.NewGuid().ToString();
		Timestamp = DateTimeOffset.UtcNow;
		Headers = new ReadOnlyDictionary<string, object>(_headers);
		Features = _features;
	}

	/// <summary>
	/// Gets or sets the CloudEvent ID.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public string MessageId { get; set; }

	/// <summary>
	/// Gets or sets the CloudEvent type.
	/// </summary>
	/// <value>The current <see cref="Type"/> value.</value>
	public string Type { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the CloudEvent data.
	/// </summary>
	/// <value>The current <see cref="Data"/> value.</value>
	public object? Data { get; set; }

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; set; }

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Headers { get; }

	/// <inheritdoc />
	public object Body => Data ?? new object();

	/// <inheritdoc />
	public string MessageType => Type;

	/// <inheritdoc />
	public IMessageFeatures Features { get; }

	/// <inheritdoc />
	public Guid Id => Guid.TryParse(MessageId, out var guid) ? guid : Guid.Empty;

	/// <inheritdoc />
	public MessageKinds Kind => MessageKinds.Event;
}
