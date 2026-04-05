// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.CosmosDb;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.DynamoDb;
using Excalibur.Inbox.ElasticSearch;
using Excalibur.Inbox.Firestore;
using Excalibur.Inbox.InMemory;
using Excalibur.Inbox.MongoDB;
using Excalibur.Inbox.Postgres;
using Excalibur.Inbox.Redis;
using Excalibur.Inbox.SqlServer;

namespace Excalibur.Data.Tests.Builders;

/// <summary>
/// Sprint 637 B.3/B.8: Tests for IInboxBuilder Use*() provider extension methods
/// for all 9 inbox providers (CosmosDb, DynamoDb, ElasticSearch, Firestore,
/// InMemory, MongoDB, Postgres, Redis, SqlServer).
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class InboxBuilderProviderExtensionsShould : UnitTestBase
{
	private sealed class TestInboxBuilder : IInboxBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	#region UseCosmosDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCosmosDb()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseCosmosDb(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseCosmosDb()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCosmosDb((Action<CosmosDbInboxOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCosmosDb()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseCosmosDb(_ => { });
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseDynamoDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseDynamoDb()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseDynamoDb(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseDynamoDb()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseDynamoDb((Action<DynamoDbInboxOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseDynamoDb()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseDynamoDb(_ => { });
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseElasticSearch

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseElasticSearch()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseElasticSearch(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseElasticSearch()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseElasticSearch((Action<ElasticsearchInboxOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseElasticSearch()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseElasticSearch(_ => { });
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseFirestore

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseFirestore()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseFirestore(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseFirestore()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseFirestore((Action<FirestoreInboxOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseFirestore()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseFirestore(_ => { });
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseInMemory

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseInMemory()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseInMemory());
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseInMemory()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseInMemory();
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseMongoDB

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseMongoDB()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseMongoDB(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseMongoDB()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseMongoDB((Action<MongoDbInboxOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseMongoDB()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseMongoDB(_ => { });
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UsePostgres

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUsePostgres()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UsePostgres(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUsePostgres()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgres((Action<PostgresInboxOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UsePostgres()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UsePostgres(_ => { });
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseRedis

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseRedis()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseRedis(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseRedis()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseRedis((Action<RedisInboxOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseRedis()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseRedis(_ => { });
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseSqlServer

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseSqlServer()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseSqlServer(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseSqlServer()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer((Action<SqlServerInboxOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseSqlServer()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseSqlServer(_ => { });
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
