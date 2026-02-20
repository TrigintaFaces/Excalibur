// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Cdc;
using Excalibur.Data.MongoDB.EventSourcing;
using Excalibur.Data.MongoDB.Inbox;
using Excalibur.Data.MongoDB.Outbox;
using Excalibur.Data.MongoDB.Projections;
using Excalibur.Data.MongoDB.Saga;
using Excalibur.Data.MongoDB.Snapshots;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies that MongoDB provider DI registrations wire up
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> correctly.
/// Sprint 564 S564.46: MongoDB provider DI ValidateOnStart verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbValidateOnStartRegistrationShould
{
	#region EventStore

	[Fact]
	public void MongoDbEventStore_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbEventStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<MongoDbEventStoreOptions>>();
		validators.ShouldNotBeEmpty("AddMongoDbEventStore should register IValidateOptions<MongoDbEventStoreOptions>");
	}

	[Fact]
	public void MongoDbEventStore_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbEventStore(opts =>
		{
			opts.ServerSelectionTimeoutSeconds = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbEventStoreOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
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
			opts.ServerSelectionTimeoutSeconds = 0; // Violates [Range(1, int.MaxValue)]
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
			opts.ServerSelectionTimeoutSeconds = 0; // Violates [Range(1, int.MaxValue)]
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
			opts.ServerSelectionTimeoutSeconds = 0; // Violates [Range(1, int.MaxValue)]
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
			opts.ServerSelectionTimeoutSeconds = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbSagaOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Cdc

	[Fact]
	public void MongoDbCdc_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbCdc(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<MongoDbCdcOptions>>();
		validators.ShouldNotBeEmpty("AddMongoDbCdc should register IValidateOptions<MongoDbCdcOptions>");
	}

	[Fact]
	public void MongoDbCdc_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbCdc(opts =>
		{
			opts.BatchSize = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbCdcOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region ProjectionStore

	[Fact]
	public void MongoDbProjectionStore_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbProjectionStore<object>(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<MongoDbProjectionStoreOptions>>();
		validators.ShouldNotBeEmpty("AddMongoDbProjectionStore should register IValidateOptions<MongoDbProjectionStoreOptions>");
	}

	[Fact]
	public void MongoDbProjectionStore_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddMongoDbProjectionStore<object>(opts =>
		{
			opts.ServerSelectionTimeoutSeconds = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbProjectionStoreOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region Cross-Cutting

	[Fact]
	public void AllMongoDbRegistrations_EachHaveTheirOwnValidation()
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

		provider.GetServices<IValidateOptions<MongoDbEventStoreOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbSnapshotStoreOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbOutboxOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbInboxOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbSagaOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbCdcOptions>>().ShouldNotBeEmpty();
		provider.GetServices<IValidateOptions<MongoDbProjectionStoreOptions>>().ShouldNotBeEmpty();
	}

	#endregion
}
