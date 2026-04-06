// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Outbox.MongoDB;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.Builders;

/// <summary>
/// Sprint 621 C.2: Tests for IOutboxBuilder Use*() provider extension methods
/// (UseMongoDB, UseCosmosDb, UseDynamoDb, UseFirestore).
/// Follows the SqlServerOutboxBuilderShould pattern using AddExcaliburOutbox entry point.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBuilderProviderExtensionsShould
{
	#region UseMongoDB

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseMongoDB()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IOutboxBuilder)null!).UseMongoDB());
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining_UseMongoDB()
	{
		// Arrange
		var services = new ServiceCollection();
		IOutboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburOutbox(outbox =>
		{
			var result = outbox.UseMongoDB();
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterOutboxStore_WhenCallingUseMongoDB()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseMongoDB());

		// Assert -- MongoDB outbox registers IOutboxStore
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore));
	}

	[Fact]
	public void RegisterMongoDbOutboxOptions_WhenCallingUseMongoDB()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseMongoDB());

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<MongoDbOutboxOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseMongoDB()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseMongoDB(opts =>
		{
			opts.ConnectionString = "mongodb://localhost:27017";
			opts.DatabaseName = "test-outbox-db";
		}));

		// Assert -- resolve options to verify configure delegate was stored
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbOutboxOptions>>().Value;
		options.ConnectionString.ShouldBe("mongodb://localhost:27017");
		options.DatabaseName.ShouldBe("test-outbox-db");
	}

	[Fact]
	public void AcceptNullConfigure_WhenCallingUseMongoDB()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- should not throw with null configure
		services.AddExcaliburOutbox(outbox => outbox.UseMongoDB(null));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore));
	}

	#endregion

	#region UseCosmosDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCosmosDb()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IOutboxBuilder)null!).UseCosmosDb());
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining_UseCosmosDb()
	{
		// Arrange
		var services = new ServiceCollection();
		IOutboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburOutbox(outbox =>
		{
			var result = outbox.UseCosmosDb();
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterCloudNativeOutboxStore_WhenCallingUseCosmosDb()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseCosmosDb());

		// Assert -- CosmosDb outbox registers ICloudNativeOutboxStore
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	[Fact]
	public void RegisterCosmosDbOutboxOptions_WhenCallingUseCosmosDb()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseCosmosDb());

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<CosmosDbOutboxOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseCosmosDb()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseCosmosDb(opts =>
		{
			opts.Connection.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;";
			opts.DatabaseName = "test-cosmos-outbox";
		}));

		// Assert -- resolve options to verify configure delegate was stored
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbOutboxOptions>>().Value;
		options.Connection.ConnectionString.ShouldBe("AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;");
		options.DatabaseName.ShouldBe("test-cosmos-outbox");
	}

	[Fact]
	public void AcceptNullConfigure_WhenCallingUseCosmosDb()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseCosmosDb(null));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	#endregion

	#region UseDynamoDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseDynamoDb()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IOutboxBuilder)null!).UseDynamoDb());
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining_UseDynamoDb()
	{
		// Arrange
		var services = new ServiceCollection();
		IOutboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburOutbox(outbox =>
		{
			var result = outbox.UseDynamoDb();
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterCloudNativeOutboxStore_WhenCallingUseDynamoDb()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseDynamoDb());

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	[Fact]
	public void RegisterDynamoDbOutboxOptions_WhenCallingUseDynamoDb()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseDynamoDb());

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<DynamoDbOutboxOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseDynamoDb()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseDynamoDb(opts =>
		{
			opts.TableName = "test-outbox";
			opts.Connection.ServiceUrl = "http://localhost:8000"; // Required by IValidateOptions
		}));

		// Assert -- resolve options to verify configure delegate was stored
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbOutboxOptions>>().Value;
		options.TableName.ShouldBe("test-outbox");
	}

	[Fact]
	public void AcceptNullConfigure_WhenCallingUseDynamoDb()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseDynamoDb(null));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	#endregion

	#region UseFirestore

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseFirestore()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IOutboxBuilder)null!).UseFirestore());
	}

	[Fact]
	public void ReturnBuilder_ForFluentChaining_UseFirestore()
	{
		// Arrange
		var services = new ServiceCollection();
		IOutboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburOutbox(outbox =>
		{
			var result = outbox.UseFirestore();
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterCloudNativeOutboxStore_WhenCallingUseFirestore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore());

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	[Fact]
	public void RegisterFirestoreOutboxOptions_WhenCallingUseFirestore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore());

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<FirestoreOutboxOptions>));
	}

	[Fact]
	public void InvokeConfigureDelegate_WhenCallingUseFirestore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore(opts =>
		{
			opts.CollectionName = "test-outbox";
			opts.ProjectId = "test-project"; // Required by IValidateOptions
		}));

		// Assert -- resolve options to verify configure delegate was stored
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<FirestoreOutboxOptions>>().Value;
		options.CollectionName.ShouldBe("test-outbox");
	}

	[Fact]
	public void AcceptNullConfigure_WhenCallingUseFirestore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore(null));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	#endregion

	#region Fluent Chaining with Other Builder Methods

	[Fact]
	public void SupportFluentChaining_WithProcessingAndCleanup()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- chain provider Use*() with other builder methods
		services.AddExcaliburOutbox(outbox => outbox
			.UseCosmosDb()
			.EnableBackgroundProcessing());

		// Assert -- no exception, services registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	#endregion

	#region E.1: Firestore CS0121 Regression Tests (Sprint 622)

	[Fact]
	public void RegisterFirestoreOutboxStore_WithoutAmbiguity()
	{
		// Regression: Sprint 621 had CS0121 due to duplicate AddFirestoreOutboxStore in
		// both Data.Firestore and Outbox.Firestore. A.1 removed the Data.Firestore duplicate.
		// This test verifies no ambiguity by calling the method directly on ServiceCollection.

		// Arrange
		var services = new ServiceCollection();

		// Act -- this would fail at compile time if CS0121 was still present
		services.AddFirestoreOutboxStore(_ => { });

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	[Fact]
	public void NotDuplicateServiceRegistrations_WhenCallingUseFirestore()
	{
		// Regression: Ensure UseFirestore() only registers ICloudNativeOutboxStore once

		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore());

		// Assert -- count ICloudNativeOutboxStore registrations
		var registrations = services.Where(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore)).ToList();
		registrations.Count.ShouldBe(1,
			"UseFirestore() should register ICloudNativeOutboxStore exactly once");
	}

	[Fact]
	public void UseCanonicalFirestoreOutboxOptions()
	{
		// Regression: Verify the options type is from Excalibur.Outbox.Firestore namespace
		// (not the duplicate that was in Data.Firestore.Outbox)

		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseFirestore(opts =>
		{
			opts.ProjectId = "test-project";
		}));

		// Assert
		var optionsDescriptor = services.FirstOrDefault(sd =>
			sd.ServiceType == typeof(IConfigureOptions<FirestoreOutboxOptions>));
		optionsDescriptor.ShouldNotBeNull();

		// Verify the options type is from the canonical namespace
		typeof(FirestoreOutboxOptions).Namespace.ShouldBe("Excalibur.Outbox.Firestore",
			"FirestoreOutboxOptions should be from the canonical Outbox.Firestore package");
	}

	#endregion
}
