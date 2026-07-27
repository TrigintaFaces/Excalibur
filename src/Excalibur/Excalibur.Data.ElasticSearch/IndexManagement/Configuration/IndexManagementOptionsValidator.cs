// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Validates <see cref="IndexManagementOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Written as an <see cref="IValidateOptions{TOptions}"/> rather than data annotations so the checks stay
/// AOT-safe and can reach the nested option objects.
/// </summary>
/// <remarks>
/// These options are bound from configuration, so every value here can come from a consumer's settings file
/// rather than from code. A shard count of zero, a non-positive refresh interval or an empty merge policy is
/// rejected by Elasticsearch itself at index-creation time — long after startup, on the first write, with an
/// error that points at the cluster rather than at the configuration that caused it. Failing at startup puts
/// the error next to its cause.
/// </remarks>
internal sealed class IndexManagementOptionsValidator : IValidateOptions<IndexManagementOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, IndexManagementOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// A disabled feature is not required to be configured coherently: validating it would reject
		// settings the consumer has deliberately left at whatever value while the feature is off.
		if (!options.Enabled)
		{
			return ValidateOptionsResult.Success;
		}

		var failures = new List<string>();
		const string Prefix = nameof(IndexManagementOptions);

		var template = options.DefaultTemplate;
		if (template.DefaultShards < 1)
		{
			failures.Add($"{Prefix}.DefaultTemplate.DefaultShards must be at least 1 (was {template.DefaultShards}).");
		}

		if (template.DefaultReplicas < 0)
		{
			failures.Add($"{Prefix}.DefaultTemplate.DefaultReplicas cannot be negative (was {template.DefaultReplicas}).");
		}

		if (template.DefaultRefreshInterval <= TimeSpan.Zero)
		{
			failures.Add(
				$"{Prefix}.DefaultTemplate.DefaultRefreshInterval must be a positive duration " +
				$"(was {template.DefaultRefreshInterval}).");
		}

		if (template.DefaultPriority < 0)
		{
			failures.Add($"{Prefix}.DefaultTemplate.DefaultPriority cannot be negative (was {template.DefaultPriority}).");
		}

		var lifecycle = options.Lifecycle;
		if (lifecycle.Enabled)
		{
			if (lifecycle.HotPhaseDuration <= TimeSpan.Zero)
			{
				failures.Add(
					$"{Prefix}.Lifecycle.HotPhaseDuration must be a positive duration when lifecycle management " +
					$"is enabled (was {lifecycle.HotPhaseDuration}).");
			}

			if (lifecycle.WarmPhaseDuration <= TimeSpan.Zero)
			{
				failures.Add(
					$"{Prefix}.Lifecycle.WarmPhaseDuration must be a positive duration when lifecycle management " +
					$"is enabled (was {lifecycle.WarmPhaseDuration}).");
			}

			if (lifecycle.ColdPhaseDuration <= TimeSpan.Zero)
			{
				failures.Add(
					$"{Prefix}.Lifecycle.ColdPhaseDuration must be a positive duration when lifecycle management " +
					$"is enabled (was {lifecycle.ColdPhaseDuration}).");
			}
		}

		var optimization = options.Optimization;
		if (string.IsNullOrWhiteSpace(optimization.MergePolicy))
		{
			failures.Add($"{Prefix}.Optimization.MergePolicy must not be empty.");
		}

		if (string.IsNullOrWhiteSpace(optimization.CompressionLevel))
		{
			failures.Add($"{Prefix}.Optimization.CompressionLevel must not be empty.");
		}

		if (optimization.MaxSegmentsPerTier < 1)
		{
			failures.Add(
				$"{Prefix}.Optimization.MaxSegmentsPerTier must be at least 1 " +
				$"(was {optimization.MaxSegmentsPerTier}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
