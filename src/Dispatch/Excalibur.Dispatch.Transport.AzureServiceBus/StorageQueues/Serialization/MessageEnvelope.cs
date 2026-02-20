// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Represents a message envelope for Azure Storage Queue messages.
/// </summary>
/// <summary>
/// Represents a serialized message envelope for Azure Storage Queue messages.
/// </summary>
public sealed class StorageQueueMessageEnvelope
{
	/// <inheritdoc/>
	public required string MessageType { get; init; }

	/// <inheritdoc/>
	public required string MessageId { get; init; }

	/// <inheritdoc/>
	public string? CorrelationId { get; init; }

	/// <inheritdoc/>
	public required DateTimeOffset Timestamp { get; init; }

	/// <inheritdoc/>
	public required byte[] Body { get; init; }

	/// <inheritdoc/>
	public Dictionary<string, string?> Properties { get; init; } = [];
}
