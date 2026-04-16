// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Postgres;

/// <summary>
/// Validates <see cref="PostgresSagaOptions"/> at startup via ValidateOnStart.
/// Supports builder-configured connections (DataSource/DataSourceFactory/ConnectionStringName)
/// that don't set ConnectionString on the options object.
/// </summary>
internal sealed class PostgresSagaOptionsValidator : IValidateOptions<PostgresSagaOptions>
{
	/// <summary>
	/// Gets or sets a value indicating whether a builder-level connection was configured.
	/// When true, ConnectionString validation is skipped.
	/// </summary>
	internal bool HasBuilderConnection { get; init; }

	public ValidateOptionsResult Validate(string? name, PostgresSagaOptions options)
	{
		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"No connection configured for Saga (Postgres). " +
				"Call ConnectionString(), ConnectionStringName(), DataSource(), DataSourceFactory(), " +
				"or BindConfiguration() inside UsePostgres().");
		}

		return ValidateOptionsResult.Success;
	}
}
