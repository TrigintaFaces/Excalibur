// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.S3;
using Azure.Storage.Blobs;
using Excalibur.EventSourcing.AwsS3;
using Excalibur.EventSourcing.AzureBlob;
using Excalibur.EventSourcing.Gcs;
using Google.Cloud.Storage.V1;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "EventSourcing")]
public sealed class ColdArchiveBuilderCompatibilityShould
{
	[Fact]
	public void PreservePriorGcsBuilderImplementations()
	{
		IEventSourcingGcsBuilder builder = new PriorGcsBuilder();
		builder.BucketName("archive").ShouldBeSameAs(builder);
		Should.Throw<NotSupportedException>(() => builder.Layout(ColdArchiveLayout.TypedV2)).Message.ShouldContain("Layout");
	}

	private sealed class PriorGcsBuilder : IEventSourcingGcsBuilder
	{
		public IEventSourcingGcsBuilder ProjectId(string projectId) => this;
		public IEventSourcingGcsBuilder BucketName(string bucketName) => this;
		public IEventSourcingGcsBuilder ObjectPrefix(string objectPrefix) => this;
		public IEventSourcingGcsBuilder CredentialsPath(string credentialsPath) => this;
		public IEventSourcingGcsBuilder CredentialsJson(string credentialsJson) => this;
		public IEventSourcingGcsBuilder Client(StorageClient client) => this;
		public IEventSourcingGcsBuilder ClientFactory(Func<IServiceProvider, StorageClient> clientFactory) => this;
		public IEventSourcingGcsBuilder BindConfiguration(string sectionPath) => this;
	}

	[Fact]
	public void PreservePriorAzureBuilderImplementations()
	{
		IEventSourcingAzureBlobBuilder builder = new PriorAzureBuilder();
		builder.ContainerName("archive").ShouldBeSameAs(builder);
		Should.Throw<NotSupportedException>(() => builder.Layout(ColdArchiveLayout.TypedV2)).Message.ShouldContain("Layout");
	}

	[Fact]
	public void PreservePriorS3BuilderImplementations()
	{
		IEventSourcingAwsS3Builder builder = new PriorS3Builder();
		builder.BucketName("archive").ShouldBeSameAs(builder);
		Should.Throw<NotSupportedException>(() => builder.Layout(ColdArchiveLayout.TypedV2)).Message.ShouldContain("Layout");
	}

	// Deliberately implement only the pre-Layout interface surface. These must continue to compile.
	private sealed class PriorAzureBuilder : IEventSourcingAzureBlobBuilder
	{
		public IEventSourcingAzureBlobBuilder ConnectionString(string connectionString) => this;
		public IEventSourcingAzureBlobBuilder ContainerName(string containerName) => this;
		public IEventSourcingAzureBlobBuilder CreateContainerIfNotExists(bool create = true) => this;
		public IEventSourcingAzureBlobBuilder Client(BlobServiceClient client) => this;
		public IEventSourcingAzureBlobBuilder ClientFactory(Func<IServiceProvider, BlobServiceClient> clientFactory) => this;
		public IEventSourcingAzureBlobBuilder BindConfiguration(string sectionPath) => this;
	}

	private sealed class PriorS3Builder : IEventSourcingAwsS3Builder
	{
		public IEventSourcingAwsS3Builder BucketName(string bucketName) => this;
		public IEventSourcingAwsS3Builder KeyPrefix(string keyPrefix) => this;
		public IEventSourcingAwsS3Builder ServiceUrl(string serviceUrl) => this;
		public IEventSourcingAwsS3Builder Region(string region) => this;
		public IEventSourcingAwsS3Builder Client(IAmazonS3 client) => this;
		public IEventSourcingAwsS3Builder ClientFactory(Func<IServiceProvider, IAmazonS3> clientFactory) => this;
		public IEventSourcingAwsS3Builder BindConfiguration(string sectionPath) => this;
	}
}
