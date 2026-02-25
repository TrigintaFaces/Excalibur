// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// A message that represents a bulk collection of operations.
/// </summary>
internal sealed class BulkMessage(IList<IDispatchMessage> messages, string operationKey) : IDispatchMessage
{
	/// <inheritdoc/>
	public string MessageId { get; } = Guid.NewGuid().ToString();

	/// <inheritdoc/>
	public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

	/// <inheritdoc/>
	public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <inheritdoc/>
	public object Body { get; } = messages;

	/// <inheritdoc/>
	public string MessageType { get; } = "BulkOptimized";

	/// <inheritdoc/>
	public IMessageFeatures Features { get; } = new DefaultMessageFeatures();

	/// <inheritdoc/>
	public Guid Id => Guid.TryParse(MessageId, out var guid) ? guid : Guid.Empty;

	/// <inheritdoc/>
	public MessageKinds Kind => MessageKinds.Action;

	public IList<IDispatchMessage> Messages { get; } = messages;

	public string OperationKey { get; } = operationKey;
}
