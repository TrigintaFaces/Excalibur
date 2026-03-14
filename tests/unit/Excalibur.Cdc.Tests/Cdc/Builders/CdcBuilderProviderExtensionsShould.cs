// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.CosmosDb;
using Excalibur.Cdc.DynamoDb;
using Excalibur.Cdc.Firestore;
using Excalibur.Cdc.MongoDB;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Tests.Cdc.Builders;

/// <summary>
/// Sprint 637 B.4: Tests for ICdcBuilder Use*() provider extension methods
/// (UseCosmosDb, UseDynamoDb, UseMongoDB, UseFirestore).
/// Follows the SagaBuilderProviderExtensionsShould pattern.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcBuilderProviderExtensionsShould : UnitTestBase
{
	private sealed class TestCdcBuilder : ICdcBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
		public ICdcBuilder TrackTable(string tableName, Action<ICdcTableBuilder> configure) => this;
		public ICdcBuilder TrackTable<TEntity>(Action<ICdcTableBuilder>? configure = null) where TEntity : class => this;
		public ICdcBuilder WithRecovery(Action<ICdcRecoveryBuilder> configure) => this;
		public ICdcBuilder EnableBackgroundProcessing(bool enable = true) => this;
	}

	#region UseCosmosDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCosmosDb()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ICdcBuilder)null!).UseCosmosDb(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseCosmosDb()
	{
		var builder = new TestCdcBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseCosmosDb((Action<CosmosDbCdcOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseCosmosDb()
	{
		var builder = new TestCdcBuilder();
		var result = builder.UseCosmosDb(_ => { });
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseCosmosDbWithStateStore()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ICdcBuilder)null!).UseCosmosDb(_ => { }, _ => { }));
	}

	#endregion

	#region UseDynamoDb

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseDynamoDb()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ICdcBuilder)null!).UseDynamoDb(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseDynamoDb()
	{
		var builder = new TestCdcBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseDynamoDb((Action<DynamoDbCdcOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseDynamoDb()
	{
		var builder = new TestCdcBuilder();
		var result = builder.UseDynamoDb(_ => { });
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseDynamoDbWithStateStore()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ICdcBuilder)null!).UseDynamoDb(_ => { }, _ => { }));
	}

	#endregion

	#region UseMongoDB

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseMongoDB()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ICdcBuilder)null!).UseMongoDB(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseMongoDB()
	{
		var builder = new TestCdcBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseMongoDB((Action<MongoDbCdcOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseMongoDB()
	{
		var builder = new TestCdcBuilder();
		var result = builder.UseMongoDB(_ => { });
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseMongoDBWithStateStore()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ICdcBuilder)null!).UseMongoDB(_ => { }, _ => { }));
	}

	#endregion

	#region UseFirestore

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseFirestore()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ICdcBuilder)null!).UseFirestore(_ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull_ForUseFirestore()
	{
		var builder = new TestCdcBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseFirestore((Action<FirestoreCdcOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_UseFirestore()
	{
		var builder = new TestCdcBuilder();
		var result = builder.UseFirestore(_ => { });
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForUseFirestoreWithStateStore()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ICdcBuilder)null!).UseFirestore(_ => { }, _ => { }));
	}

	#endregion
}
