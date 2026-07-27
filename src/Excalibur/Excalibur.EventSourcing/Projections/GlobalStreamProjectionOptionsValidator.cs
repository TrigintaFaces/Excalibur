// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Validates <see cref="GlobalStreamProjectionOptions"/> at startup so a misconfigured projection host fails
/// fast instead of surfacing as a deep runtime error once the background host starts processing.
/// </summary>
internal sealed class GlobalStreamProjectionOptionsValidator : IValidateOptions<GlobalStreamProjectionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GlobalStreamProjectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.CheckpointInterval < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.CheckpointInterval)} must be greater than zero.");
		}

		if (string.IsNullOrWhiteSpace(options.ProjectionName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.ProjectionName)} is required and must not be empty or whitespace.");
		}

		if (options.BatchSize < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.BatchSize)} must be greater than zero.");
		}

		if (options.IdlePollingInterval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.IdlePollingInterval)} must be greater than zero.");
		}

		return ValidateOptionsResult.Success;
	}
}
