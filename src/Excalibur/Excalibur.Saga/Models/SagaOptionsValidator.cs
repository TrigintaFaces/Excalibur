// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Saga;

/// <summary>
/// Validates <see cref="SagaOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks that cannot be expressed via
/// <see cref="System.ComponentModel.DataAnnotations"/> attributes alone.
/// </remarks>
public sealed class SagaOptionsValidator : IValidateOptions<SagaOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SagaOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// MaxRetryAttempts must be non-negative
		if (options.MaxRetryAttempts < 0)
		{
			failures.Add($"{nameof(SagaOptions.MaxRetryAttempts)} must be >= 0 (was {options.MaxRetryAttempts}).");
		}

		// MaxConcurrency must be positive
		if (options.MaxConcurrency < 1)
		{
			failures.Add($"{nameof(SagaOptions.MaxConcurrency)} must be >= 1 (was {options.MaxConcurrency}).");
		}

		// DefaultTimeout must be positive
		if (options.DefaultTimeout <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(SagaOptions.DefaultTimeout)} must be positive (was {options.DefaultTimeout}).");
		}

		// RetryDelay must be non-negative
		if (options.RetryDelay < TimeSpan.Zero)
		{
			failures.Add($"{nameof(SagaOptions.RetryDelay)} must be non-negative (was {options.RetryDelay}).");
		}

		// Cross-property: RetryDelay should not exceed DefaultTimeout
		if (options.MaxRetryAttempts > 0 && options.RetryDelay >= options.DefaultTimeout)
		{
			failures.Add(
				$"{nameof(SagaOptions.RetryDelay)} ({options.RetryDelay}) must be less than " +
				$"{nameof(SagaOptions.DefaultTimeout)} ({options.DefaultTimeout}) when retries are enabled.");
		}

		// SagaRetentionPeriod must be positive
		if (options.SagaRetentionPeriod <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(SagaOptions.SagaRetentionPeriod)} must be positive (was {options.SagaRetentionPeriod}).");
		}

		// CleanupInterval must be positive when cleanup is enabled
		if (options.EnableAutomaticCleanup && options.CleanupInterval <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(SagaOptions.CleanupInterval)} must be positive when " +
				$"{nameof(SagaOptions.EnableAutomaticCleanup)} is true (was {options.CleanupInterval}).");
		}

		// Fail-loud guard for the carved-but-not-yet-enforced automatic-cleanup toggle (ADR-336 — never
		// advertised-but-inert). Enforcement lives in a cross-provider saga-store purge-by-age contract.
		// Defaults to false; an explicit opt-in must fail loudly rather than no-op.
		// (Optimistic concurrency is always enforced unconditionally, so it carries no opt-in toggle.)
		if (options.EnableAutomaticCleanup)
		{
			failures.Add(
				$"{nameof(SagaOptions.EnableAutomaticCleanup)} is set but automatic saga cleanup is not yet enforced " +
				"(cross-provider saga-store purge-by-age is not yet implemented). Leave it false until that lands.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
