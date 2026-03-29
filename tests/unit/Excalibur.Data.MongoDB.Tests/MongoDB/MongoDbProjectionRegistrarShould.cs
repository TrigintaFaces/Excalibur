// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Projections;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.MongoDB.Tests.MongoDB;

/// <summary>
/// Unit tests for <see cref="MongoDbProjectionRegistrar"/> and <c>AddMongoDbProjections</c> batch DI extensions.
/// Validates both string-based and Action-based overloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "MongoDB")]
public sealed class MongoDbProjectionRegistrarShould : UnitTestBase
{
	private const string SharedConnStr = "mongodb://localhost:27017";
	private const string SharedDbName = "testdb";

	#region String-based overload

	[Fact]
	public void RegisterMultipleProjections_WithStringOverload()
	{
		var services = new ServiceCollection();

		services.AddMongoDbProjections(SharedConnStr, SharedDbName, p =>
			p.Add<ProjectionA>().Add<ProjectionB>());

		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionA>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionB>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void ApplySharedConfig_WithStringOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddMongoDbProjections(SharedConnStr, SharedDbName, p => p.Add<ProjectionA>());

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbProjectionStoreOptions>>();
		options.Value.ConnectionString.ShouldBe(SharedConnStr);
		options.Value.DatabaseName.ShouldBe(SharedDbName);
	}

	[Fact]
	public void ApplyPerProjectionOverride_WithStringOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddMongoDbProjections(SharedConnStr, SharedDbName, p =>
			p.Add<ProjectionA>(opts => opts.CollectionName = "custom_collection"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbProjectionStoreOptions>>();
		options.Value.CollectionName.ShouldBe("custom_collection");
	}

	#endregion

	#region Action<Options> overload

	[Fact]
	public void RegisterMultipleProjections_WithActionOverload()
	{
		var services = new ServiceCollection();

		services.AddMongoDbProjections(
			opts => { opts.ConnectionString = SharedConnStr; opts.DatabaseName = SharedDbName; },
			p => p.Add<ProjectionA>().Add<ProjectionB>());

		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionA>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionB>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void PerProjectionOverrideWinsOverShared_WithActionOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddMongoDbProjections(
			opts => { opts.ConnectionString = SharedConnStr; opts.DatabaseName = SharedDbName; opts.CollectionName = "shared"; },
			p => p.Add<ProjectionA>(opts => opts.CollectionName = "override"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MongoDbProjectionStoreOptions>>();
		options.Value.CollectionName.ShouldBe("override");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddMongoDbProjections(SharedConnStr, SharedDbName, p =>
		{
			p.Add<ProjectionA>().ShouldNotBeNull();
		});
		result.ShouldBeSameAs(services);
	}

	#endregion

	private sealed class ProjectionA { public string? Name { get; set; } }
	private sealed class ProjectionB { public int Count { get; set; } }
}
