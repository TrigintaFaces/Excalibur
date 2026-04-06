// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// AOT-safe validator for <see cref="ObservabilityOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class ObservabilityOptionsValidator : IValidateOptions<ObservabilityOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ObservabilityOptions options)
	{
		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ActivitySourceName))
		{
			failures.Add($"{nameof(options.ActivitySourceName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.MeterName))
		{
			failures.Add($"{nameof(options.MeterName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.ServiceName))
		{
			failures.Add($"{nameof(options.ServiceName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.ServiceVersion))
		{
			failures.Add($"{nameof(options.ServiceVersion)} is required.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
