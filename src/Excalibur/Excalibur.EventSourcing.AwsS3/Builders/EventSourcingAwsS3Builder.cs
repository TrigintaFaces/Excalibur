// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

namespace Excalibur.EventSourcing.AwsS3;

internal sealed class EventSourcingAwsS3Builder : IEventSourcingAwsS3Builder
{
	internal string? BucketNameValue { get; private set; }
	internal string? KeyPrefixValue { get; private set; }
	internal string? ServiceUrlValue { get; private set; }
	internal string? RegionValue { get; private set; }
	internal IAmazonS3? ClientInstance { get; private set; }
	internal Func<IServiceProvider, IAmazonS3>? ClientFactoryFunc { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public IEventSourcingAwsS3Builder BucketName(string bucketName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
		BucketNameValue = bucketName;
		return this;
	}

	public IEventSourcingAwsS3Builder KeyPrefix(string keyPrefix)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);
		KeyPrefixValue = keyPrefix;
		return this;
	}

	public IEventSourcingAwsS3Builder ServiceUrl(string serviceUrl)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl);
		ServiceUrlValue = serviceUrl;
		ClientInstance = null;
		ClientFactoryFunc = null;
		RegionValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingAwsS3Builder Region(string region)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(region);
		RegionValue = region;
		ClientInstance = null;
		ClientFactoryFunc = null;
		ServiceUrlValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingAwsS3Builder Client(IAmazonS3 client)
	{
		ArgumentNullException.ThrowIfNull(client);
		ClientInstance = client;
		ClientFactoryFunc = null;
		ServiceUrlValue = null;
		RegionValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingAwsS3Builder ClientFactory(Func<IServiceProvider, IAmazonS3> clientFactory)
	{
		ArgumentNullException.ThrowIfNull(clientFactory);
		ClientFactoryFunc = clientFactory;
		ClientInstance = null;
		ServiceUrlValue = null;
		RegionValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IEventSourcingAwsS3Builder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ClientInstance = null;
		ClientFactoryFunc = null;
		ServiceUrlValue = null;
		RegionValue = null;
		return this;
	}
}
