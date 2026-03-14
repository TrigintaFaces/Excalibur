// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Batching and retry options for the Splunk HEC exporter.
/// </summary>
/// <remarks>
/// Follows the <c>OtlpExporterOptions</c> pattern of separating batching from endpoint configuration.
/// </remarks>
public sealed class SplunkBatchOptions
{
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
}
