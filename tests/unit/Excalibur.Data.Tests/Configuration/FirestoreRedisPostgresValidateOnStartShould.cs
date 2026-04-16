// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;
using Excalibur.Cdc.Firestore;
using Excalibur.Cdc.Postgres;
using Excalibur.Inbox.Postgres;
using Excalibur.Data.Postgres.Persistence;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Redis;
using Excalibur.Outbox.Redis;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies that Firestore, Redis, and Postgres provider DI registrations wire up
/// <c>ValidateOnStart()</c> correctly.
/// Sprint 564 S564.48: Firestore + Redis + Postgres + InMemory ValidateOnStart verification.
/// Sprint 750: ValidateDataAnnotations removed (AOT-safe migration).
/// Packages with explicit IValidateOptions validators registered in DI still validate;
/// packages without (FirestoreCdc) verify IOptions resolvability instead.
/// InMemory already covered in <see cref="ValidateOnStartRegistrationShould"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class FirestoreRedisPostgresValidateOnStartShould
{
	#region Firestore

	[Fact]
	public void Firestore_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburFirestore(fs => fs.ProjectId("test-project"));

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<FirestoreOptions>>();
		validators.ShouldNotBeEmpty("AddExcaliburFirestore should register IValidateOptions<FirestoreOptions>");
	}

	[Fact]
	public void Firestore_InvalidOptions_ThrowsAtBuilderTime()
	{
		// The builder validates connection values eagerly via ArgumentException.ThrowIfNullOrWhiteSpace
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() =>
			services.AddExcaliburFirestore(fs => fs.ProjectId("")));
	}

	[Fact]
	public void FirestoreCdc_RegistersOptions()
	{
		// FirestoreCdc has a validator class but it is not registered in DI.
		// Verify options are resolvable instead.
		var services = new ServiceCollection();
		_ = services.AddFirestoreCdc(opts =>
		{
			opts.CollectionPath = "test-collection";
			opts.ProcessorName = "test-processor";
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<FirestoreCdcOptions>>();
		options.ShouldNotBeNull("AddFirestoreCdc should register IOptions<FirestoreCdcOptions>");
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void FirestoreCdc_ConfiguresOptionsCorrectly()
	{
		var services = new ServiceCollection();
		_ = services.AddFirestoreCdc(opts =>
		{
			opts.CollectionPath = "test-collection";
			opts.ProcessorName = "test-processor";
			opts.MaxBatchSize = 50;
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<FirestoreCdcOptions>>();
		options.Value.MaxBatchSize.ShouldBe(50);
	}

	#endregion

	#region Redis

	[Fact]
	public void RedisOutbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburOutbox(outbox =>
			outbox.UseRedis(redis => redis.ConnectionString("localhost:6379")));

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<RedisOutboxOptions>>();
		validators.ShouldNotBeEmpty("UseRedis should register IValidateOptions<RedisOutboxOptions>");
	}

	[Fact]
	public void RedisOutbox_NoConnection_ThrowsAtBuilderTime()
	{
		// The builder now validates connection strings eagerly via ArgumentException.ThrowIfNullOrWhiteSpace
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
				outbox.UseRedis(redis => redis.ConnectionString(""))));
	}

	[Fact]
	public void RedisInbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(inbox =>
			inbox.UseRedis(redis => redis.ConnectionString("localhost:6379")));

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<RedisInboxOptions>>();
		validators.ShouldNotBeEmpty("UseRedis should register IValidateOptions<RedisInboxOptions>");
	}

	[Fact]
	public void RedisInbox_InvalidOptions_ThrowsAtBuilderTime()
	{
		// The builder now validates connection strings eagerly via ArgumentException.ThrowIfNullOrWhiteSpace
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() =>
			services.AddExcaliburInbox(inbox =>
				inbox.UseRedis(redis => redis.ConnectionString(""))));
	}

	#endregion

	#region Postgres

	[Fact]
	public void PostgresPersistence_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddPostgresPersistence("Host=localhost;Database=test");

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<PostgresPersistenceOptions>>();
		validators.ShouldNotBeEmpty("AddPostgresPersistence should register IValidateOptions<PostgresPersistenceOptions>");
	}

	[Fact]
	public void PostgresPersistence_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddPostgresPersistence("Host=localhost;Database=test", opts =>
		{
			opts.ConnectionTimeout = 0; // Violates range check in validator
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PostgresPersistenceOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void PostgresInbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(inbox =>
			inbox.UsePostgres(pg => pg.ConnectionString("Host=localhost;Database=test;")));

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<PostgresInboxOptions>>();
		validators.ShouldNotBeEmpty("UsePostgres should register IValidateOptions<PostgresInboxOptions>");
	}

	[Fact]
	public void PostgresInbox_NoConnection_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddExcaliburInbox(inbox =>
			inbox.UsePostgres(_ => { })); // No connection configured

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<PostgresInboxOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void PostgresCdc_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddPostgresCdc(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<PostgresCdcOptions>>();
		validators.ShouldNotBeEmpty("AddPostgresCdc should register IValidateOptions<PostgresCdcOptions>");
	}

	#endregion
}
