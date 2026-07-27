// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Validates <see cref="MigrationOptions"/> at startup so a misconfigured migration fails fast
/// instead of surfacing as a deep runtime error mid-migration.
/// </summary>
internal sealed class MigrationOptionsValidator : IValidateOptions<MigrationOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MigrationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.BatchSize < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MigrationOptions.BatchSize)} must be greater than zero.");
		}

		if (options.MaxEvents < 0)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MigrationOptions.MaxEvents)} must not be negative (0 means no limit).");
		}

		return ValidateOptionsResult.Success;
	}
}

/// <summary>
/// Validates <see cref="MigrationRunnerOptions"/> at startup so a misconfigured migration runner fails fast
/// instead of surfacing as a deep runtime error when migrations are discovered or executed.
/// </summary>
internal sealed class MigrationRunnerOptionsValidator : IValidateOptions<MigrationRunnerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MigrationRunnerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.ParallelStreams < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MigrationRunnerOptions.ParallelStreams)} must be greater than zero.");
		}

		if (options.BatchSize < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MigrationRunnerOptions.BatchSize)} must be greater than zero.");
		}

		return ValidateOptionsResult.Success;
	}
}
