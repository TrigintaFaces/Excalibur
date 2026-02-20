// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SNS-specific CloudEvent configuration options.
/// </summary>
public sealed class AwsSnsCloudEventOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to include SNS message attributes for binary mode CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to include SNS message attributes for binary mode CloudEvents.
	/// </value>
	public bool IncludeMessageAttributes { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable message filtering based on CloudEvent attributes.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable message filtering based on CloudEvent attributes.
	/// </value>
	public bool EnableMessageFiltering { get; set; }

	/// <summary>
	/// Gets or sets the subject for SNS messages containing CloudEvents.
	/// </summary>
	/// <value>
	/// The subject for SNS messages containing CloudEvents.
	/// </value>
	public string? DefaultSubject { get; set; } = "CloudEvent";

	/// <summary>
	/// Gets or sets a value indicating whether to enable FIFO topic features for CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable FIFO topic features for CloudEvents.
	/// </value>
	public bool UseFifoFeatures { get; set; }

	/// <summary>
	/// Gets or sets the default message group ID for FIFO topics.
	/// </summary>
	/// <value>
	/// The default message group ID for FIFO topics.
	/// </value>
	public string? DefaultMessageGroupId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable content-based deduplication for FIFO topics.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable content-based deduplication for FIFO topics.
	/// </value>
	public bool EnableContentBasedDeduplication { get; set; }
}
