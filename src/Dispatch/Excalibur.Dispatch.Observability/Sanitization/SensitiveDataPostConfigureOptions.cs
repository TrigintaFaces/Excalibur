// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Options.Core;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Sanitization;

/// <summary>
/// Post-configures <see cref="TracingOptions"/>, <see cref="AuditLoggingOptions"/>, and
/// <see cref="ObservabilityOptions"/> to flow the <see cref="TelemetrySanitizerOptions.IncludeRawPii"/>
/// flag into each <c>IncludeSensitiveData</c> property.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="TelemetrySanitizerOptions.IncludeRawPii"/> is <see langword="true"/>,
/// all three <c>IncludeSensitiveData</c> flags are set to <see langword="true"/> automatically.
/// This ensures a single top-level toggle controls PII inclusion across the entire Dispatch pipeline.
/// </para>
/// <para>
/// If a consumer has explicitly set <c>IncludeSensitiveData = true</c> on any individual options class,
/// that setting is preserved regardless of <c>IncludeRawPii</c>.
/// </para>
/// </remarks>
internal sealed class SensitiveDataPostConfigureOptions(IOptions<TelemetrySanitizerOptions> sanitizerOptions)
	: IPostConfigureOptions<TracingOptions>,
	  IPostConfigureOptions<AuditLoggingOptions>,
	  IPostConfigureOptions<ObservabilityOptions>
{
	private readonly bool _includeRawPii = sanitizerOptions?.Value?.IncludeRawPii ?? false;

	/// <inheritdoc />
	public void PostConfigure(string? name, TracingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (_includeRawPii)
		{
			options.IncludeSensitiveData = true;
		}
	}

	/// <inheritdoc />
	public void PostConfigure(string? name, AuditLoggingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (_includeRawPii)
		{
			options.IncludeSensitiveData = true;
		}
	}

	/// <inheritdoc />
	public void PostConfigure(string? name, ObservabilityOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (_includeRawPii)
		{
			options.IncludeSensitiveData = true;
		}
	}
}
