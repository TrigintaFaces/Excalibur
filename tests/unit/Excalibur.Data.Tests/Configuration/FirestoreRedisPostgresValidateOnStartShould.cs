// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;
using Excalibur.Cdc.Firestore;
using Excalibur.Cdc.Postgres;
using Excalibur.Inbox.Postgres;
using Excalibur.Data.Postgres.Persistence;
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
		_ = services.AddFirestore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<FirestoreOptions>>();
		validators.ShouldNotBeEmpty("AddFirestore should register IValidateOptions<FirestoreOptions>");
	}

	[Fact]
	public void Firestore_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddFirestore(opts =>
		{
			opts.TimeoutInSeconds = 0; // Violates range check in validator
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<FirestoreOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void FirestoreCdc_RegistersOptions()
	{
		// FirestoreCdc has a validator class but it is not registered in DI.
		// Verify options are resolvable instead.
		var services = new ServiceCollection();
		_ = services.AddFirestoreCdc(opts => { });

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
		_ = services.AddRedisOutboxStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<RedisOutboxOptions>>();
		validators.ShouldNotBeEmpty("AddRedisOutboxStore should register IValidateOptions<RedisOutboxOptions>");
	}

	[Fact]
	public void RedisOutbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddRedisOutboxStore(opts =>
		{
			opts.ConnectionString = ""; // Violates required string check in Validate()
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<RedisOutboxOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void RedisInbox_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddRedisInboxStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<RedisInboxOptions>>();
		validators.ShouldNotBeEmpty("AddRedisInboxStore should register IValidateOptions<RedisInboxOptions>");
	}

	[Fact]
	public void RedisInbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddRedisInboxStore(opts =>
		{
			opts.ConnectionString = ""; // Violates required string check in Validate()
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<RedisInboxOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
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
		_ = services.AddPostgresInboxStore(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<PostgresInboxOptions>>();
		validators.ShouldNotBeEmpty("AddPostgresInboxStore should register IValidateOptions<PostgresInboxOptions>");
	}

	[Fact]
	public void PostgresInbox_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddPostgresInboxStore(opts =>
		{
			opts.CommandTimeoutSeconds = 0; // Violates range check in validator
		});

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
