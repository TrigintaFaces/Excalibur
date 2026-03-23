// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Kafka-specific mapping context for configuring message properties.
/// </summary>
public interface IKafkaMappingContext
{
	/// <summary>
	/// Gets or sets the topic name.
	/// </summary>
	string? Topic { get; set; }

	/// <summary>
	/// Gets or sets the message key (used for partitioning).
	/// </summary>
	string? Key { get; set; }

	/// <summary>
	/// Gets or sets the target partition (or null for automatic partitioning).
	/// </summary>
	int? Partition { get; set; }

	/// <summary>
	/// Gets or sets the schema ID for schema registry integration.
	/// </summary>
	int? SchemaId { get; set; }

	/// <summary>
	/// Sets a custom header on the message.
	/// </summary>
	/// <param name="key">The header key.</param>
	/// <param name="value">The header value.</param>
	void SetHeader(string key, string value);
}
