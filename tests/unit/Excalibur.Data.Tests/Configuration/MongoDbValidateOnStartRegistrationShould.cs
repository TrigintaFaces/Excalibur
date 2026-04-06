// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.MongoDB;
using Excalibur.Data.MongoDB.Projections;
using Excalibur.Data.MongoDB.Snapshots;
using Excalibur.EventSourcing.MongoDB;
using Excalibur.Inbox.MongoDB;
using Excalibur.Outbox.MongoDB;
using Excalibur.Saga.MongoDB;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies that MongoDB provider DI registrations wire up
/// <c>ValidateOnStart()</c> correctly.
/// Sprint 564 S564.46: MongoDB provider DI ValidateOnStart verification.
/// Sprint 750: ValidateDataAnnotations removed (AOT-safe migration).
/// Packages that register explicit IValidateOptions validators (Outbox, Inbox, Saga, Snapshot)
/// still validate; others (EventStore, Cdc, ProjectionStore) only have ValidateOnStart
/// without an explicit validator, so we verify IOptions resolvability instead.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MongoDbValidateOnStartRegistrationShould
{
	#region EventStore

	[Fact]
	public void MongoDbEventStore_RegistersOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbEventStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<MongoDbEventStoreOptions>>();
		options.ShouldNotBeNull("AddMongoDbEventStore should register IOptions<MongoDbEventStoreOptions>");
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void MongoDbEventStore_ConfiguresOptionsCorrectly()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbEventStore(opts =>
		{
			opts.ConnectionString = "mongodb://testhost:27017";
			opts.DatabaseName = "test-db";
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbEventStoreOptions>>();
		options.Value.ConnectionString.ShouldBe("mongodb://testhost:27017");
		options.Value.DatabaseName.ShouldBe("test-db");
	}

	#endregion

	#region SnapshotStore

	[Fact]
	public void MongoDbSnapshotStore_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbSnapshotStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<MongoDbSnapshotStoreOptions>>();
		validators.ShouldNotBeEmpty("AddMongoDbSnapshotStore should register IValidateOptions<MongoDbSnapshotStoreOptions>");
	}

	[Fact]
	public void MongoDbSnapshotStore_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbSnapshotStore(opts =>
		{
			opts.ConnectionString = ""; // Violates required string check in Validate()
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbSnapshotStoreOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Outbox

	[Fact]
	public void MongoDbOutbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbOutboxStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<MongoDbOutboxOptions>>();
		validators.ShouldNotBeEmpty("AddMongoDbOutboxStore should register IValidateOptions<MongoDbOutboxOptions>");
	}

	[Fact]
	public void MongoDbOutbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbOutboxStore(opts =>
		{
			opts.ConnectionString = ""; // Violates required string check in Validate()
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbOutboxOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Inbox

	[Fact]
	public void MongoDbInbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbInboxStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<MongoDbInboxOptions>>();
		validators.ShouldNotBeEmpty("AddMongoDbInboxStore should register IValidateOptions<MongoDbInboxOptions>");
	}

	[Fact]
	public void MongoDbInbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbInboxStore(opts =>
		{
			opts.ConnectionString = ""; // Violates required string check in Validate()
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbInboxOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Saga

	[Fact]
	public void MongoDbSaga_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbSagaStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<MongoDbSagaOptions>>();
		validators.ShouldNotBeEmpty("AddMongoDbSagaStore should register IValidateOptions<MongoDbSagaOptions>");
	}

	[Fact]
	public void MongoDbSaga_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbSagaStore(opts =>
		{
			opts.ConnectionString = ""; // Violates required string check in Validate()
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbSagaOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Cdc

	[Fact]
	public void MongoDbCdc_RegistersOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbCdc(opts => { });

		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<MongoDbCdcOptions>>();
		options.ShouldNotBeNull("AddMongoDbCdc should register IOptions<MongoDbCdcOptions>");
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void MongoDbCdc_ConfiguresOptionsCorrectly()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbCdc(opts =>
		{
			opts.BatchSize = 50;
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbCdcOptions>>();
		options.Value.BatchSize.ShouldBe(50);
	}

	#endregion

	#region ProjectionStore

	[Fact]
	public void MongoDbProjectionStore_RegistersOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbProjectionStore<object>(opts => { });

		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<MongoDbProjectionStoreOptions>>();
		options.ShouldNotBeNull("AddMongoDbProjectionStore should register IOptions<MongoDbProjectionStoreOptions>");
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void MongoDbProjectionStore_ConfiguresOptionsCorrectly()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbProjectionStore<object>(opts =>
		{
			opts.ConnectionString = "mongodb://testhost:27017";
			opts.DatabaseName = "projections-db";
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbProjectionStoreOptions>>();
		options.Value.ConnectionString.ShouldBe("mongodb://testhost:27017");
		options.Value.DatabaseName.ShouldBe("projections-db");
	}

	#endregion

	#region Cross-Cutting

	[Fact]
	public void AllMongoDbRegistrations_EachHaveOptionsResolvable()
	{
		var services = new ServiceCollection();

		_ = services.AddMongoDbEventStore(opts => { });
		_ = services.AddMongoDbSnapshotStore(opts => { });
		_ = services.AddMongoDbOutboxStore(opts => { });
		_ = services.AddMongoDbInboxStore(opts => { });
		_ = services.AddMongoDbSagaStore(opts => { });
		_ = services.AddMongoDbCdc(opts => { });
		_ = services.AddMongoDbProjectionStore<object>(opts => { });

		using var provider = services.BuildServiceProvider();

		// All options should be resolvable
		provider.GetRequiredService<IOptions<MongoDbEventStoreOptions>>().Value.ShouldNotBeNull();
		provider.GetRequiredService<IOptions<MongoDbSnapshotStoreOptions>>().Value.ShouldNotBeNull();
		provider.GetRequiredService<IOptions<MongoDbOutboxOptions>>().Value.ShouldNotBeNull();
		provider.GetRequiredService<IOptions<MongoDbInboxOptions>>().Value.ShouldNotBeNull();
		provider.GetRequiredService<IOptions<MongoDbSagaOptions>>().Value.ShouldNotBeNull();
		provider.GetRequiredService<IOptions<MongoDbCdcOptions>>().Value.ShouldNotBeNull();
		provider.GetRequiredService<IOptions<MongoDbProjectionStoreOptions>>().Value.ShouldNotBeNull();

		// Packages with explicit validators should have IValidateOptions registered
		provider.GetServices<IValidateOptions<MongoDbSnapshotStoreOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbOutboxOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbInboxOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbSagaOptions>>().ShouldNotBeEmpty();
	}

	#endregion
}
