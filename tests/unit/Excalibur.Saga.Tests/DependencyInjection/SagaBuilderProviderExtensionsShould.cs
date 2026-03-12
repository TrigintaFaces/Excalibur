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
			((ISagaBuilder)null!).UseMongoDB());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseMongoDB()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseMongoDB();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_WhenCallingUseMongoDB()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseMongoDB();

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
		builder.UseMongoDB();

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
		builder.UseMongoDB(opts =>
		{
			opts.ConnectionString = "mongodb://localhost:27017";
			opts.DatabaseName = "test-db";
		});

		// Assert -- resolve options to trigger the deferred configure delegate
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbSagaOptions>>().Value;
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
		options.DatabaseName.ShouldBe("test-db");
	}

	[Fact]
	public void AcceptNullConfigure_WhenCallingUseMongoDB()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act -- should not throw with null configure
		var result = builder.UseMongoDB(null);

		// Assert
		result.ShouldBeSameAs(builder);
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	#endregion

	#region UseCosmosDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCosmosDb()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseCosmosDb());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseCosmosDb();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_WhenCallingUseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseCosmosDb();

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
		builder.UseCosmosDb();

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<CosmosDbSagaOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseCosmosDb(opts =>
		{
			opts.Client.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;";
			opts.DatabaseName = "test-cosmos-db";
		});

		// Assert -- resolve options to trigger the deferred configure delegate
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbSagaOptions>>().Value;
		options.Client.ConnectionString.ShouldBe("AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;");
		options.DatabaseName.ShouldBe("test-cosmos-db");
	}

	[Fact]
	public void AcceptNullConfigure_WhenCallingUseCosmosDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseCosmosDb(null);

		// Assert
		result.ShouldBeSameAs(builder);
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	#endregion

	#region UseDynamoDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseDynamoDb()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseDynamoDb());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseDynamoDb();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_WhenCallingUseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseDynamoDb();

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
		builder.UseDynamoDb();

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<DynamoDbSagaOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseDynamoDb(opts =>
		{
			opts.TableName = "test-sagas";
			opts.Connection.Region = "us-east-1";
		});

		// Assert -- resolve options to trigger the deferred configure delegate
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbSagaOptions>>().Value;
		options.TableName.ShouldBe("test-sagas");
		options.Connection.Region.ShouldBe("us-east-1");
	}

	[Fact]
	public void AcceptNullConfigure_WhenCallingUseDynamoDb()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseDynamoDb(null);

		// Assert
		result.ShouldBeSameAs(builder);
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	#endregion

	#region UseFirestore

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseFirestore()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseFirestore());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseFirestore();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterSagaStore_WhenCallingUseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseFirestore();

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
		builder.UseFirestore();

		// Assert
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<FirestoreSagaOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		builder.UseFirestore(opts =>
		{
			opts.CollectionName = "test-sagas";
		});

		// Assert -- resolve options to trigger the deferred configure delegate
		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<FirestoreSagaOptions>>().Value;
		options.CollectionName.ShouldBe("test-sagas");
	}

	[Fact]
	public void AcceptNullConfigure_WhenCallingUseFirestore()
	{
		// Arrange
		var builder = new TestSagaBuilder();

		// Act
		var result = builder.UseFirestore(null);

		// Assert
		result.ShouldBeSameAs(builder);
		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISagaStore));
	}

	#endregion

	#region Fluent Chaining Across Providers

	[Fact]
	public void SupportFluentChaining_WithBuilderEntry()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- verify UseMongoDB works through AddExcaliburSaga builder entry point
		services.AddExcaliburSaga(saga => saga.UseMongoDB());

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
			.UseCosmosDb()
			.WithOrchestration()
			.WithTimeouts();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
