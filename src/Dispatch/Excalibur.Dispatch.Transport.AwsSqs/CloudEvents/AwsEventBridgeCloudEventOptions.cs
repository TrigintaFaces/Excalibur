// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS EventBridge-specific CloudEvent configuration options.
/// </summary>
public sealed class AwsEventBridgeCloudEventOptions
{
	/// <summary>
	/// Gets or sets the EventBridge event bus name for CloudEvents.
	/// </summary>
	/// <value>
	/// The EventBridge event bus name for CloudEvents.
	/// </value>
	public string EventBusName { get; set; } = "default";

	/// <summary>
	/// Gets or sets the source prefix for EventBridge events.
	/// </summary>
	/// <remarks> EventBridge events require a source identifier. This prefix is combined with the CloudEvent source. </remarks>
	/// <value>
	/// The source prefix for EventBridge events.
	/// </value>
	public string SourcePrefix { get; set; } = "dispatch.cloudevents";

	/// <summary>
	/// Gets or sets a value indicating whether to use CloudEvent type as EventBridge detail-type.
	/// </summary>
	/// <value>
	/// A value indicating whether to use CloudEvent type as EventBridge detail-type.
	/// </value>
	public bool UseCloudEventTypeAsDetailType { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include all CloudEvent extension attributes in EventBridge detail.
	/// </summary>
	/// <value>
	/// A value indicating whether to include all CloudEvent extension attributes in EventBridge detail.
	/// </value>
	public bool IncludeExtensionsInDetail { get; set; } = true;

}
