// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Validates <see cref="PostgresEventSourcingOptions"/> at startup via ValidateOnStart.
/// Ensures a connection has been configured through the builder.
/// </summary>
internal sealed class PostgresEventSourcingOptionsValidator : IValidateOptions<PostgresEventSourcingOptions>
{
	internal bool HasBuilderConnection { get; init; }

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, PostgresEventSourcingOptions options)
	{
		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"No connection configured for EventSourcing (Postgres). " +
				"Call ConnectionString(), ConnectionStringName(), DataSource(), DataSourceFactory(), " +
				"or BindConfiguration() inside UsePostgres().");
		}

		return ValidateOptionsResult.Success;
	}
}
