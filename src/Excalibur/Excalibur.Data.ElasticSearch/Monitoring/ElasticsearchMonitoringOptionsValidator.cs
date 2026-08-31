// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>Validates <see cref="ElasticsearchMonitoringOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ElasticsearchMonitoringOptionsValidator : IValidateOptions<ElasticsearchMonitoringOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ElasticsearchMonitoringOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!Enum.IsDefined(options.Level))
		{
			return ValidateOptionsResult.Fail($"{nameof(ElasticsearchMonitoringOptions.Level)} must be a defined monitoring level.");
		}

		var requestLogging = options.RequestLogging;
		if (requestLogging is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(ElasticsearchMonitoringOptions.RequestLogging)} must not be null.");
		}

		if (requestLogging.MaxBodySizeBytes <= 0)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(RequestLoggingOptions.MaxBodySizeBytes)} must be greater than zero. "
				+ "Turn body logging off with LogRequestBody and LogResponseBody rather than by setting a size of zero.");
		}

		if (requestLogging.AllowedBodyProperties is null)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(RequestLoggingOptions.AllowedBodyProperties)} must not be null. "
				+ "Leave it empty to redact every body value.");
		}

		return ValidateOptionsResult.Success;
	}
}
