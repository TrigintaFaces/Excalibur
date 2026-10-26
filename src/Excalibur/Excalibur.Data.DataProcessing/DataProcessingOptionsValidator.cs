// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Validation;

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
/// <item><description><see cref="DataProcessingOptions.ProcessingStallTimeout"/> must be between 1 second and 1 hour</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class DataProcessingOptionsValidator : IValidateOptions<DataProcessingOptions>
{
	/// <summary>
	/// Minimum dispatcher timeout in milliseconds (1 second).
	/// </summary>
	internal static readonly TimeSpan MinProcessingStallTimeout = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Maximum dispatcher timeout in milliseconds (1 hour).
	/// </summary>
	internal static readonly TimeSpan MaxProcessingStallTimeout = TimeSpan.FromHours(1);

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, DataProcessingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (!SqlIdentifierValidator.IsValid(options.SchemaName))
		{
			failures.Add(
				$"SchemaName contains invalid characters. Only alphanumeric characters and underscores are allowed: '{options.SchemaName}'.");
		}

		if (!SqlIdentifierValidator.IsValid(options.TableName))
		{
			failures.Add(
				$"TableName contains invalid characters. Only alphanumeric characters and underscores are allowed: '{options.TableName}'.");
		}

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

		if (options.ProcessingStallTimeout < MinProcessingStallTimeout
			|| options.ProcessingStallTimeout > MaxProcessingStallTimeout)
		{
			failures.Add(
				$"ProcessingStallTimeout ({options.ProcessingStallTimeout}) must be between " +
				$"{MinProcessingStallTimeout} and {MaxProcessingStallTimeout}.");
		}

		// A total budget BELOW the stall timeout can never be satisfied: the task would be cancelled by the
		// cap before the stall clock could even expire once, so every task fails and the message blames the
		// cap rather than the misconfiguration.
		if (options.ProcessingTotalTimeout is { } totalBudget && totalBudget < options.ProcessingStallTimeout)
		{
			failures.Add(
				$"ProcessingTotalTimeout ({totalBudget}) must not be less than " +
				$"ProcessingStallTimeout ({options.ProcessingStallTimeout}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
