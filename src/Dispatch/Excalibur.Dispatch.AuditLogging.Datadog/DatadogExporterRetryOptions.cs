// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.AuditLogging.Datadog;

/// <summary>
/// Retry and resilience configuration for Datadog audit log exporter.
/// </summary>
public sealed class DatadogExporterRetryOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retries.
	/// </summary>
	/// <remarks>
	/// Actual delay uses exponential backoff: baseDelay * 2^(attempt-1).
	/// </remarks>
	public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the HTTP request timeout.
	/// </summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
