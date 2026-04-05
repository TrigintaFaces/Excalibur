// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Extension methods for <see cref="IMongoDbCdcBuilder"/>.
/// </summary>
public static class MongoDbCdcBuilderExtensions
{
	/// <summary>Sets the number of changes to process in a single batch.</summary>
	public static IMongoDbCdcBuilder BatchSize(this IMongoDbCdcBuilder builder, int batchSize)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((MongoDbCdcBuilder)builder).BatchSize(batchSize);
	}

	/// <summary>Sets the interval between reconnection attempts.</summary>
	public static IMongoDbCdcBuilder ReconnectInterval(this IMongoDbCdcBuilder builder, TimeSpan interval)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((MongoDbCdcBuilder)builder).ReconnectInterval(interval);
	}

	/// <summary>Configures a separate connection for CDC state persistence.</summary>
	public static IMongoDbCdcBuilder WithStateStore(this IMongoDbCdcBuilder builder, Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((MongoDbCdcBuilder)builder).WithStateStore(configure);
	}
}
