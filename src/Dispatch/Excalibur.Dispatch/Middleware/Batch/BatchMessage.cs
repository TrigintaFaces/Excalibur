// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Middleware.Batch;

/// <summary>
/// A composite message that represents a batch of messages.
/// </summary>
/// <remarks>
/// Classified as a document because it is an inert container for other messages rather than an operation in its own
/// right. Leaving it unclassifiable would make the pipeline decide its protection from a default rather than from its
/// declared intent.
/// </remarks>
internal sealed class BatchMessage : IDispatchDocument
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
	}

	/// <inheritdoc/>
	public Guid Id { get; }

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
