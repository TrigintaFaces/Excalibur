// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using Excalibur.Data.CosmosDb;
using Excalibur.Data.DynamoDb;
using Excalibur.Data.Firestore;
using Excalibur.Data.MongoDB;
using Excalibur.Data.MongoDB.Snapshots;
using Excalibur.Data.Postgres.Persistence;
using Excalibur.Data.Redis.Inbox;
using Excalibur.Data.Redis.Outbox;

namespace Excalibur.Data.Tests.Configuration;

/// <summary>
/// Verifies DataAnnotation validation on provider Options classes (MongoDB, DynamoDb, CosmosDb, Postgres, Redis, Firestore).
/// Sprint 564 S564.58-59: Provider Options DataAnnotation coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProviderDataAnnotationsShould
{
	private static bool TryValidate(object instance, out ICollection<ValidationResult> results)
	{
		results = new List<ValidationResult>();
		return Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
	}

	#region MongoDB

	[Fact]
	public void MongoDbProvider_Succeed_WhenRequiredFieldsProvided()
	{
		// MongoDbProviderOptions has [Required] on ConnectionString and DatabaseName,
		// so defaults (empty string) fail validation. Provide valid values.
		var options = new MongoDbProviderOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test-db",
		};
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void MongoDbProvider_Fail_WhenConnectionStringIsEmpty()
	{
		var options = new MongoDbProviderOptions { DatabaseName = "test-db" };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(MongoDbProviderOptions.ConnectionString)));
	}

	[Fact]
	public void MongoDbProvider_Fail_WhenServerSelectionTimeoutIsZero()
	{
		var options = new MongoDbProviderOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test-db",
			ServerSelectionTimeout = 0,
		};
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(MongoDbProviderOptions.ServerSelectionTimeout)));
	}

	[Fact]
	public void MongoDbProvider_Fail_WhenMaxPoolSizeIsZero()
	{
		var options = new MongoDbProviderOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test-db",
			MaxPoolSize = 0,
		};
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(MongoDbProviderOptions.MaxPoolSize)));
	}

	[Fact]
	public void MongoDbProvider_Fail_WhenMinPoolSizeIsNegative()
	{
		var options = new MongoDbProviderOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "test-db",
			MinPoolSize = -1,
		};
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(MongoDbProviderOptions.MinPoolSize)));
	}

	[Fact]
	public void MongoDbSnapshotStore_Succeed_WithDefaults()
	{
		var options = new MongoDbSnapshotStoreOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void MongoDbSnapshotStore_Fail_WhenServerSelectionTimeoutSecondsIsZero()
	{
		var options = new MongoDbSnapshotStoreOptions { ServerSelectionTimeoutSeconds = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(MongoDbSnapshotStoreOptions.ServerSelectionTimeoutSeconds)));
	}

	#endregion

	#region DynamoDb

	[Fact]
	public void DynamoDb_Succeed_WithDefaults()
	{
		var options = new DynamoDbOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void DynamoDb_Fail_WhenMaxRetryAttemptsIsZero()
	{
		var options = new DynamoDbOptions { MaxRetryAttempts = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(DynamoDbOptions.MaxRetryAttempts)));
	}

	#endregion

	#region CosmosDb

	[Fact]
	public void CosmosDb_Succeed_WithDefaults()
	{
		var options = new CosmosDbOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void CosmosDb_Succeed_WhenMaxRetryAttemptsIsZero()
	{
		// CosmosDb MaxRetryAttempts has [Range(0, int.MaxValue)] — 0 is valid (no retries)
		var options = new CosmosDbOptions { MaxRetryAttempts = 0 };
		TryValidate(options, out var results).ShouldBeTrue();
	}

	[Fact]
	public void CosmosDb_Fail_WhenMaxRetryAttemptsIsNegative()
	{
		var options = new CosmosDbOptions { MaxRetryAttempts = -1 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(CosmosDbOptions.MaxRetryAttempts)));
	}

	#endregion

	#region Firestore

	[Fact]
	public void Firestore_Succeed_WithDefaults()
	{
		var options = new FirestoreOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void Firestore_Fail_WhenTimeoutInSecondsIsZero()
	{
		var options = new FirestoreOptions { TimeoutInSeconds = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(FirestoreOptions.TimeoutInSeconds)));
	}

	#endregion

	#region Redis

	[Fact]
	public void RedisOutbox_Succeed_WithDefaults()
	{
		var options = new RedisOutboxOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void RedisOutbox_Fail_WhenConnectTimeoutMsIsZero()
	{
		var options = new RedisOutboxOptions { ConnectTimeoutMs = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(RedisOutboxOptions.ConnectTimeoutMs)));
	}

	[Fact]
	public void RedisInbox_Succeed_WithDefaults()
	{
		var options = new RedisInboxOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void RedisInbox_Fail_WhenConnectTimeoutMsIsZero()
	{
		var options = new RedisInboxOptions { ConnectTimeoutMs = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(RedisInboxOptions.ConnectTimeoutMs)));
	}

	#endregion

	#region Postgres

	[Fact]
	public void PostgresPersistence_Succeed_WhenRequiredFieldsProvided()
	{
		// PostgresPersistenceOptions has [Required] on ConnectionString,
		// so defaults (empty string) fail validation. Provide valid value.
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=test",
		};
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void PostgresPersistence_Fail_WhenConnectionStringIsEmpty()
	{
		var options = new PostgresPersistenceOptions();
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(PostgresPersistenceOptions.ConnectionString)));
	}

	[Fact]
	public void PostgresPersistence_Fail_WhenConnectionTimeoutIsZero()
	{
		var options = new PostgresPersistenceOptions
		{
			ConnectionString = "Host=localhost;Database=test",
			ConnectionTimeout = 0,
		};
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(PostgresPersistenceOptions.ConnectionTimeout)));
	}

	#endregion
}
