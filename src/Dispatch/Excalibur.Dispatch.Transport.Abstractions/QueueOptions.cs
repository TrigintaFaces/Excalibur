// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Options for creating a queue.
/// </summary>
public sealed class QueueOptions
{
	/// <summary>
	/// Gets or sets the maximum size of the queue in MB.
	/// </summary>
	/// <value>The current <see cref="MaxSizeInMB"/> value.</value>
	[Range(1, long.MaxValue)]
	public long? MaxSizeInMB { get; set; }

	/// <summary>
	/// Gets or sets the default message time to live.
	/// </summary>
	/// <value>The current <see cref="DefaultMessageTimeToLive"/> value.</value>
	public TimeSpan? DefaultMessageTimeToLive { get; set; }

	/// <summary>
	/// Gets or sets the lock duration for messages.
	/// </summary>
	/// <value>The current <see cref="LockDuration"/> value.</value>
	public TimeSpan? LockDuration { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether duplicate detection is enabled.
	/// </summary>
	/// <value>The current <see cref="EnableDeduplication"/> value.</value>
	public bool? EnableDeduplication { get; set; }

	/// <summary>
	/// Gets or sets the duplicate detection window.
	/// </summary>
	/// <value>The current <see cref="DuplicateDetectionWindow"/> value.</value>
	public TimeSpan? DuplicateDetectionWindow { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the queue requires sessions.
	/// </summary>
	/// <value>The current <see cref="RequiresSession"/> value.</value>
	public bool? RequiresSession { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether dead lettering is enabled on message expiration.
	/// </summary>
	/// <value>The current <see cref="DeadLetteringOnMessageExpiration"/> value.</value>
	public bool? DeadLetteringOnMessageExpiration { get; set; }

	/// <summary>
	/// Gets or sets the maximum delivery count before dead lettering.
	/// </summary>
	/// <value>The current <see cref="MaxDeliveryCount"/> value.</value>
	[Range(1, int.MaxValue)]
	public int? MaxDeliveryCount { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether partitioning is enabled.
	/// </summary>
	/// <value>The current <see cref="EnablePartitioning"/> value.</value>
	public bool? EnablePartitioning { get; set; }

	/// <summary>
	/// Gets additional provider-specific properties.
	/// </summary>
	/// <value>The current <see cref="Properties"/> value.</value>
	public Dictionary<string, object> Properties { get; init; } = [];
}
