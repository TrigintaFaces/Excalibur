// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.MongoDB;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Sprint 637 B.7: Tests for ILeaderElectionBuilder.UseMongoDB() extension methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class MongoDbLeaderElectionBuilderExtensionsShould
{
	private sealed class TestLeaderElectionBuilder : ILeaderElectionBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	#region UseMongoDB(resourceName, Action<MongoDbLeaderElectionOptions>)

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ILeaderElectionBuilder)null!).UseMongoDB("resource", _ => { }));
	}

	[Fact]
	public void ThrowArgumentException_WhenResourceNameIsNull()
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseMongoDB(null!, _ => { }));
	}

	[Fact]
	public void ThrowArgumentException_WhenResourceNameIsEmpty()
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseMongoDB(string.Empty, _ => { }));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseMongoDB("resource", (Action<MongoDbLeaderElectionOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		var builder = new TestLeaderElectionBuilder();

		// Register IMongoClient dependency (required by the extension)
		builder.Services.AddSingleton(FakeItEasy.A.Fake<global::MongoDB.Driver.IMongoClient>());

		var result = builder.UseMongoDB("resource", opts =>
		{
			opts.ConnectionString = "mongodb://localhost:27017";
		});

		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseMongoDB(resourceName, connectionString)

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConnectionStringOverload()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ILeaderElectionBuilder)null!).UseMongoDB("resource", "mongodb://localhost:27017"));
	}

	[Fact]
	public void ThrowArgumentException_WhenResourceNameIsNull_ForConnectionStringOverload()
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseMongoDB(null!, "mongodb://localhost:27017"));
	}

	[Fact]
	public void ThrowArgumentException_WhenConnectionStringIsEmpty()
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseMongoDB("resource", string.Empty));
	}

	[Fact]
	public void ReturnSameBuilder_ForConnectionStringOverload()
	{
		var builder = new TestLeaderElectionBuilder();

		// Register IMongoClient dependency
		builder.Services.AddSingleton(FakeItEasy.A.Fake<global::MongoDB.Driver.IMongoClient>());

		var result = builder.UseMongoDB("resource", "mongodb://localhost:27017");

		result.ShouldBeSameAs(builder);
	}

	#endregion
}
