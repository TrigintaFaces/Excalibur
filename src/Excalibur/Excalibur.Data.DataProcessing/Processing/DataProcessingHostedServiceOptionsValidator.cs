// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.DataProcessing.Processing;

/// <summary>
/// Validates <see cref="DataProcessingHostedServiceOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks that cannot be expressed via
/// <see cref="System.ComponentModel.DataAnnotations"/> attributes alone.
/// </remarks>
internal sealed class DataProcessingHostedServiceOptionsValidator : IValidateOptions<DataProcessingHostedServiceOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DataProcessingHostedServiceOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.PollingInterval <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(DataProcessingHostedServiceOptions.PollingInterval)} must be positive (was {options.PollingInterval}).");
		}

		if (options.DrainTimeout <= options.PollingInterval)
		{
			failures.Add(
				$"{nameof(DataProcessingHostedServiceOptions.DrainTimeoutSeconds)} ({options.DrainTimeoutSeconds}s) must yield a timeout greater than " +
				$"{nameof(DataProcessingHostedServiceOptions.PollingInterval)} ({options.PollingInterval}).");
		}

		if (options.UnhealthyThreshold < 1)
		{
			failures.Add(
				$"{nameof(DataProcessingHostedServiceOptions.UnhealthyThreshold)} must be >= 1 (was {options.UnhealthyThreshold}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
