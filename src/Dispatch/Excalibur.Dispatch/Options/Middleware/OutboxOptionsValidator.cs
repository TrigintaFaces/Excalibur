// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Validates <see cref="OutboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks including:
/// <list type="bullet">
///   <item><description>PublishBatchSize must be positive</description></item>
///   <item><description>PublishPollingInterval must be positive</description></item>
///   <item><description>MaxRetries must be non-negative</description></item>
///   <item><description>RetryDelay must be positive</description></item>
///   <item><description>MaxRetryDelay must be greater than or equal to RetryDelay when exponential backoff is enabled</description></item>
///   <item><description>MinPollingInterval must be less than or equal to PublishPollingInterval when adaptive polling is enabled</description></item>
///   <item><description>AdaptivePollingBackoffMultiplier must be greater than 1.0 when adaptive polling is enabled</description></item>
/// </list>
/// </remarks>
public sealed class OutboxOptionsValidator : IValidateOptions<OutboxOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OutboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.PublishBatchSize <= 0)
		{
			failures.Add($"{nameof(OutboxOptions.PublishBatchSize)} must be greater than 0 (was {options.PublishBatchSize}).");
		}

		if (options.PublishPollingInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxOptions.PublishPollingInterval)} must be positive (was {options.PublishPollingInterval}).");
		}

		if (options.MaxRetries < 0)
		{
			failures.Add($"{nameof(OutboxOptions.MaxRetries)} must be non-negative (was {options.MaxRetries}).");
		}

		if (options.RetryDelay <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxOptions.RetryDelay)} must be positive (was {options.RetryDelay}).");
		}

		if (options.CleanupAge <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxOptions.CleanupAge)} must be positive (was {options.CleanupAge}).");
		}

		if (options.CleanupInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxOptions.CleanupInterval)} must be positive (was {options.CleanupInterval}).");
		}

		// Cross-property: MaxRetryDelay >= RetryDelay when exponential backoff is enabled
		if (options.EnableExponentialRetryBackoff)
		{
			if (options.MaxRetryDelay <= TimeSpan.Zero)
			{
				failures.Add($"{nameof(OutboxOptions.MaxRetryDelay)} must be positive when exponential backoff is enabled (was {options.MaxRetryDelay}).");
			}
			else if (options.RetryDelay > TimeSpan.Zero && options.MaxRetryDelay < options.RetryDelay)
			{
				failures.Add(
					$"{nameof(OutboxOptions.MaxRetryDelay)} ({options.MaxRetryDelay}) " +
					$"must be greater than or equal to {nameof(OutboxOptions.RetryDelay)} ({options.RetryDelay}) when exponential backoff is enabled.");
			}
		}

		// Cross-property: MinPollingInterval <= PublishPollingInterval when adaptive polling is enabled
		if (options.EnableAdaptivePolling)
		{
			if (options.MinPollingInterval <= TimeSpan.Zero)
			{
				failures.Add($"{nameof(OutboxOptions.MinPollingInterval)} must be positive when adaptive polling is enabled (was {options.MinPollingInterval}).");
			}
			else if (options.PublishPollingInterval > TimeSpan.Zero && options.MinPollingInterval > options.PublishPollingInterval)
			{
				failures.Add(
					$"{nameof(OutboxOptions.MinPollingInterval)} ({options.MinPollingInterval}) " +
					$"must be less than or equal to {nameof(OutboxOptions.PublishPollingInterval)} ({options.PublishPollingInterval}) when adaptive polling is enabled.");
			}

			if (options.AdaptivePollingBackoffMultiplier <= 1.0)
			{
				failures.Add($"{nameof(OutboxOptions.AdaptivePollingBackoffMultiplier)} must be greater than 1.0 (was {options.AdaptivePollingBackoffMultiplier}).");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
