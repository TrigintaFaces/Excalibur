// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>Validates <see cref="ElasticsearchSecurityOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ElasticsearchSecurityOptionsValidator : IValidateOptions<ElasticsearchSecurityOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ElasticsearchSecurityOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return !Enum.IsDefined(options.Mode)
			? ValidateOptionsResult.Fail($"{nameof(ElasticsearchSecurityOptions.Mode)} must be a defined security mode.")
			: ValidateOptionsResult.Success;
	}
}
