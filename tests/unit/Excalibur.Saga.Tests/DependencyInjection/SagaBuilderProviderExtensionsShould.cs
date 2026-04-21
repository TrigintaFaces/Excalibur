// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.CosmosDb;
using Excalibur.Saga.DynamoDb;
using Excalibur.Saga.Firestore;
using Excalibur.Saga.MongoDB;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Sprint 621 C.1: Tests for ISagaBuilder Use*() provider extension methods
/// (UseMongoDB, UseCosmosDb, UseDynamoDb, UseFirestore).
/// Follows the SagaBuilderSqlServerExtensionsShould pattern from S617.
/// Updated for Phase C builder API migration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaBuilderProviderExtensionsShould
{
	private sealed class TestSagaBuilder : ISagaBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	#region UseMongoDB

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseMongoDB()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseMongoDB(mongo => mongo.ConnectionString("mongodb://localhost:27017")));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseMongoDB()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseMongoDB(mongo => mongo.ConnectionString("mongodb://localhost:27017"));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_WhenCallingUseMongoDB()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseMongoDB(mongo => mongo.ConnectionString("mongodb://localhost:27017"));

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	[Fact]
	public void RegisterMongoDbSagaOptions_WhenCallingUseMongoDB()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseMongoDB(mongo => mongo.ConnectionString("mongodb://localhost:27017"));

		// Assert -- IOptions<MongoDbSagaOptions> should be configured
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<MongoDbSagaOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseMongoDB()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseMongoDB(mongo =>
		{
			mongo.ConnectionString("mongodb://localhost:27017")
				.DatabaseName("test-db");
		});

		// Assert -- resolve options to trigger the deferred configure delegate
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbSagaOptions>>().Value;
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
		options.DatabaseName.ShouldBe("test-db");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseMongoDB()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert -- null configure should throw
		Should.Throw<ArgumentNullException>(() =>
			builder.UseMongoDB((Action<IMongoDBSagaBuilder>)null!));
	}

	#endregion

	#region UseCosmosDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCosmosDb()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;")));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;"));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_WhenCallingUseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;"));

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	[Fact]
	public void RegisterCosmosDbSagaOptions_WhenCallingUseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;"));

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<CosmosDbSagaOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();
		var configureInvoked = false;

		// Act
		builder.UseCosmosDb(cosmos =>
		{
			configureInvoked = true;
			cosmos.ConnectionString("AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;")
				.DatabaseName("test-cosmos-db");
		});

		// Assert -- verify the configure delegate was invoked eagerly by the builder
		configureInvoked.ShouldBeTrue();
		// Verify IConfigureOptions<CosmosDbSagaOptions> is registered
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<CosmosDbSagaOptions>));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert -- null configure should throw
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCosmosDb((Action<ICosmosDbSagaBuilder>)null!));
	}

	#endregion

	#region UseDynamoDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseDynamoDb()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseDynamoDb(dynamo => dynamo.TableName("test")));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseDynamoDb(dynamo => dynamo.TableName("test-sagas"));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_WhenCallingUseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseDynamoDb(dynamo => dynamo.TableName("test-sagas"));

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	[Fact]
	public void RegisterDynamoDbSagaOptions_WhenCallingUseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseDynamoDb(dynamo => dynamo.TableName("test-sagas"));

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<DynamoDbSagaOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();
		var configureInvoked = false;

		// Act
		builder.UseDynamoDb(dynamo =>
		{
			configureInvoked = true;
			dynamo.TableName("test-sagas");
		});

		// Assert -- verify the configure delegate was invoked eagerly by the builder
		configureInvoked.ShouldBeTrue();
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<DynamoDbSagaOptions>));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert -- null configure should throw
		Should.Throw<ArgumentNullException>(() =>
			builder.UseDynamoDb((Action<IDynamoDBSagaBuilder>)null!));
	}

	#endregion

	#region UseFirestore

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseFirestore()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseFirestore(fs => fs.ProjectId("test-project")));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseFirestore(fs => fs.ProjectId("test-project"));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_WhenCallingUseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseFirestore(fs => fs.ProjectId("test-project"));

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	[Fact]
	public void RegisterFirestoreSagaOptions_WhenCallingUseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseFirestore(fs => fs.ProjectId("test-project"));

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<FirestoreSagaOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();
		var configureInvoked = false;

		// Act
		builder.UseFirestore(fs =>
		{
			configureInvoked = true;
			fs.ProjectId("test-project")
				.CollectionName("test-sagas");
		});

		// Assert -- verify the configure delegate was invoked eagerly by the builder
		configureInvoked.ShouldBeTrue();
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<FirestoreSagaOptions>));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act & Assert -- null configure should throw
		Should.Throw<ArgumentNullException>(() =>
			builder.UseFirestore((Action<IFirestoreSagaBuilder>)null!));
	}

	#endregion

	#region Fluent Chaining Across Providers

	[Fact]
	public void SupportFluentChaining_WithBuilderEntry()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- verify UseMongoDB works through AddExcaliburSaga builder entry point
		services.AddExcaliburSaga(saga => saga.UseMongoDB(mongo => mongo.ConnectionString("mongodb://localhost:27017")));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	[Fact]
	public void SupportFluentChaining_WithOtherBuilderExtensions()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act -- chain a provider Use*() with existing saga builder extensions
		var result = builder
			.UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;"))
			.WithOrchestration()
			.WithTimeouts();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
