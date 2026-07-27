// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.OpenSearch;

/// <summary>Validates <see cref="OpenSearchConfigurationOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class OpenSearchConfigurationOptionsValidator : IValidateOptions<OpenSearchConfigurationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, OpenSearchConfigurationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var hasUrl = options.Url is not null;
		var hasUrls = options.Urls is not null && options.Urls.Any();

		return !hasUrl && !hasUrls
			? ValidateOptionsResult.Fail(
				$"A connection target is required: set {nameof(OpenSearchConfigurationOptions.Url)} or " +
				$"{nameof(OpenSearchConfigurationOptions.Urls)}.")
			: ValidateOptionsResult.Success;
	}
}
