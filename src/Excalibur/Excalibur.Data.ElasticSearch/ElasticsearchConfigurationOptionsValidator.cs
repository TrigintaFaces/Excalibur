// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch;

/// <summary>Validates <see cref="ElasticsearchConfigurationOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ElasticsearchConfigurationOptionsValidator : IValidateOptions<ElasticsearchConfigurationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ElasticsearchConfigurationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var hasUrl = options.Url is not null;
		var hasUrls = options.Urls is not null && options.Urls.Any();
		var hasCloudId = !string.IsNullOrWhiteSpace(options.CloudId);

		return !hasUrl && !hasUrls && !hasCloudId
			? ValidateOptionsResult.Fail(
				$"A connection target is required: set one of {nameof(ElasticsearchConfigurationOptions.Url)}, " +
				$"{nameof(ElasticsearchConfigurationOptions.Urls)}, or {nameof(ElasticsearchConfigurationOptions.CloudId)}.")
			: ValidateOptionsResult.Success;
	}
}
