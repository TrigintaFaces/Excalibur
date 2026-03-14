// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Retry configuration for Azure Key Vault operations.
/// </summary>
public sealed class AzureKeyVaultRetryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic retry with exponential backoff. Default is true.
	/// </summary>
	public bool EnableRetry { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures. Default is 3.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the initial delay for exponential backoff. Default is 1 second.
	/// </summary>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
