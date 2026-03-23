// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// AWS SNS-specific mapping context for configuring message properties.
/// </summary>
public interface IAwsSnsMappingContext
{
	/// <summary>
	/// Gets or sets the topic ARN.
	/// </summary>
	string? TopicArn { get; set; }

	/// <summary>
	/// Gets or sets the message group ID (for FIFO topics).
	/// </summary>
	string? MessageGroupId { get; set; }

	/// <summary>
	/// Gets or sets the message deduplication ID (for FIFO topics).
	/// </summary>
	string? MessageDeduplicationId { get; set; }

	/// <summary>
	/// Gets or sets the subject for email endpoints.
	/// </summary>
	string? Subject { get; set; }

	/// <summary>
	/// Sets a message attribute.
	/// </summary>
	/// <param name="name">The attribute name.</param>
	/// <param name="value">The attribute value.</param>
	/// <param name="dataType">The attribute data type (String, Number, Binary).</param>
	void SetAttribute(string name, string value, string dataType = "String");
}
