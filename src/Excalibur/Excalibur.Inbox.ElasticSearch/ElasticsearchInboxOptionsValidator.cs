// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.ElasticSearch;

/// <summary>
/// Validates <see cref="ElasticsearchInboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class ElasticsearchInboxOptionsValidator : IValidateOptions<ElasticsearchInboxOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ElasticsearchInboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("ElasticsearchInboxOptions cannot be null.");
		}

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.IndexName))
		{
			failures.Add("ElasticsearchInboxOptions.IndexName is required.");
		}

		if (options.RetentionDays < 0)
		{
			failures.Add("ElasticsearchInboxOptions.RetentionDays must be zero or greater.");
		}

		if (options.MaxConcurrencyRetries < 1)
		{
			failures.Add(
				"ElasticsearchInboxOptions.MaxConcurrencyRetries must be at least 1. A bound below 1 skips the "
				+ "conditional write entirely, so the store would report Undecided without ever having "
				+ "attempted the transition.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
