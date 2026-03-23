// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Azure Service Bus-specific mapping context for configuring message properties.
/// </summary>
public interface IAzureServiceBusMappingContext
{
	/// <summary>
	/// Gets or sets the topic or queue name.
	/// </summary>
	string? TopicOrQueueName { get; set; }

	/// <summary>
	/// Gets or sets the session ID for session-enabled entities.
	/// </summary>
	string? SessionId { get; set; }

	/// <summary>
	/// Gets or sets the partition key.
	/// </summary>
	string? PartitionKey { get; set; }

	/// <summary>
	/// Gets or sets the reply-to session ID.
	/// </summary>
	string? ReplyToSessionId { get; set; }

	/// <summary>
	/// Gets or sets the time to live for the message.
	/// </summary>
	TimeSpan? TimeToLive { get; set; }

	/// <summary>
	/// Gets or sets the scheduled enqueue time.
	/// </summary>
	DateTimeOffset? ScheduledEnqueueTime { get; set; }

	/// <summary>
	/// Sets a custom property on the message.
	/// </summary>
	/// <param name="key">The property key.</param>
	/// <param name="value">The property value.</param>
	void SetProperty(string key, object value);
}
