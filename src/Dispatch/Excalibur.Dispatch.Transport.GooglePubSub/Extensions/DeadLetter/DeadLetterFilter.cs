// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Filter for dead letter messages based on criteria.
/// </summary>
public sealed class DeadLetterFilter
{
	/// <summary>
	/// Gets or sets the message type filter.
	/// </summary>
	/// <value>
	/// The message type filter.
	/// </value>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the minimum age filter.
	/// </summary>
	/// <value>
	/// The minimum age filter.
	/// </value>
	public TimeSpan? MinAge { get; set; }

	/// <summary>
	/// Gets or sets the maximum age filter.
	/// </summary>
	/// <value>
	/// The maximum age filter.
	/// </value>
	public TimeSpan? MaxAge { get; set; }

	/// <summary>
	/// Gets or sets the poison reason filter.
	/// </summary>
	/// <value>
	/// The poison reason filter.
	/// </value>
	public PoisonReason? Reason { get; set; }

	/// <summary>
	/// Gets or sets attribute filters.
	/// </summary>
	/// <value>
	/// Attribute filters.
	/// </value>
	public Dictionary<string, string> AttributeFilters { get; set; } = [];

	/// <summary>
	/// Determines if a message matches this filter.
	/// </summary>
	/// <param name="message"> The message to check. </param>
	/// <param name="reason"> The poison reason. </param>
	/// <returns> True if the message matches the filter; otherwise, false. </returns>
	public bool Matches(PubSubMessage message, PoisonReason reason)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Check message type
		if (MessageType != null &&
			message.Attributes.TryGetValue("messageType", out var messageType) &&
!string.Equals(messageType, MessageType, StringComparison.Ordinal))
		{
			return false;
		}

		// Check age
		var age = DateTimeOffset.UtcNow - message.PublishTime;
		if (MinAge.HasValue && age < MinAge.Value)
		{
			return false;
		}

		if (MaxAge.HasValue && age > MaxAge.Value)
		{
			return false;
		}

		// Check reason
		if (Reason.HasValue && reason != Reason.Value)
		{
			return false;
		}

		// Check attribute filters
		foreach (var filter in AttributeFilters)
		{
			if (!message.Attributes.TryGetValue(filter.Key, out var value) || !string.Equals(value, filter.Value, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}
}
