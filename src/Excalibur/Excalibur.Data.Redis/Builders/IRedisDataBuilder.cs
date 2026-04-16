// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using StackExchange.Redis;

namespace Excalibur.Data.Redis;

/// <summary>
/// Fluent builder for configuring Redis data settings.
/// </summary>
public interface IRedisDataBuilder
{
	/// <summary>Sets the Redis connection string.</summary>
	IRedisDataBuilder ConnectionString(string connectionString);

	/// <summary>Sets a pre-configured <see cref="IConnectionMultiplexer"/> instance.</summary>
	IRedisDataBuilder ConnectionMultiplexer(IConnectionMultiplexer multiplexer);

	/// <summary>Sets a factory that resolves an <see cref="IConnectionMultiplexer"/> from DI.</summary>
	IRedisDataBuilder ConnectionMultiplexerFactory(Func<IServiceProvider, IConnectionMultiplexer> factory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IRedisDataBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the key prefix. Prevents collisions between subsystems.</summary>
	IRedisDataBuilder KeyPrefix(string prefix);

	/// <summary>Sets the Redis database number. Default: 0.</summary>
	IRedisDataBuilder Database(int database);

}
