// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Sanitization;

/// <summary>
/// Validates <see cref="TelemetrySanitizerOptions"/> at startup and emits a warning
/// when <see cref="TelemetrySanitizerOptions.IncludeRawPii"/> is <see langword="true"/>
/// in a non-Development environment.
/// </summary>
/// <remarks>
/// <para>
/// This validator does not fail validation — <c>IncludeRawPii=true</c> is a legitimate
/// configuration for debugging. The warning ensures operators are aware that PII
/// sanitization has been intentionally disabled.
/// </para>
/// </remarks>
internal sealed partial class TelemetrySanitizerOptionsValidator(
	IHostEnvironment hostEnvironment,
	ILogger<TelemetrySanitizerOptionsValidator> logger)
	: IValidateOptions<TelemetrySanitizerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TelemetrySanitizerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.IncludeRawPii && !hostEnvironment.IsDevelopment())
		{
			LogPiiSanitizationBypassed(hostEnvironment.EnvironmentName);
		}

		return ValidateOptionsResult.Success;
	}

	[LoggerMessage(
		ObservabilityEventId.PiiSanitizationBypassed,
		LogLevel.Warning,
		"TelemetrySanitizerOptions.IncludeRawPii is enabled in '{EnvironmentName}' environment. " +
		"PII data will be emitted to telemetry without sanitization. " +
		"This should only be used in Development environments for debugging.")]
	private partial void LogPiiSanitizationBypassed(string environmentName);
}
