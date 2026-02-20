// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents a batch of received messages.
/// </summary>
public sealed class ReceivedMessageBatch
{
	/// <summary>
	/// Gets or sets the batch ID.
	/// </summary>
	/// <value>
	/// The batch ID.
	/// </value>
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets the messages in the batch.
	/// </summary>
	/// <value>
	/// The messages in the batch.
	/// </value>
	public Collection<Message> Messages { get; } = [];

	/// <summary>
	/// Gets the batch size.
	/// </summary>
	/// <value>
	/// The batch size.
	/// </value>
	public int Size => Messages.Count;

	/// <summary>
	/// Gets or sets the batch creation time.
	/// </summary>
	/// <value>
	/// The batch creation time.
	/// </value>
	public DateTime CreatedAt { get; set; } = CreateTimestamp();

	/// <summary>
	/// Gets or sets the queue URL.
	/// </summary>
	/// <value>
	/// The queue URL.
	/// </value>
	public Uri? QueueUrl { get; set; }

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
