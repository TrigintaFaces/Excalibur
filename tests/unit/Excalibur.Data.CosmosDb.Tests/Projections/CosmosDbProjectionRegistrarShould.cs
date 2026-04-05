// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Projections;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.CosmosDb.Tests.Projections;

/// <summary>
/// Unit tests for <see cref="CosmosDbProjectionRegistrar"/> and <c>AddCosmosDbProjections</c> batch DI extensions.
/// Validates both string-based and Action-based overloads.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
public sealed class CosmosDbProjectionRegistrarShould : UnitTestBase
{
	private const string SharedConnStr = "AccountEndpoint=https://localhost:8081;AccountKey=test==;";
	private const string SharedDbName = "testdb";

	#region String-based overload

	[Fact]
	public void RegisterMultipleProjections_WithStringOverload()
	{
		var services = new ServiceCollection();

		services.AddCosmosDbProjections(SharedConnStr, SharedDbName, p =>
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

		services.AddCosmosDbProjections(SharedConnStr, SharedDbName, p => p.Add<ProjectionA>());

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbProjectionStoreOptions>>();
		options.Value.Client.ConnectionString.ShouldBe(SharedConnStr);
		options.Value.DatabaseName.ShouldBe(SharedDbName);
	}

	[Fact]
	public void ApplyPerProjectionOverride_WithStringOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddCosmosDbProjections(SharedConnStr, SharedDbName, p =>
			p.Add<ProjectionA>(opts => opts.ContainerName = "custom_container"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbProjectionStoreOptions>>();
		options.Value.ContainerName.ShouldBe("custom_container");
	}

	#endregion

	#region Action<Options> overload

	[Fact]
	public void RegisterMultipleProjections_WithActionOverload()
	{
		var services = new ServiceCollection();

		services.AddCosmosDbProjections(
			opts => { opts.Client.ConnectionString = SharedConnStr; opts.DatabaseName = SharedDbName; },
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

		services.AddCosmosDbProjections(
			opts => { opts.Client.ConnectionString = SharedConnStr; opts.DatabaseName = SharedDbName; opts.ContainerName = "shared"; },
			p => p.Add<ProjectionA>(opts => opts.ContainerName = "override"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CosmosDbProjectionStoreOptions>>();
		options.Value.ContainerName.ShouldBe("override");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddCosmosDbProjections(SharedConnStr, SharedDbName, p =>
		{
			p.Add<ProjectionA>().ShouldNotBeNull();
		});
		result.ShouldBeSameAs(services);
	}

	#endregion

	private sealed class ProjectionA { public string? Name { get; set; } }
	private sealed class ProjectionB { public int Count { get; set; } }
}
