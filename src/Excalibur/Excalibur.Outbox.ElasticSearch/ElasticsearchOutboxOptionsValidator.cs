// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.ElasticSearch;

/// <summary>
/// Validates <see cref="ElasticsearchOutboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class ElasticsearchOutboxOptionsValidator : IValidateOptions<ElasticsearchOutboxOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ElasticsearchOutboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("ElasticsearchOutboxOptions cannot be null.");
		}

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.IndexName))
		{
			failures.Add("ElasticsearchOutboxOptions.IndexName is required.");
		}

		if (options.DefaultBatchSize is < 1 or > 10000)
		{
			failures.Add("ElasticsearchOutboxOptions.DefaultBatchSize must be between 1 and 10000.");
		}

		if (options.SentMessageRetentionDays < 0)
		{
			failures.Add("ElasticsearchOutboxOptions.SentMessageRetentionDays must be zero or greater.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
