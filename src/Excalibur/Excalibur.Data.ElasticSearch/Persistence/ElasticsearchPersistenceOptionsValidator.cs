// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Persistence;

/// <summary>Validates <see cref="ElasticsearchPersistenceOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ElasticsearchPersistenceOptionsValidator : IValidateOptions<ElasticsearchPersistenceOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ElasticsearchPersistenceOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.IndexPrefix))
		{
			failures.Add($"{nameof(ElasticsearchPersistenceOptions.IndexPrefix)} must be a non-empty prefix.");
		}

		if (options.NumberOfShards is < 1 or > 1024)
		{
			failures.Add($"{nameof(ElasticsearchPersistenceOptions.NumberOfShards)} must be between 1 and 1024.");
		}

		if (options.NumberOfReplicas is < 0 or > 10)
		{
			failures.Add($"{nameof(ElasticsearchPersistenceOptions.NumberOfReplicas)} must be between 0 and 10.");
		}

		if (options.MaxResultCount is < 1 or > 10000)
		{
			failures.Add($"{nameof(ElasticsearchPersistenceOptions.MaxResultCount)} must be between 1 and 10000.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
