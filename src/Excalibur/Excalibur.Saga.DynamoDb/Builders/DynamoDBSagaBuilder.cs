// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.DynamoDBv2;

namespace Excalibur.Saga.DynamoDb;

internal sealed class DynamoDBSagaBuilder : IDynamoDBSagaBuilder
{
	internal IAmazonDynamoDB? ClientInstance { get; private set; }
	internal Func<IServiceProvider, IAmazonDynamoDB>? ClientFactoryFunc { get; private set; }
	internal string? ServiceUrlValue { get; private set; }
	internal RegionEndpoint? RegionValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal string? TableNameValue { get; private set; }
	internal string? TablePrefixValue { get; private set; }

	public IDynamoDBSagaBuilder ServiceUrl(string serviceUrl)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
		ServiceUrlValue = serviceUrl;
		ClientInstance = null;
		ClientFactoryFunc = null;
		RegionValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IDynamoDBSagaBuilder Region(RegionEndpoint region)
	{
		ArgumentNullException.ThrowIfNull(region);
		RegionValue = region;
		ClientInstance = null;
		ClientFactoryFunc = null;
		ServiceUrlValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IDynamoDBSagaBuilder Client(IAmazonDynamoDB client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		ClientFactoryFunc = null;
		ServiceUrlValue = null;
		RegionValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IDynamoDBSagaBuilder ClientFactory(Func<IServiceProvider, IAmazonDynamoDB> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ClientFactoryFunc = clientFactory;
		ClientInstance = null;
		ServiceUrlValue = null;
		RegionValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IDynamoDBSagaBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ClientInstance = null;
		ClientFactoryFunc = null;
		ServiceUrlValue = null;
		RegionValue = null;
		return this;
	}

	public IDynamoDBSagaBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		TableNameValue = tableName;
		return this;
	}

	public IDynamoDBSagaBuilder TablePrefix(string prefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
		TablePrefixValue = prefix;
		return this;
	}

}
