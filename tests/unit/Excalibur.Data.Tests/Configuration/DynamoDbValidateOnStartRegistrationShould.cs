// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Snapshots;
using Excalibur.Inbox.DynamoDb;
using Excalibur.Outbox.DynamoDb;
using Excalibur.Saga.DynamoDb;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies that DynamoDb provider DI registrations wire up
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> correctly.
/// Sprint 564 S564.47: DynamoDb ValidateOnStart verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DynamoDbValidateOnStartRegistrationShould
{
	#region DynamoDb Core

	[Fact]
	public void DynamoDb_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburDynamoDb(db => db.ServiceUrl("http://localhost:8000"));

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbOptions>>();
		validators.ShouldNotBeEmpty("AddExcaliburDynamoDb should register IValidateOptions<DynamoDbOptions>");
	}

	[Fact]
	public void DynamoDb_InvalidOptions_ThrowsAtBuilderTime()
	{
		// The builder validates connection values eagerly via ArgumentException.ThrowIfNullOrWhiteSpace
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() =>
			services.AddExcaliburDynamoDb(db => db.ServiceUrl("")));
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
		_ = services.AddExcaliburOutbox(outbox =>
			outbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbOutboxOptions>>();
		validators.ShouldNotBeEmpty("UseDynamoDb should register IValidateOptions<DynamoDbOutboxOptions>");
	}

	[Fact]
	public void DynamoDbOutbox_InvalidOptions_ThrowsAtBuilderTime()
	{
		// The builder validates connection values eagerly via ArgumentException.ThrowIfNullOrWhiteSpace
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
				outbox.UseDynamoDb(db => db.ServiceUrl(""))));
	}

	#endregion

	#region DynamoDb Inbox

	[Fact]
	public void DynamoDbInbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(inbox =>
			inbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbInboxOptions>>();
		validators.ShouldNotBeEmpty("UseDynamoDb should register IValidateOptions<DynamoDbInboxOptions>");
	}

	[Fact]
	public void DynamoDbInbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(inbox =>
			inbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));

		// Override with invalid options after builder registration
		_ = services.Configure<DynamoDbInboxOptions>(opts =>
		{
			opts.TableName = string.Empty; // Violates [Required]
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
		_ = services.AddExcaliburSaga(saga =>
			saga.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<DynamoDbSagaOptions>>();
		validators.ShouldNotBeEmpty("UseDynamoDb should register IValidateOptions<DynamoDbSagaOptions>");
	}

	[Fact]
	public void DynamoDbSaga_InvalidOptions_ThrowsAtBuilderTime()
	{
		// The builder validates connection values eagerly via ArgumentException.ThrowIfNullOrWhiteSpace
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() =>
			services.AddExcaliburSaga(saga =>
				saga.UseDynamoDb(db => db.ServiceUrl(""))));
	}

	#endregion

	#region DynamoDb CDC

	// A CDC source must name the table or the stream it follows: with neither, the processor has nothing to
	// read and no later call supplies one. AddDynamoDbCdc registers an IValidateOptions with ValidateOnStart,
	// so the refusal lands at startup rather than at the first poll.
	[Fact]
	public void DynamoDbCdc_ValidOptionsResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbCdc(opts =>
		{
			opts.TableName = "orders";
			opts.MaxBatchSize = 50;
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbCdcOptions>>().Value;
		options.MaxBatchSize.ShouldBe(50);
	}

	[Fact]
	public void DynamoDbCdc_WithoutTableOrStream_IsRefused()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbCdc(opts => opts.MaxBatchSize = 50);

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<DynamoDbCdcOptions>>().Value);

		ex.Message.ShouldContain("TableName");
	}

	// The companion refusal. A batch size of zero reads nothing per poll, which is a processor that runs
	// forever and delivers no record -- indistinguishable, from outside, from a stream with no traffic.
	[Fact]
	public void DynamoDbCdc_OutOfRangeBatchSize_IsRefused()
	{
		var services = new ServiceCollection();
		_ = services.AddDynamoDbCdc(opts =>
		{
			opts.TableName = "orders";
			opts.MaxBatchSize = 0;
		});

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<DynamoDbCdcOptions>>().Value);

		ex.Message.ShouldContain(nameof(DynamoDbCdcOptions.MaxBatchSize));
	}

	#endregion
}