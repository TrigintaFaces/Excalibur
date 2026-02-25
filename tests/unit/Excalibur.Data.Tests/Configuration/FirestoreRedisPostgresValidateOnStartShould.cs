// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;
using Excalibur.Data.Firestore.Cdc;
using Excalibur.Data.Postgres.Cdc;
using Excalibur.Data.Postgres.Inbox;
using Excalibur.Data.Postgres.Persistence;
using Excalibur.Data.Redis.Inbox;
using Excalibur.Data.Redis.Outbox;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies that Firestore, Redis, and Postgres provider DI registrations wire up
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> correctly.
/// Sprint 564 S564.48: Firestore + Redis + Postgres + InMemory ValidateOnStart verification.
/// InMemory already covered in <see cref="ValidateOnStartRegistrationShould"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
			opts.TimeoutInSeconds = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<FirestoreOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
	}

	[Fact]
	public void FirestoreCdc_RegistersOptionsValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddFirestoreCdc(opts => { });

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<FirestoreCdcOptions>>();
		validators.ShouldNotBeEmpty("AddFirestoreCdc should register IValidateOptions<FirestoreCdcOptions>");
	}

	[Fact]
	public void FirestoreCdc_InvalidOptions_ThrowsOnResolve()
	{
		var services = new ServiceCollection();
		_ = services.AddFirestoreCdc(opts =>
		{
			opts.MaxBatchSize = 0; // Violates [Range(1, int.MaxValue)]
		});

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<FirestoreCdcOptions>>();
		_ = Should.Throw<OptionsValidationException>(() => _ = options.Value);
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
			opts.ConnectTimeoutMs = 0; // Violates [Range(1, int.MaxValue)]
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
			opts.ConnectTimeoutMs = 0; // Violates [Range(1, int.MaxValue)]
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
			opts.ConnectionTimeout = 0; // Violates [Range(1, 300)]
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
			opts.CommandTimeoutSeconds = 0; // Violates [Range(1, int.MaxValue)]
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
