// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using StackExchange.Redis;

namespace Excalibur.LeaderElection.Redis;

/// <summary>
/// Fluent builder for configuring Redis leaderelection settings.
/// </summary>
public interface IRedisLeaderElectionBuilder
{
	/// <summary>Sets the Redis connection string.</summary>
	IRedisLeaderElectionBuilder ConnectionString(string connectionString);

	/// <summary>Sets a pre-configured <see cref="IConnectionMultiplexer"/> instance.</summary>
	IRedisLeaderElectionBuilder ConnectionMultiplexer(IConnectionMultiplexer multiplexer);

	/// <summary>Sets a factory that resolves an <see cref="IConnectionMultiplexer"/> from DI.</summary>
	IRedisLeaderElectionBuilder ConnectionMultiplexerFactory(Func<IServiceProvider, IConnectionMultiplexer> factory);

	/// <summary>Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.</summary>
	IRedisLeaderElectionBuilder BindConfiguration(string sectionPath);

	/// <summary>Sets the key prefix. Prevents collisions between subsystems.</summary>
	IRedisLeaderElectionBuilder KeyPrefix(string prefix);

	/// <summary>Sets the Redis database number. Default: 0.</summary>
	IRedisLeaderElectionBuilder Database(int database);

	/// <summary>Sets the lock key for leader election.</summary>
	IRedisLeaderElectionBuilder LockKey(string lockKey);
}
