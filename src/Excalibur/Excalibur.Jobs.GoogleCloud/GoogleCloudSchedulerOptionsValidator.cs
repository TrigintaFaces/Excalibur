// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.GoogleCloud;

/// <summary>Validates <see cref="GoogleCloudSchedulerOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class GoogleCloudSchedulerOptionsValidator : IValidateOptions<GoogleCloudSchedulerOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, GoogleCloudSchedulerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ProjectId))
		{
			failures.Add($"{nameof(GoogleCloudSchedulerOptions.ProjectId)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.LocationId))
		{
			failures.Add($"{nameof(GoogleCloudSchedulerOptions.LocationId)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.TimeZone))
		{
			failures.Add($"{nameof(GoogleCloudSchedulerOptions.TimeZone)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.TargetUrl))
		{
			failures.Add($"{nameof(GoogleCloudSchedulerOptions.TargetUrl)} is required.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
