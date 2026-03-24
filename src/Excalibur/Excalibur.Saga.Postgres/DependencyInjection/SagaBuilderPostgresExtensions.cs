// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Postgres.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// <c>SagaBuilderSqlServerExtensions.UseSqlServer()</c> pattern.
/// Connection strings are configured via <see cref="PostgresSagaOptions.ConnectionString"/>
/// inside the options action -- not as a top-level parameter.
/// </para>
/// </remarks>
public static class SagaBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the saga builder to use Postgres for saga store persistence.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Action to configure the saga store options including the connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UsePostgres(options =&gt;
	///     {
	///         options.ConnectionString = "Host=localhost;Database=myapp;";
	///         options.Schema = "sagas";
	///     });
	/// });
	/// </code>
	/// </example>
	public static ISagaBuilder UsePostgres(
		this ISagaBuilder builder,
		Action<PostgresSagaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddPostgresSagaStore(configure);

		return builder;
	}
}
