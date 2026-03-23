// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// AWS SQS-specific mapping context for configuring message properties.
/// </summary>
public interface IAwsSqsMappingContext
{
	/// <summary>
	/// Gets or sets the queue URL.
	/// </summary>
	string? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the message group ID (for FIFO queues).
	/// </summary>
	string? MessageGroupId { get; set; }

	/// <summary>
	/// Gets or sets the message deduplication ID (for FIFO queues).
	/// </summary>
	string? MessageDeduplicationId { get; set; }

	/// <summary>
	/// Gets or sets the delay in seconds before the message becomes visible.
	/// </summary>
	int? DelaySeconds { get; set; }

	/// <summary>
	/// Sets a message attribute.
	/// </summary>
	/// <param name="name">The attribute name.</param>
	/// <param name="value">The attribute value.</param>
	/// <param name="dataType">The attribute data type (String, Number, Binary).</param>
	void SetAttribute(string name, string value, string dataType = "String");
}
