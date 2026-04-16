// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using StackExchange.Redis;

namespace Excalibur.Outbox.Redis;

internal sealed class RedisOutboxBuilder : IRedisOutboxBuilder
{
	private readonly RedisOutboxOptions _options;

	internal RedisOutboxBuilder(RedisOutboxOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal IConnectionMultiplexer? MultiplexerInstance { get; private set; }
	internal Func<IServiceProvider, IConnectionMultiplexer>? MultiplexerFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal string? ConnectionStringValue { get; private set; }
	internal int? DatabaseValue { get; private set; }

	public IRedisOutboxBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ConnectionStringValue = connectionString;
		_options.ConnectionString = connectionString;
		MultiplexerInstance = null;
		MultiplexerFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IRedisOutboxBuilder ConnectionMultiplexer(IConnectionMultiplexer multiplexer)
	{
		ArgumentNullException.ThrowIfNull(multiplexer);
		MultiplexerInstance = multiplexer;
		_options.ConnectionString = null!;
		ConnectionStringValue = null;
		MultiplexerFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	public IRedisOutboxBuilder ConnectionMultiplexerFactory(Func<IServiceProvider, IConnectionMultiplexer> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		MultiplexerFactoryFunc = factory;
		_options.ConnectionString = null!;
		ConnectionStringValue = null;
		MultiplexerInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	public IRedisOutboxBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null!;
		ConnectionStringValue = null;
		MultiplexerInstance = null;
		MultiplexerFactoryFunc = null;
		return this;
	}

	public IRedisOutboxBuilder KeyPrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		_options.KeyPrefix = prefix;
		return this;
	}

	public IRedisOutboxBuilder Database(int database)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(database);
		DatabaseValue = database;
		return this;
	}

}
