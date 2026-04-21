// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

namespace Excalibur.Cdc.DynamoDb;

/// <summary>
/// Extension methods for <see cref="IDynamoDbCdcBuilder"/>.
/// </summary>
public static class DynamoDbCdcBuilderExtensions
{
	/// <summary>Sets the maximum number of records per batch.</summary>
	public static IDynamoDbCdcBuilder MaxBatchSize(this IDynamoDbCdcBuilder builder, int maxBatchSize)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((DynamoDbCdcBuilder)builder).MaxBatchSize(maxBatchSize);
	}

	/// <summary>Sets the interval between stream polls.</summary>
	public static IDynamoDbCdcBuilder PollInterval(this IDynamoDbCdcBuilder builder, TimeSpan interval)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((DynamoDbCdcBuilder)builder).PollInterval(interval);
	}

	/// <summary>Configures a separate DynamoDB client factory for state persistence.</summary>
	public static IDynamoDbCdcBuilder WithStateStore(this IDynamoDbCdcBuilder builder, Func<IServiceProvider, IAmazonDynamoDB> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((DynamoDbCdcBuilder)builder).WithStateStore(clientFactory);
	}
}
