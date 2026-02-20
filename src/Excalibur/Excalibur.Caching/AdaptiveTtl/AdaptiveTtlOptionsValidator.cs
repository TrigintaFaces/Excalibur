// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Validates <see cref="AdaptiveTtlOptions"/> cross-property constraints that cannot be expressed
/// with <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/> alone.
/// </summary>
/// <remarks>
/// <para>Validates:</para>
/// <list type="bullet">
/// <item><description><see cref="AdaptiveTtlOptions.MinTtl"/> must be greater than <see cref="TimeSpan.Zero"/>.</description></item>
/// <item><description><see cref="AdaptiveTtlOptions.MaxTtl"/> must be greater than or equal to <see cref="AdaptiveTtlOptions.MinTtl"/>.</description></item>
/// </list>
/// </remarks>
internal sealed class AdaptiveTtlOptionsValidator : IValidateOptions<AdaptiveTtlOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AdaptiveTtlOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MinTtl <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(AdaptiveTtlOptions.MinTtl)} must be greater than TimeSpan.Zero. Current value: {options.MinTtl}.");
		}

		if (options.MaxTtl <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(AdaptiveTtlOptions.MaxTtl)} must be greater than TimeSpan.Zero. Current value: {options.MaxTtl}.");
		}

		if (options.MaxTtl < options.MinTtl)
		{
			failures.Add(
				$"{nameof(AdaptiveTtlOptions.MaxTtl)} ({options.MaxTtl}) must be greater than or equal to " +
				$"{nameof(AdaptiveTtlOptions.MinTtl)} ({options.MinTtl}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
