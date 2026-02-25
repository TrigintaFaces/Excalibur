// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Service Bus-specific CloudEvent this.configuration options.
/// </summary>
public sealed class AzureServiceBusCloudEventOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to use Service Bus sessions for CloudEvents.
	/// </summary>
	/// <remarks> When enabled, CloudEvents will include session IDs for ordered processing. </remarks>
	/// <value>
	/// A value indicating whether to use Service Bus sessions for CloudEvents.
	/// </value>
	public bool UseSessionsForOrdering { get; set; }

	/// <summary>
	/// Gets or sets the default session ID for Service Bus messages.
	/// </summary>
	/// <value>
	/// The default session ID for Service Bus messages.
	/// </value>
	public string? DefaultSessionId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable duplicate detection for CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable duplicate detection for CloudEvents.
	/// </value>
	public bool EnableDuplicateDetection { get; set; } = true;

	/// <summary>
	/// Gets or sets the duplicate detection window for CloudEvents.
	/// </summary>
	/// <value>
	/// The duplicate detection window for CloudEvents.
	/// </value>
	public TimeSpan DuplicateDetectionWindow { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Gets or sets a value indicating whether to use partition keys for load balancing.
	/// </summary>
	/// <value>
	/// A value indicating whether to use partition keys for load balancing.
	/// </value>
	public bool UsePartitionKeys { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum message size for Service Bus CloudEvents.
	/// </summary>
	/// <remarks> Service Bus Standard tier supports up to 256KB messages, Premium tier supports up to 1MB. </remarks>
	/// <value>
	/// The maximum message size for Service Bus CloudEvents.
	/// </value>
	public long MaxMessageSizeBytes { get; set; } = 256 * 1024; // 256KB default

	/// <summary>
	/// Gets or sets a value indicating whether to enable message scheduling for CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable message scheduling for CloudEvents.
	/// </value>
	public bool EnableScheduledDelivery { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable dead letter queue for failed CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable dead letter queue for failed CloudEvents.
	/// </value>
	public bool EnableDeadLetterQueue { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum delivery count before moving to dead letter queue.
	/// </summary>
	/// <value>
	/// The maximum delivery count before moving to dead letter queue.
	/// </value>
	public int MaxDeliveryCount { get; set; } = 10;

	/// <summary>
	/// Gets or sets the time-to-live for CloudEvent messages.
	/// </summary>
	/// <value>
	/// The time-to-live for CloudEvent messages.
	/// </value>
	public TimeSpan? TimeToLive { get; set; } = TimeSpan.FromDays(14);
}
