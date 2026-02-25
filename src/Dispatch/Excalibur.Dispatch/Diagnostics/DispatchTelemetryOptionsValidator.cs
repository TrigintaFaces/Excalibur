// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Validator for DispatchTelemetryOptions.
/// </summary>
internal sealed class DispatchTelemetryOptionsValidator : IValidateOptions<DispatchTelemetryOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DispatchTelemetryOptions options)
	{
		try
		{
			options.Validate();
			return ValidateOptionsResult.Success;
		}
		catch (ArgumentException ex)
		{
			return ValidateOptionsResult.Fail(ex.Message);
		}
	}
}
