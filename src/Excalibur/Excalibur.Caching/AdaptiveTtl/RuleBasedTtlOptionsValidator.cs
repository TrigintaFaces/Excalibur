// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Validates <see cref="RuleBasedTtlOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Complements the per-field data-annotation ranges with cross-field ordering invariants.
/// </summary>
internal sealed class RuleBasedTtlOptionsValidator : IValidateOptions<RuleBasedTtlOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RuleBasedTtlOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.HitRate.LowHitRateThreshold > options.HitRate.HighHitRateThreshold)
		{
			failures.Add(
				$"{nameof(RuleBasedTtlOptions.HitRate)}.{nameof(RuleBasedHitRateOptions.LowHitRateThreshold)} " +
				$"must not exceed {nameof(RuleBasedHitRateOptions.HighHitRateThreshold)}.");
		}

		if (options.Frequency.LowFrequencyThreshold > options.Frequency.HighFrequencyThreshold)
		{
			failures.Add(
				$"{nameof(RuleBasedTtlOptions.Frequency)}.{nameof(RuleBasedFrequencyOptions.LowFrequencyThreshold)} " +
				$"must not exceed {nameof(RuleBasedFrequencyOptions.HighFrequencyThreshold)}.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
