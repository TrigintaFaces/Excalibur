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

using Microsoft.Extensions.DependencyInjection;

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
			((IInboxBuilder)null!).UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==")));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseCosmosDb()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCosmosDb((Action<ICosmosDbInboxBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCosmosDb()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseCosmosDb(cosmos => cosmos.ConnectionString("AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA=="));
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseDynamoDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseDynamoDb()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseDynamoDb()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseDynamoDb((Action<IDynamoDBInboxBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseDynamoDb()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000"));
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
			builder.UseElasticSearch((Action<IElasticSearchInboxBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseElasticSearch()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseElasticSearch(es => es.NodeUri(new Uri("http://localhost:9200")));
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseFirestore

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseFirestore()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseFirestore(fs => fs.ProjectId("test-project")));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseFirestore()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseFirestore((Action<IFirestoreInboxBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseFirestore()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseFirestore(fs => fs.ProjectId("test-project"));
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
			((IInboxBuilder)null!).UseMongoDB(mongo => mongo.ConnectionString("mongodb://localhost:27017")));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseMongoDB()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseMongoDB((Action<IMongoDBInboxBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseMongoDB()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseMongoDB(mongo => mongo.ConnectionString("mongodb://localhost:27017"));
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
			builder.UsePostgres((Action<IPostgresInboxBuilder>)null!));
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
			((IInboxBuilder)null!).UseRedis(redis => redis.ConnectionString("localhost:6379")));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseRedis()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseRedis((Action<IRedisInboxBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseRedis()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseRedis(redis => redis.ConnectionString("localhost:6379"));
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseSqlServer

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseSqlServer()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseSqlServer((Action<ISqlServerInboxBuilder>)(_ => { })));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseSqlServer()
	{
		var builder = new TestInboxBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer((Action<ISqlServerInboxBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseSqlServer()
	{
		var builder = new TestInboxBuilder();
		var result = builder.UseSqlServer(sql => sql.ConnectionString("Server=localhost;Database=Test;"));
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
