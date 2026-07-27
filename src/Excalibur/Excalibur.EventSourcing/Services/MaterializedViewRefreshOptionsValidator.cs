// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Services;

/// <summary>
/// Validates <see cref="MaterializedViewRefreshOptions"/> at startup so a misconfigured refresh service fails
/// fast instead of surfacing as a deep runtime error during the first scheduled refresh.
/// </summary>
internal sealed class MaterializedViewRefreshOptionsValidator : IValidateOptions<MaterializedViewRefreshOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MaterializedViewRefreshOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.BatchSize < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MaterializedViewRefreshOptions.BatchSize)} must be greater than zero.");
		}

		if (options.MaxRetryCount < 0)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MaterializedViewRefreshOptions.MaxRetryCount)} must not be negative.");
		}

		if (options.InitialRetryDelay < TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MaterializedViewRefreshOptions.InitialRetryDelay)} must not be negative.");
		}

		if (options.MaxRetryDelay < options.InitialRetryDelay)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MaterializedViewRefreshOptions.MaxRetryDelay)} must be greater than or equal to " +
				$"{nameof(MaterializedViewRefreshOptions.InitialRetryDelay)}.");
		}

		if (options.RefreshInterval is { } interval && interval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MaterializedViewRefreshOptions.RefreshInterval)}, when set, must be greater than zero.");
		}

		return ValidateOptionsResult.Success;
	}
}
