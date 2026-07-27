// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Middleware.Batch;

/// <summary>
/// A message that represents a bulk collection of operations.
/// </summary>
/// <remarks>
/// Classified as an action because it carries a batch of dispatched operations down the remaining pipeline. Leaving it
/// unclassifiable would make the pipeline decide its protection from a default rather than from its declared intent.
/// </remarks>
internal sealed class BulkMessage(IList<IDispatchMessage> messages, string operationKey) : IDispatchAction
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

	public IList<IDispatchMessage> Messages { get; } = messages;

	public string OperationKey { get; } = operationKey;
}
