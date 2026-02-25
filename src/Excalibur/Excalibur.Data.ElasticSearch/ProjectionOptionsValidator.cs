// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Validates <see cref="ProjectionOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks including:
/// <list type="bullet">
///   <item><description>RetryPolicy.BaseDelay must be less than or equal to RetryPolicy.MaxDelay</description></item>
///   <item><description>RebuildManager.DefaultBatchSize must be positive when rebuild is enabled</description></item>
///   <item><description>RebuildManager.MaxDegreeOfParallelism must be positive when rebuild is enabled</description></item>
///   <item><description>ConsistencyTracking.SLAPercentage must be between 0 and 100</description></item>
///   <item><description>ConsistencyTracking.MetricsInterval must be positive when tracking is enabled</description></item>
/// </list>
/// </remarks>
public sealed class ProjectionOptionsValidator : IValidateOptions<ProjectionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ProjectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// IndexPrefix must not be empty
		if (string.IsNullOrWhiteSpace(options.IndexPrefix))
		{
			failures.Add($"{nameof(ProjectionOptions.IndexPrefix)} must not be null or whitespace.");
		}

		// RetryPolicy cross-property: BaseDelay <= MaxDelay
		if (options.RetryPolicy.Enabled && options.RetryPolicy.BaseDelay > options.RetryPolicy.MaxDelay)
		{
			failures.Add(
				$"RetryPolicy.{nameof(ProjectionRetryOptions.BaseDelay)} ({options.RetryPolicy.BaseDelay}) " +
				$"must be less than or equal to RetryPolicy.{nameof(ProjectionRetryOptions.MaxDelay)} ({options.RetryPolicy.MaxDelay}).");
		}

		// RetryPolicy: MaxIndexAttempts must be positive when enabled
		if (options.RetryPolicy.Enabled && options.RetryPolicy.MaxIndexAttempts <= 0)
		{
			failures.Add(
				$"RetryPolicy.{nameof(ProjectionRetryOptions.MaxIndexAttempts)} must be greater than 0 when retry is enabled (was {options.RetryPolicy.MaxIndexAttempts}).");
		}

		// RetryPolicy: MaxBulkAttempts must be positive when enabled
		if (options.RetryPolicy.Enabled && options.RetryPolicy.MaxBulkAttempts <= 0)
		{
			failures.Add(
				$"RetryPolicy.{nameof(ProjectionRetryOptions.MaxBulkAttempts)} must be greater than 0 when retry is enabled (was {options.RetryPolicy.MaxBulkAttempts}).");
		}

		// RebuildManager: DefaultBatchSize must be positive when enabled
		if (options.RebuildManager.Enabled && options.RebuildManager.DefaultBatchSize <= 0)
		{
			failures.Add(
				$"RebuildManager.{nameof(RebuildManagerOptions.DefaultBatchSize)} must be greater than 0 when rebuild is enabled (was {options.RebuildManager.DefaultBatchSize}).");
		}

		// RebuildManager: MaxDegreeOfParallelism must be positive when enabled
		if (options.RebuildManager.Enabled && options.RebuildManager.MaxDegreeOfParallelism <= 0)
		{
			failures.Add(
				$"RebuildManager.{nameof(RebuildManagerOptions.MaxDegreeOfParallelism)} must be greater than 0 when rebuild is enabled (was {options.RebuildManager.MaxDegreeOfParallelism}).");
		}

		// ConsistencyTracking: SLAPercentage must be between 0 and 100
		if (options.ConsistencyTracking.Enabled && (options.ConsistencyTracking.SLAPercentage <= 0.0 || options.ConsistencyTracking.SLAPercentage > 100.0))
		{
			failures.Add(
				$"ConsistencyTracking.{nameof(ConsistencyTrackingOptions.SLAPercentage)} must be between 0 and 100 (was {options.ConsistencyTracking.SLAPercentage}).");
		}

		// ConsistencyTracking: MetricsInterval must be positive when enabled
		if (options.ConsistencyTracking.Enabled && options.ConsistencyTracking.MetricsInterval <= TimeSpan.Zero)
		{
			failures.Add(
				$"ConsistencyTracking.{nameof(ConsistencyTrackingOptions.MetricsInterval)} must be positive when tracking is enabled (was {options.ConsistencyTracking.MetricsInterval}).");
		}

		// ConsistencyTracking: ExpectedMaxLag must be positive when enabled
		if (options.ConsistencyTracking.Enabled && options.ConsistencyTracking.ExpectedMaxLag <= TimeSpan.Zero)
		{
			failures.Add(
				$"ConsistencyTracking.{nameof(ConsistencyTrackingOptions.ExpectedMaxLag)} must be positive when tracking is enabled (was {options.ConsistencyTracking.ExpectedMaxLag}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
