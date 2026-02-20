// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Represents a generic dispatch message with serialized payload and metadata.
/// </summary>
public sealed class DispatchMessage
{
	/// <summary>
	/// Gets or sets the unique identifier for the message.
	/// </summary>
	/// <value>
	/// The unique identifier for the message.
	/// </value>
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the type name of the message for routing and deserialization.
	/// </summary>
	/// <value>The current <see cref="MessageType"/> value.</value>
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized message payload as a byte array.
	/// </summary>
	/// <value>The current <see cref="Payload"/> value.</value>
	public byte[] Payload { get; set; } = [];

	/// <summary>
	/// Gets or sets the timestamp when the message was created.
	/// </summary>
	/// <value>
	/// The timestamp when the message was created.
	/// </value>
	public DateTime CreatedAt { get; set; } = CreateTimestamp();

	/// <summary>
	/// Creates a high-performance timestamp using ValueStopwatch.
	/// </summary>
	private static DateTime CreateTimestamp()
	{
		var perfTimestamp = ValueStopwatch.GetTimestamp();
		var elapsedTicks = (long)(perfTimestamp * 10_000_000.0 / ValueStopwatch.GetFrequency());
		var baseDateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		return new DateTime(baseDateTime.Ticks + elapsedTicks, DateTimeKind.Utc);
	}
}
