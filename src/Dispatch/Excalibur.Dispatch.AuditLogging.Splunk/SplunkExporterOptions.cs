// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Configuration options for the Splunk HTTP Event Collector (HEC) exporter.
/// </summary>
public sealed class SplunkExporterOptions
{
	/// <summary>
	/// Gets or sets the Splunk HEC endpoint URL (e.g., "https://splunk.example.com:8088/services/collector").
	/// </summary>
	[Required]
	public required Uri HecEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the HEC authentication token.
	/// </summary>
	[Required]
	public required string HecToken { get; set; }

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
	/// Gets or sets the maximum batch size for batch exports.
	/// Defaults to 100.
	/// </summary>
	[Range(1, 10000)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the timeout for HTTP requests.
	/// Defaults to 30 seconds.
	/// </summary>
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// Defaults to 3.
	/// </summary>
	[Range(0, 10)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the initial delay for exponential backoff.
	/// Defaults to 1 second.
	/// </summary>
	public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets whether to enable gzip compression for requests.
	/// Defaults to true for batch requests.
	/// </summary>
	public bool EnableCompression { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to validate the SSL certificate of the HEC endpoint.
	/// Defaults to true. Set to false only for development/testing.
	/// </summary>
	public bool ValidateCertificate { get; set; } = true;

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
}
