// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Splunk;

/// <summary>Validates <see cref="SplunkExporterOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class SplunkExporterOptionsValidator : IValidateOptions<SplunkExporterOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SplunkExporterOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return string.IsNullOrWhiteSpace(options.SourceType)
			? ValidateOptionsResult.Fail($"{nameof(SplunkExporterOptions.SourceType)} must be a non-empty source type.")
			: ValidateOptionsResult.Success;
	}
}
