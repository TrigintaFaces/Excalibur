// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;

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
			options.Pooling.EnableConnectionPooling = enabled;
			options.Pooling.MinPoolSize = minSize;
			options.Pooling.MaxPoolSize = maxSize;
		});
		return this;
	}

	/// <inheritdoc/>
	public IPostgresPersistenceBuilder WithRetryPolicy(int maxAttempts = 3, int delayMilliseconds = 1000)
	{
		_ = Services.Configure<PostgresPersistenceOptions>(options =>
		{
			options.Resilience.MaxRetryAttempts = maxAttempts;
			options.Resilience.RetryDelayMilliseconds = delayMilliseconds;
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

}
