// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Validates <see cref="OutboxMiddlewareOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
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
internal sealed class OutboxMiddlewareOptionsValidator : IValidateOptions<OutboxMiddlewareOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OutboxMiddlewareOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.PublishBatchSize <= 0)
		{
			failures.Add($"{nameof(OutboxMiddlewareOptions.PublishBatchSize)} must be greater than 0 (was {options.PublishBatchSize}).");
		}

		if (options.PublishPollingInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxMiddlewareOptions.PublishPollingInterval)} must be positive (was {options.PublishPollingInterval}).");
		}

		if (options.Retry.MaxRetries < 0)
		{
			failures.Add($"{nameof(OutboxMiddlewareRetryOptions)}.{nameof(OutboxMiddlewareRetryOptions.MaxRetries)} must be non-negative (was {options.Retry.MaxRetries}).");
		}

		if (options.Retry.RetryDelay <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxMiddlewareRetryOptions)}.{nameof(OutboxMiddlewareRetryOptions.RetryDelay)} must be positive (was {options.Retry.RetryDelay}).");
		}

		if (options.CleanupAge <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxMiddlewareOptions.CleanupAge)} must be positive (was {options.CleanupAge}).");
		}

		if (options.CleanupInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxMiddlewareOptions.CleanupInterval)} must be positive (was {options.CleanupInterval}).");
		}

		// Cross-property: MaxRetryDelay >= RetryDelay when exponential backoff is enabled
		if (options.Retry.EnableExponentialRetryBackoff)
		{
			if (options.Retry.MaxRetryDelay <= TimeSpan.Zero)
			{
				failures.Add($"{nameof(OutboxMiddlewareRetryOptions)}.{nameof(OutboxMiddlewareRetryOptions.MaxRetryDelay)} must be positive when exponential backoff is enabled (was {options.Retry.MaxRetryDelay}).");
			}
			else if (options.Retry.RetryDelay > TimeSpan.Zero && options.Retry.MaxRetryDelay < options.Retry.RetryDelay)
			{
				failures.Add(
					$"{nameof(OutboxMiddlewareRetryOptions)}.{nameof(OutboxMiddlewareRetryOptions.MaxRetryDelay)} ({options.Retry.MaxRetryDelay}) " +
					$"must be greater than or equal to {nameof(OutboxMiddlewareRetryOptions)}.{nameof(OutboxMiddlewareRetryOptions.RetryDelay)} ({options.Retry.RetryDelay}) when exponential backoff is enabled.");
			}
		}

		// Cross-property: MinPollingInterval <= PublishPollingInterval when adaptive polling is enabled
		if (options.AdaptivePolling.EnableAdaptivePolling)
		{
			if (options.AdaptivePolling.MinPollingInterval <= TimeSpan.Zero)
			{
				failures.Add($"{nameof(OutboxMiddlewareAdaptivePollingOptions)}.{nameof(OutboxMiddlewareAdaptivePollingOptions.MinPollingInterval)} must be positive when adaptive polling is enabled (was {options.AdaptivePolling.MinPollingInterval}).");
			}
			else if (options.PublishPollingInterval > TimeSpan.Zero && options.AdaptivePolling.MinPollingInterval > options.PublishPollingInterval)
			{
				failures.Add(
					$"{nameof(OutboxMiddlewareAdaptivePollingOptions)}.{nameof(OutboxMiddlewareAdaptivePollingOptions.MinPollingInterval)} ({options.AdaptivePolling.MinPollingInterval}) " +
					$"must be less than or equal to {nameof(OutboxMiddlewareOptions.PublishPollingInterval)} ({options.PublishPollingInterval}) when adaptive polling is enabled.");
			}

			if (options.AdaptivePolling.AdaptivePollingBackoffMultiplier <= 1.0)
			{
				failures.Add($"{nameof(OutboxMiddlewareAdaptivePollingOptions)}.{nameof(OutboxMiddlewareAdaptivePollingOptions.AdaptivePollingBackoffMultiplier)} must be greater than 1.0 (was {options.AdaptivePolling.AdaptivePollingBackoffMultiplier}).");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
