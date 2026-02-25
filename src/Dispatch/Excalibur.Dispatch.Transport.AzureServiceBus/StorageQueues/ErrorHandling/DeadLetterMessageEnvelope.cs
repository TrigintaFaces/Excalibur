// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Represents a message envelope for dead letter queue messages.
/// </summary>
internal sealed class DeadLetterMessageEnvelope
{
	public required string OriginalMessageId { get; init; }

	public required string OriginalMessage { get; init; }

	public required string DeadLetterReason { get; init; }

	public required DateTimeOffset DeadLetterTimestamp { get; init; }

	public required int OriginalDequeueCount { get; init; }

	public string? ExceptionDetails { get; init; }

	public string? CorrelationId { get; init; }

	public string? MessageType { get; init; }

	public Dictionary<string, string?> Properties { get; init; } = [];
}
