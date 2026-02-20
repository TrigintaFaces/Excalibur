// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Validates <see cref="ClaimCheckOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks including:
/// <list type="bullet">
///   <item><description>PayloadThreshold must be positive</description></item>
///   <item><description>CompressionThreshold must be less than PayloadThreshold when compression is enabled</description></item>
///   <item><description>CleanupInterval and DefaultTtl must be positive when cleanup is enabled</description></item>
///   <item><description>MinCompressionRatio must be between 0.0 and 1.0</description></item>
/// </list>
/// </remarks>
public sealed class ClaimCheckOptionsValidator : IValidateOptions<ClaimCheckOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ClaimCheckOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.PayloadThreshold <= 0)
		{
			failures.Add($"{nameof(ClaimCheckOptions.PayloadThreshold)} must be greater than 0 (was {options.PayloadThreshold}).");
		}

		// Compression cross-property constraints
		if (options.Compression.EnableCompression)
		{
			if (options.Compression.CompressionThreshold < 0)
			{
				failures.Add($"{nameof(ClaimCheckCompressionOptions.CompressionThreshold)} must not be negative (was {options.Compression.CompressionThreshold}).");
			}

			if (options.PayloadThreshold > 0 && options.Compression.CompressionThreshold >= options.PayloadThreshold)
			{
				failures.Add(
					$"{nameof(ClaimCheckCompressionOptions.CompressionThreshold)} ({options.Compression.CompressionThreshold}) " +
					$"must be less than {nameof(ClaimCheckOptions.PayloadThreshold)} ({options.PayloadThreshold}).");
			}
		}

		if (options.Compression.MinCompressionRatio is < 0.0 or > 1.0)
		{
			failures.Add($"{nameof(ClaimCheckCompressionOptions.MinCompressionRatio)} must be between 0.0 and 1.0 (was {options.Compression.MinCompressionRatio}).");
		}

		// Cleanup cross-property constraints
		if (options.Cleanup.EnableCleanup)
		{
			if (options.Cleanup.CleanupInterval <= TimeSpan.Zero)
			{
				failures.Add($"{nameof(ClaimCheckCleanupOptions.CleanupInterval)} must be positive when cleanup is enabled (was {options.Cleanup.CleanupInterval}).");
			}

			if (options.Cleanup.DefaultTtl <= TimeSpan.Zero)
			{
				failures.Add($"{nameof(ClaimCheckCleanupOptions.DefaultTtl)} must be positive when cleanup is enabled (was {options.Cleanup.DefaultTtl}).");
			}

			if (options.Cleanup.CleanupBatchSize <= 0)
			{
				failures.Add($"{nameof(ClaimCheckCleanupOptions.CleanupBatchSize)} must be greater than 0 (was {options.Cleanup.CleanupBatchSize}).");
			}

			if (options.Cleanup.CleanupInterval > TimeSpan.Zero && options.Cleanup.DefaultTtl > TimeSpan.Zero &&
				options.Cleanup.CleanupInterval >= options.Cleanup.DefaultTtl)
			{
				failures.Add(
					$"{nameof(ClaimCheckCleanupOptions.CleanupInterval)} ({options.Cleanup.CleanupInterval}) " +
					$"must be less than {nameof(ClaimCheckCleanupOptions.DefaultTtl)} ({options.Cleanup.DefaultTtl}).");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
