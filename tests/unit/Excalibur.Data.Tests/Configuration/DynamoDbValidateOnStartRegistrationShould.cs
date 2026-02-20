// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Inbox;
using Excalibur.Data.DynamoDb.Outbox;
using Excalibur.Data.DynamoDb.Saga;
using Excalibur.Data.DynamoDb.Snapshots;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies that DynamoDb provider DI registrations wire up
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> correctly.
/// Sprint 564 S564.47: DynamoDb ValidateOnStart verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DynamoDbValidateOnStartRegistrationShould
{
	#region DynamoDb Core

	[Fact]
	public void DynamoDb_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDb(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbOptions>>();
		validators.ShouldNotBeEmpty("AddDynamoDb should register IValidateOptions<DynamoDbOptions>");
	}

	[Fact]
	public void DynamoDb_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDb(opts =>
		{
			opts.MaxRetryAttempts = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region DynamoDb SnapshotStore

	[Fact]
	public void DynamoDbSnapshotStore_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbSnapshotStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbSnapshotStoreOptions>>();
		validators.ShouldNotBeEmpty("AddDynamoDbSnapshotStore should register IValidateOptions<DynamoDbSnapshotStoreOptions>");
	}

	[Fact]
	public void DynamoDbSnapshotStore_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbSnapshotStore(opts =>
		{
			opts.MaxRetryAttempts = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbSnapshotStoreOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region DynamoDb Outbox

	[Fact]
	public void DynamoDbOutbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbOutbox(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbOutboxOptions>>();
		validators.ShouldNotBeEmpty("AddDynamoDbOutbox should register IValidateOptions<DynamoDbOutboxOptions>");
	}

	[Fact]
	public void DynamoDbOutbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbOutbox(opts =>
		{
			opts.MaxRetryAttempts = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbOutboxOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region DynamoDb Inbox

	[Fact]
	public void DynamoDbInbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbInboxStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbInboxOptions>>();
		validators.ShouldNotBeEmpty("AddDynamoDbInboxStore should register IValidateOptions<DynamoDbInboxOptions>");
	}

	[Fact]
	public void DynamoDbInbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbInboxStore(opts =>
		{
			opts.MaxRetryAttempts = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbInboxOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region DynamoDb Saga

	[Fact]
	public void DynamoDbSaga_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbSagaStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbSagaOptions>>();
		validators.ShouldNotBeEmpty("AddDynamoDbSagaStore should register IValidateOptions<DynamoDbSagaOptions>");
	}

	[Fact]
	public void DynamoDbSaga_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbSagaStore(opts =>
		{
			opts.MaxRetryAttempts = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbSagaOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion

	#region DynamoDb CDC

	[Fact]
	public void DynamoDbCdc_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbCdc(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbCdcOptions>>();
		validators.ShouldNotBeEmpty("AddDynamoDbCdc should register IValidateOptions<DynamoDbCdcOptions>");
	}

	[Fact]
	public void DynamoDbCdc_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbCdc(opts =>
		{
			opts.MaxBatchSize = 0; // Violates [Range(1, 1000)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbCdcOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	#endregion
}
