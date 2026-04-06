// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Aws;

/// <summary>
/// Validates <see cref="AwsObservabilityOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class AwsObservabilityOptionsValidator : IValidateOptions<AwsObservabilityOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsObservabilityOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.SamplingRate is < 0.0 or > 1.0)
		{
			failures.Add($"{nameof(AwsObservabilityOptions.SamplingRate)} must be between 0.0 and 1.0 (was {options.SamplingRate}).");
		}

		if (string.IsNullOrWhiteSpace(options.ServiceName))
		{
			failures.Add($"{nameof(AwsObservabilityOptions.ServiceName)} is required.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
