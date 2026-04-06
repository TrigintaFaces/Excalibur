// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Validates <see cref="MaterializedViewHealthCheckOptions"/> settings.
/// </summary>
internal sealed class MaterializedViewHealthCheckOptionsValidator : IValidateOptions<MaterializedViewHealthCheckOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, MaterializedViewHealthCheckOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Materialized view health check options cannot be null.");
		}

		if (options.FailureRateThresholdPercent is < 0.0 or > 100.0)
		{
			return ValidateOptionsResult.Fail("FailureRateThresholdPercent must be between 0 and 100.");
		}

		if (string.IsNullOrWhiteSpace(options.Name))
		{
			return ValidateOptionsResult.Fail("Name is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
