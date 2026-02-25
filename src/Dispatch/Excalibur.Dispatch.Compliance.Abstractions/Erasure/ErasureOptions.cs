// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Text;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for erasure processing.
/// </summary>
/// <remarks>
/// The default grace period is 72 hours to allow for:
/// - Cancellation of mistaken requests
/// - Legal hold integration
/// - Operational recovery
/// </remarks>
public sealed class ErasureOptions
{
	private static readonly CompositeFormat DefaultGracePeriodLessThanMinimumFormat =
			CompositeFormat.Parse(Resources.ErasureOptions_DefaultGracePeriodLessThanMinimum);

	private static readonly CompositeFormat DefaultGracePeriodExceedsMaximumFormat =
			CompositeFormat.Parse(Resources.ErasureOptions_DefaultGracePeriodExceedsMaximum);

	/// <summary>
	/// Gets or sets the default grace period before key deletion.
	/// Default: 72 hours.
	/// </summary>
	public TimeSpan DefaultGracePeriod { get; set; } = TimeSpan.FromHours(72);

	/// <summary>
	/// Gets or sets the minimum grace period (cannot be shorter).
	/// Default: 1 hour.
	/// </summary>
	public TimeSpan MinimumGracePeriod { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the maximum grace period (cannot exceed GDPR deadline).
	/// Default: 30 days.
	/// </summary>
	public TimeSpan MaximumGracePeriod { get; set; } = TimeSpan.FromDays(30);

	/// <summary>
	/// Gets or sets whether to automatically discover personal data.
	/// Default: true.
	/// </summary>
	public bool EnableAutoDiscovery { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to require verification before issuing certificate.
	/// Default: true.
	/// </summary>
	public bool RequireVerification { get; set; } = true;

	/// <summary>
	/// Gets or sets the verification methods to use.
	/// Default: AuditLog | KeyManagementSystem.
	/// </summary>
	public VerificationMethod VerificationMethods { get; set; } =
		VerificationMethod.AuditLog | VerificationMethod.KeyManagementSystem;

	/// <summary>
	/// Gets or sets whether to notify data subject upon completion.
	/// Default: true.
	/// </summary>
	public bool NotifyOnCompletion { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to allow emergency immediate erasure (bypasses grace period).
	/// Requires elevated permissions.
	/// Default: false.
	/// </summary>
	public bool AllowImmediateErasure { get; set; }

	/// <summary>
	/// Gets or sets retention-related options (certificate retention, signing keys).
	/// </summary>
	public ErasureRetentionOptions Retention { get; set; } = new();

	/// <summary>
	/// Gets or sets execution-related options (batch size, retry policy).
	/// </summary>
	public ErasureExecutionOptions Execution { get; set; } = new();

	// --- Backward-compatible shims that delegate to sub-options ---

	/// <summary>
	/// Gets or sets the retention period for erasure certificates.
	/// Default: 7 years (regulatory requirement).
	/// </summary>
	public TimeSpan CertificateRetentionPeriod { get => Retention.CertificateRetentionPeriod; set => Retention.CertificateRetentionPeriod = value; }

	/// <summary>
	/// Gets or sets the signing key identifier for certificate signatures.
	/// </summary>
	public string? SigningKeyId { get => Retention.SigningKeyId; set => Retention.SigningKeyId = value; }

	/// <summary>
	/// Gets or sets the batch size for erasure operations.
	/// Default: 100 keys per batch.
	/// </summary>
	public int BatchSize { get => Execution.BatchSize; set => Execution.BatchSize = value; }

	/// <summary>
	/// Gets or sets the maximum retry attempts for failed erasures.
	/// Default: 3.
	/// </summary>
	public int MaxRetryAttempts { get => Execution.MaxRetryAttempts; set => Execution.MaxRetryAttempts = value; }

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// Default: 30 seconds.
	/// </summary>
	public TimeSpan RetryDelay { get => Execution.RetryDelay; set => Execution.RetryDelay = value; }

	/// <summary>
	/// Validates the configured options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when options are invalid.</exception>
	public void Validate()
	{
		if (MinimumGracePeriod < TimeSpan.Zero)
		{
			throw new InvalidOperationException(Resources.ErasureOptions_MinimumGracePeriodNegative);
		}

		if (DefaultGracePeriod < MinimumGracePeriod)
		{
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.CurrentCulture,
							DefaultGracePeriodLessThanMinimumFormat,
							DefaultGracePeriod,
							MinimumGracePeriod));
		}

		if (DefaultGracePeriod > MaximumGracePeriod)
		{
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.CurrentCulture,
							DefaultGracePeriodExceedsMaximumFormat,
							DefaultGracePeriod,
							MaximumGracePeriod));
		}

		if (MaximumGracePeriod > TimeSpan.FromDays(30))
		{
			throw new InvalidOperationException(Resources.ErasureOptions_MaximumGracePeriodExceedsDeadline);
		}

		if (Execution.BatchSize < 1)
		{
			throw new InvalidOperationException(Resources.ErasureOptions_BatchSizeTooSmall);
		}

		if (Execution.MaxRetryAttempts < 0)
		{
			throw new InvalidOperationException(Resources.ErasureOptions_MaxRetryAttemptsNegative);
		}
	}
}

/// <summary>
/// Retention-related options for erasure processing.
/// </summary>
public sealed class ErasureRetentionOptions
{
	/// <summary>
	/// Gets or sets the retention period for erasure certificates.
	/// Default: 7 years (regulatory requirement).
	/// </summary>
	public TimeSpan CertificateRetentionPeriod { get; set; } = TimeSpan.FromDays(365 * 7);

	/// <summary>
	/// Gets or sets the signing key identifier for certificate signatures.
	/// </summary>
	public string? SigningKeyId { get; set; }
}

/// <summary>
/// Execution-related options for erasure processing (batch size, retry policy).
/// </summary>
public sealed class ErasureExecutionOptions
{
	/// <summary>
	/// Gets or sets the batch size for erasure operations.
	/// Default: 100 keys per batch.
	/// </summary>
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum retry attempts for failed erasures.
	/// Default: 3.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// Default: 30 seconds.
	/// </summary>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}
