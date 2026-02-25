// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Implementation of the Postgres persistence builder.
/// </summary>
internal sealed class PostgresPersistenceBuilder(IServiceCollection services) : IPostgresPersistenceBuilder
{
	/// <inheritdoc/>
	public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithConnectionString(string connectionString)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options => options.ConnectionString = connectionString);
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithConnectionPooling(bool enabled = true, int minSize = 0, int maxSize = 100)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options =>
		{
			options.EnableConnectionPooling = enabled;
			options.MinPoolSize = minSize;
			options.MaxPoolSize = maxSize;
		});
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithRetryPolicy(int maxAttempts = 3, int delayMilliseconds = 1000)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options =>
		{
			options.MaxRetryAttempts = maxAttempts;
			options.RetryDelayMilliseconds = delayMilliseconds;
		});
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithTimeouts(int connectionTimeout = 30, int commandTimeout = 30)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options =>
		{
			options.ConnectionTimeout = connectionTimeout;
			options.CommandTimeout = commandTimeout;
		});
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithMetrics(bool enabled = true)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options => options.EnableMetrics = enabled);
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithDetailedLogging(bool enabled = false)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options => options.EnableDetailedLogging = enabled);
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithPreparedStatements(bool enabled = true, int maxStatements = 200)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options =>
		{
			options.Statements.EnablePreparedStatementCaching = enabled;
			options.Statements.MaxPreparedStatements = maxStatements;
		});
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithApplicationName(string applicationName)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options => options.Connection.ApplicationName = applicationName);
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder AddHealthCheck(string name = "Postgres", params string[] tags)
	{
		_ = Services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Add(new HealthCheckRegistration(
			name,
			provider => new PostgresPersistenceHealthCheck(
				provider.GetRequiredService<IOptions<PostgresPersistenceOptions>>(),
				provider.GetRequiredService<ILogger<PostgresPersistenceHealthCheck>>(),
				provider.GetService<PostgresPersistenceMetrics>()),
			HealthStatus.Unhealthy,
			tags)));
		return this;
	}
}
