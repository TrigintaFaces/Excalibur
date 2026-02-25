// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Represents the result of parsing a queue message.
/// </summary>
public sealed record ParsedMessageResult
{
	/// <summary>
	/// Gets the parsed message object.
	/// </summary>
	/// <value>
	/// The parsed message object.
	/// </value>
	public required object Message { get; init; }

	/// <summary>
	/// Gets the message context.
	/// </summary>
	/// <value>
	/// The message context.
	/// </value>
	public required IMessageContext Context { get; init; }

	/// <summary>
	/// Gets the message type information.
	/// </summary>
	/// <value>
	/// The message type information.
	/// </value>
	public required Type MessageType { get; init; }

	/// <summary>
	/// Gets a value indicating whether the message is a CloudEvent.
	/// </summary>
	/// <value>
	/// A value indicating whether the message is a CloudEvent.
	/// </value>
	public bool IsCloudEvent { get; init; }

	/// <summary>
	/// Gets additional metadata about the parsed message.
	/// </summary>
	/// <value>
	/// Additional metadata about the parsed message.
	/// </value>
	public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
