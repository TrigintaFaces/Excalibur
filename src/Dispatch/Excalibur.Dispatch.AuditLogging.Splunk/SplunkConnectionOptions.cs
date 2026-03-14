// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Connection and endpoint options for the Splunk HEC exporter.
/// </summary>
/// <remarks>
/// Follows the <c>OtlpExporterOptions</c> pattern of separating endpoint/protocol from batching configuration.
/// </remarks>
public sealed class SplunkConnectionOptions
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
	/// Gets or sets whether to validate the SSL certificate of the HEC endpoint.
	/// Defaults to true. Set to false only for development/testing.
	/// </summary>
	public bool ValidateCertificate { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to enable gzip compression for requests.
	/// Defaults to true for batch requests.
	/// </summary>
	public bool EnableCompression { get; set; } = true;
}
