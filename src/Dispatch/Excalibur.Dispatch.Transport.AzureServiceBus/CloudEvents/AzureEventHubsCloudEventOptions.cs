// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Event Hubs-specific CloudEvent configuration options.
/// </summary>
public sealed class AzureEventHubsCloudEventOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to use partition keys for Event Hubs CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to use partition keys for Event Hubs CloudEvents.
	/// </value>
	public bool UsePartitionKeys { get; set; } = true;

	/// <summary>
	/// Gets or sets the default partition key strategy.
	/// </summary>
	/// <value>
	/// The default partition key strategy.
	/// </value>
	public PartitionKeyStrategy PartitionKeyStrategy { get; set; } = PartitionKeyStrategy.CorrelationId;

	/// <summary>
	/// Gets or sets the maximum batch size for Event Hubs CloudEvents.
	/// </summary>
	/// <remarks> Event Hubs supports up to 100 events per batch or 1MB total size, whichever is reached first. </remarks>
	/// <value>
	/// The maximum batch size for Event Hubs CloudEvents.
	/// </value>
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum batch size in bytes.
	/// </summary>
	/// <value>
	/// The maximum batch size in bytes.
	/// </value>
	public long MaxBatchSizeBytes { get; set; } = 1024 * 1024; // 1MB

	/// <summary>
	/// Gets or sets a value indicating whether to enable capture for CloudEvents.
	/// </summary>
	/// <remarks> When enabled, Event Hubs will capture CloudEvents to Azure Storage for archival. </remarks>
	/// <value>
	/// A value indicating whether to enable capture for CloudEvents.
	/// </value>
	public bool EnableCapture { get; set; }

	/// <summary>
	/// Gets or sets the capture file name format.
	/// </summary>
	/// <value>
	/// The capture file name format.
	/// </value>
	public string CaptureFileNameFormat { get; set; } =
		"cloudevents/{Namespace}/{EventHub}/{PartitionId}/{Year}/{Month}/{Day}/{Hour}/{Minute}/{Second}";

	/// <summary>
	/// Gets or sets a value indicating whether to include schema registry information for CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to include schema registry information for CloudEvents.
	/// </value>
	public bool UseSchemaRegistry { get; set; }

	/// <summary>
	/// Gets or sets the schema registry Excalibur.Dispatch.Transport.Aws.Advanced.SessionManagement.
	/// </summary>
	/// <value>
	/// The schema registry Excalibur.Dispatch.Transport.Aws.Advanced.SessionManagement.
	/// </value>
	public string? SchemaRegistryNamespace { get; set; }
}
