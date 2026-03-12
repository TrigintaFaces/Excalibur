// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Configuration options for the Splunk HTTP Event Collector (HEC) exporter.
/// </summary>
/// <remarks>
/// <para>
/// Connection/endpoint properties are in <see cref="Connection"/> and batching/retry properties are in <see cref="Batch"/>.
/// This follows the <c>OtlpExporterOptions</c> pattern of separating endpoint/protocol from batching configuration.
/// </para>
/// </remarks>
public sealed class SplunkExporterOptions
{
	/// <summary>
	/// Gets or sets the Splunk index to send events to.
	/// If null, uses the default index configured for the HEC token.
	/// </summary>
	public string? Index { get; set; }

	/// <summary>
	/// Gets or sets the source type for audit events.
	/// Defaults to "audit:dispatch".
	/// </summary>
	public string SourceType { get; set; } = "audit:dispatch";

	/// <summary>
	/// Gets or sets the source identifier for audit events.
	/// Defaults to the application name if not specified.
	/// </summary>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the host identifier for audit events.
	/// Defaults to the machine name if not specified.
	/// </summary>
	public string? Host { get; set; }

	/// <summary>
	/// Gets or sets whether acknowledgment is required from Splunk.
	/// When true, waits for indexer acknowledgment before returning success.
	/// Defaults to false for better performance.
	/// </summary>
	public bool UseAck { get; set; }

	/// <summary>
	/// Gets or sets the channel identifier for indexed acknowledgment.
	/// Required when UseAck is true.
	/// </summary>
	public string? Channel { get; set; }

	/// <summary>
	/// Gets or sets the connection and endpoint options.
	/// </summary>
	/// <value> The Splunk connection options. </value>
	public SplunkConnectionOptions Connection { get; set; } = new()
	{
		HecEndpoint = new Uri("https://localhost:8088/services/collector"),
		HecToken = string.Empty
	};

	/// <summary>
	/// Gets or sets the batching and retry options.
	/// </summary>
	/// <value> The Splunk batch options. </value>
	public SplunkBatchOptions Batch { get; set; } = new();
}
