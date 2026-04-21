// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using StackExchange.Redis;

namespace Excalibur.LeaderElection.Redis;

internal sealed class RedisLeaderElectionBuilder : IRedisLeaderElectionBuilder
{
	internal IConnectionMultiplexer? MultiplexerInstance { get; private set; }
	internal Func<IServiceProvider, IConnectionMultiplexer>? MultiplexerFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal string? ConnectionStringValue { get; private set; }
	internal int? DatabaseValue { get; private set; }
	internal string? KeyPrefixValue { get; private set; }
	internal string? LockKeyValue { get; private set; }

	public IRedisLeaderElectionBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ConnectionStringValue = connectionString;
		MultiplexerInstance = null;
		MultiplexerFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IRedisLeaderElectionBuilder ConnectionMultiplexer(IConnectionMultiplexer multiplexer)
	{
		ArgumentNullException.ThrowIfNull(multiplexer);
		MultiplexerInstance = multiplexer;
		ConnectionStringValue = null;
		MultiplexerFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IRedisLeaderElectionBuilder ConnectionMultiplexerFactory(Func<IServiceProvider, IConnectionMultiplexer> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		MultiplexerFactoryFunc = factory;
		ConnectionStringValue = null;
		MultiplexerInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	public IRedisLeaderElectionBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ConnectionStringValue = null;
		MultiplexerInstance = null;
		MultiplexerFactoryFunc = null;
		return this;
	}

	public IRedisLeaderElectionBuilder KeyPrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		KeyPrefixValue = prefix;
		return this;
	}

	public IRedisLeaderElectionBuilder Database(int database)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(database);
		DatabaseValue = database;
		return this;
	}

	public IRedisLeaderElectionBuilder LockKey(string lockKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
		LockKeyValue = lockKey;
		return this;
	}
}
