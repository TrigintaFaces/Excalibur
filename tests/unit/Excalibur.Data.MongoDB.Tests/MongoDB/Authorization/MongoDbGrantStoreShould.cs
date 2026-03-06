// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.MongoDB.Authorization;

using Microsoft.Extensions.Logging.Abstractions;

using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Authorization;

/// <summary>
/// Unit tests for <see cref="MongoDbGrantStore"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "A3")]
public sealed class MongoDbGrantStoreShould
{
	private static IOptions<MongoDbAuthorizationOptions> CreateValidOptions() =>
		Microsoft.Extensions.Options.Options.Create(new MongoDbAuthorizationOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "testdb",
			GrantsCollectionName = "grants"
		});

	private static MongoDbGrantStore CreateStore()
	{
		var client = A.Fake<IMongoClient>();
		var db = A.Fake<IMongoDatabase>();
		A.CallTo(() => client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(db);
		return new MongoDbGrantStore(client, CreateValidOptions(), NullLogger<MongoDbGrantStore>.Instance);
	}

	[Fact]
	public void ImplementIGrantStore()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IGrantStore>();
	}

	[Fact]
	public void ImplementIGrantQueryStore()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IGrantQueryStore>();
	}

	[Fact]
	public void NotImplementIActivityGroupGrantStore()
	{
		typeof(MongoDbGrantStore).IsAssignableTo(typeof(IActivityGroupGrantStore)).ShouldBeFalse();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		var store = CreateStore();
		store.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MongoDbGrantStore(
				(IMongoClient)null!,
				CreateValidOptions(),
				NullLogger<MongoDbGrantStore>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MongoDbGrantStore(null!, NullLogger<MongoDbGrantStore>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MongoDbGrantStore(
				A.Fake<IMongoClient>(),
				CreateValidOptions(),
				null!));
	}

	[Fact]
	public void ReturnSelf_WhenGetServiceRequestsIGrantQueryStore()
	{
		var store = CreateStore();

		var result = store.GetService(typeof(IGrantQueryStore));

		result.ShouldNotBeNull();
		result.ShouldBeSameAs(store);
	}

	[Fact]
	public void ReturnNull_WhenGetServiceRequestsUnsupportedType()
	{
		var store = CreateStore();

		var result = store.GetService(typeof(IDisposable));

		result.ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenGetServiceTypeIsNull()
	{
		var store = CreateStore();

		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
	}

	[Fact]
	public void BeSealed()
	{
		typeof(MongoDbGrantStore).IsSealed.ShouldBeTrue();
	}
}
