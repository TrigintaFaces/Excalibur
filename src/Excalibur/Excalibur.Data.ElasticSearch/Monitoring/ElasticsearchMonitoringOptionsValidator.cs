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

		return !Enum.IsDefined(options.Level)
			? ValidateOptionsResult.Fail($"{nameof(ElasticsearchMonitoringOptions.Level)} must be a defined monitoring level.")
			: ValidateOptionsResult.Success;
	}
}
