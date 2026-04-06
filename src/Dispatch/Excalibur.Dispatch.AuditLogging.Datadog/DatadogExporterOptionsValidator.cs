// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Datadog;

/// <summary>
/// Validates <see cref="DatadogExporterOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class DatadogExporterOptionsValidator : IValidateOptions<DatadogExporterOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DatadogExporterOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.ApiKey))
		{
			return ValidateOptionsResult.Fail($"{nameof(DatadogExporterOptions.ApiKey)} is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
