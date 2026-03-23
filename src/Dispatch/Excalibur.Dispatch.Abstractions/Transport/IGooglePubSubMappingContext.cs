// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Google Pub/Sub-specific mapping context for configuring message properties.
/// </summary>
public interface IGooglePubSubMappingContext
{
	/// <summary>
	/// Gets or sets the topic name.
	/// </summary>
	string? TopicName { get; set; }

	/// <summary>
	/// Gets or sets the ordering key for ordered delivery.
	/// </summary>
	string? OrderingKey { get; set; }

	/// <summary>
	/// Sets a custom attribute on the message.
	/// </summary>
	/// <param name="key">The attribute key.</param>
	/// <param name="value">The attribute value.</param>
	void SetAttribute(string key, string value);
}
