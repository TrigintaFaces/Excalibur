// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Spanner;

/// <summary>
/// Validates <see cref="SpannerOptions"/> at startup (fail-fast) so a misconfigured Spanner provider is
/// rejected before the first request rather than surfacing as a runtime connection failure.
/// </summary>
internal sealed class SpannerOptionsValidator : IValidateOptions<SpannerOptions>
{
	public ValidateOptionsResult Validate(string? name, SpannerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ProjectId))
		{
			failures.Add($"{nameof(SpannerOptions.ProjectId)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.InstanceId))
		{
			failures.Add($"{nameof(SpannerOptions.InstanceId)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.DatabaseId))
		{
			failures.Add($"{nameof(SpannerOptions.DatabaseId)} is required.");
		}

		if (options.MaxAbortRetries < 0)
		{
			failures.Add($"{nameof(SpannerOptions.MaxAbortRetries)} must be non-negative.");
		}

		if (options.AbortRetryBaseDelayMilliseconds < 0)
		{
			failures.Add($"{nameof(SpannerOptions.AbortRetryBaseDelayMilliseconds)} must be non-negative.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
