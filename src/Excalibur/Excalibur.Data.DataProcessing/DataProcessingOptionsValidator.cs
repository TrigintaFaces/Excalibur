// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Validates cross-property constraints for <see cref="DataProcessingOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// This validator enforces inter-property relationships that cannot be expressed
/// with individual <c>[Range]</c> or <c>[Required]</c> attributes:
/// <list type="bullet">
/// <item><description><see cref="DataProcessingOptions.ProducerBatchSize"/> must not exceed <see cref="DataProcessingOptions.QueueSize"/></description></item>
/// <item><description><see cref="DataProcessingOptions.ConsumerBatchSize"/> must not exceed <see cref="DataProcessingOptions.QueueSize"/></description></item>
/// <item><description><see cref="DataProcessingOptions.DispatcherTimeoutMilliseconds"/> must be between 1000 and 3600000 (1s to 1hr)</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class DataProcessingOptionsValidator : IValidateOptions<DataProcessingOptions>
{
	/// <summary>
	/// Minimum dispatcher timeout in milliseconds (1 second).
	/// </summary>
	internal const int MinDispatcherTimeoutMs = 1_000;

	/// <summary>
	/// Maximum dispatcher timeout in milliseconds (1 hour).
	/// </summary>
	internal const int MaxDispatcherTimeoutMs = 3_600_000;

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, DataProcessingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.ProducerBatchSize > options.QueueSize)
		{
			failures.Add(
				$"ProducerBatchSize ({options.ProducerBatchSize}) must not exceed QueueSize ({options.QueueSize}).");
		}

		if (options.ConsumerBatchSize > options.QueueSize)
		{
			failures.Add(
				$"ConsumerBatchSize ({options.ConsumerBatchSize}) must not exceed QueueSize ({options.QueueSize}).");
		}

		if (options.DispatcherTimeoutMilliseconds is < MinDispatcherTimeoutMs or > MaxDispatcherTimeoutMs)
		{
			failures.Add(
				$"DispatcherTimeoutMilliseconds ({options.DispatcherTimeoutMilliseconds}) must be between " +
				$"{MinDispatcherTimeoutMs} and {MaxDispatcherTimeoutMs}.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
