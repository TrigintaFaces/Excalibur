// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// A composite message that represents a batch of messages.
/// </summary>
internal sealed class BatchMessage : IDispatchMessage
{
	public BatchMessage(IList<IDispatchMessage> messages)
	{
		Messages = messages;
		Id = Guid.NewGuid();
		MessageId = Id.ToString();
		Timestamp = DateTimeOffset.UtcNow;
		Headers = new Dictionary<string, object>(StringComparer.Ordinal);
		Body = messages;
		MessageType = "BatchMessage";
		Features = new DefaultMessageFeatures();

		// Batch messages are document-style messages containing multiple messages
		Kind = MessageKinds.Document;
	}

	/// <inheritdoc/>
	public Guid Id { get; }

	/// <inheritdoc/>
	public MessageKinds Kind { get; }

	public IList<IDispatchMessage> Messages { get; }



	/// <inheritdoc/>
	public string MessageId { get; }

	/// <inheritdoc/>
	public DateTimeOffset Timestamp { get; }

	/// <inheritdoc/>
	public IReadOnlyDictionary<string, object> Headers { get; }

	/// <inheritdoc/>
	public object Body { get; }

	/// <inheritdoc/>
	public string MessageType { get; }

	/// <inheritdoc/>
	public IMessageFeatures Features { get; }
}
