// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.AccessReviews;

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance;

/// <summary>
/// Validates <see cref="AccessReviewOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces <c>[Range(typeof(TimeSpan), ...)]</c> attributes with AOT-safe checks.
/// </summary>
internal sealed class AccessReviewOptionsValidator : IValidateOptions<AccessReviewOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AccessReviewOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DefaultCampaignDuration < TimeSpan.FromMinutes(1) || options.DefaultCampaignDuration > TimeSpan.FromDays(365))
		{
			failures.Add(
				$"{nameof(AccessReviewOptions.DefaultCampaignDuration)} must be between 00:01:00 and 365.00:00:00 (was {options.DefaultCampaignDuration}).");
		}

		if (options.ExpiryCheckInterval < TimeSpan.FromSeconds(10) || options.ExpiryCheckInterval > TimeSpan.FromDays(1))
		{
			failures.Add(
				$"{nameof(AccessReviewOptions.ExpiryCheckInterval)} must be between 00:00:10 and 24:00:00 (was {options.ExpiryCheckInterval}).");
		}

		if (options.RetryBaseDelay < TimeSpan.FromSeconds(1) || options.RetryBaseDelay > TimeSpan.FromMinutes(5))
		{
			failures.Add(
				$"{nameof(AccessReviewOptions.RetryBaseDelay)} must be between 00:00:01 and 00:05:00 (was {options.RetryBaseDelay}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
