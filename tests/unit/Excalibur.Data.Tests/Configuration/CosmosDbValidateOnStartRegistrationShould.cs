// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Inbox;
using Excalibur.Data.CosmosDb.Outbox;
using Excalibur.Data.CosmosDb.Saga;
using Excalibur.Data.CosmosDb.Snapshots;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies that CosmosDb provider DI registrations wire up
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> correctly.
/// Sprint 564 S564.47: CosmosDb ValidateOnStart verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CosmosDbValidateOnStartRegistrationShould
{
	#region CosmosDb Core

	[Fact]
	public void CosmosDb_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDb(opts =>
		{
			opts.ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;";
		});

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CosmosDbOptions>>();
		validators.ShouldNotBeEmpty("AddCosmosDb should register IValidateOptions<CosmosDbOptions>");
	}

	[Fact]
	public void CosmosDb_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDb(opts =>
		{
			opts.MaxRetryAttempts = -1; // Violates [Range(0, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region CosmosDb SnapshotStore

	[Fact]
	public void CosmosDbSnapshotStore_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDbSnapshotStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CosmosDbSnapshotStoreOptions>>();
		validators.ShouldNotBeEmpty("AddCosmosDbSnapshotStore should register IValidateOptions<CosmosDbSnapshotStoreOptions>");
	}

	[Fact]
	public void CosmosDbSnapshotStore_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDbSnapshotStore(opts =>
		{
			opts.ContainerThroughput = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbSnapshotStoreOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region CosmosDb Outbox

	[Fact]
	public void CosmosDbOutbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDbOutbox(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CosmosDbOutboxOptions>>();
		validators.ShouldNotBeEmpty("AddCosmosDbOutbox should register IValidateOptions<CosmosDbOutboxOptions>");
	}

	#endregion

	#region CosmosDb Inbox

	[Fact]
	public void CosmosDbInbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDbInboxStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CosmosDbInboxOptions>>();
		validators.ShouldNotBeEmpty("AddCosmosDbInboxStore should register IValidateOptions<CosmosDbInboxOptions>");
	}

	[Fact]
	public void CosmosDbInbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDbInboxStore(opts =>
		{
			opts.MaxRetryWaitTimeInSeconds = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbInboxOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region CosmosDb Saga

	[Fact]
	public void CosmosDbSaga_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDbSagaStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CosmosDbSagaOptions>>();
		validators.ShouldNotBeEmpty("AddCosmosDbSagaStore should register IValidateOptions<CosmosDbSagaOptions>");
	}

	#endregion

	#region CosmosDb CDC

	[Fact]
	public void CosmosDbCdc_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddCosmosDbCdc(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CosmosDbCdcOptions>>();
		validators.ShouldNotBeEmpty("AddCosmosDbCdc should register IValidateOptions<CosmosDbCdcOptions>");
	}

	#endregion
}
