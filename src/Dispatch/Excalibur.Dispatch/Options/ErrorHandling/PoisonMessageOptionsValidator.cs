// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.ErrorHandling;

/// <summary>
/// Validates <see cref="PoisonMessageOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class PoisonMessageOptionsValidator : IValidateOptions<PoisonMessageOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, PoisonMessageOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxRetryAttempts < 1)
		{
			failures.Add($"{nameof(PoisonMessageOptions.MaxRetryAttempts)} must be >= 1 (was {options.MaxRetryAttempts}).");
		}

		if (options.Alerting.AlertThreshold < 1)
		{
			failures.Add($"Alerting.{nameof(PoisonMessageAlertingOptions.AlertThreshold)} must be >= 1 (was {options.Alerting.AlertThreshold}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
