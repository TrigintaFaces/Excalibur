// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Builder interface for configuring Postgres persistence.
/// </summary>
public interface IPostgresPersistenceBuilder
{
	/// <summary>
	/// Gets the service collection.
	/// </summary>
	/// <value>
	/// The service collection.
	/// </value>
	IServiceCollection Services { get; }

	/// <summary>
	/// Configures the connection string.
	/// </summary>
	/// <param name="connectionString"> The connection string. </param>
	/// <returns> The builder for chaining. </returns>
	IPostgresPersistenceBuilder WithConnectionString(string connectionString);

	/// <summary>
	/// Configures connection pooling.
	/// </summary>
	/// <param name="enabled"> Whether to enable pooling. </param>
	/// <param name="minSize"> Minimum pool size. </param>
	/// <param name="maxSize"> Maximum pool size. </param>
	/// <returns> The builder for chaining. </returns>
	IPostgresPersistenceBuilder WithConnectionPooling(bool enabled = true, int minSize = 0, int maxSize = 100);

	/// <summary>
	/// Configures retry policy.
	/// </summary>
	/// <param name="maxAttempts"> Maximum retry attempts. </param>
	/// <param name="delayMilliseconds"> Delay between retries in milliseconds. </param>
	/// <returns> The builder for chaining. </returns>
	IPostgresPersistenceBuilder WithRetryPolicy(int maxAttempts = 3, int delayMilliseconds = 1000);

	/// <summary>
	/// Configures timeouts.
	/// </summary>
	/// <param name="connectionTimeout"> Connection timeout in seconds. </param>
	/// <param name="commandTimeout"> Command timeout in seconds. </param>
	/// <returns> The builder for chaining. </returns>
	IPostgresPersistenceBuilder WithTimeouts(int connectionTimeout = 30, int commandTimeout = 30);

}
