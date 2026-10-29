// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CosmosDb.Projections;
using Excalibur.EventSourcing;

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
		var options = NamedOptions<ProjectionA>(provider);
		options.Client.ConnectionString.ShouldBe(SharedConnStr);
		options.DatabaseName.ShouldBe(SharedDbName);
	}

	[Fact]
	public void ApplyPerProjectionOverride_WithStringOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddCosmosDbProjections(SharedConnStr, SharedDbName, p =>
			p.Add<ProjectionA>(opts => opts.ContainerName = "custom_container"));

		using var provider = services.BuildServiceProvider();
		NamedOptions<ProjectionA>(provider).ContainerName.ShouldBe("custom_container");
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
		NamedOptions<ProjectionA>(provider).ContainerName.ShouldBe("override");
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

	[Fact]
	public void IsolateEachProjectionsContainerWhenRegisteredTogether()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddCosmosDbProjections(SharedConnStr, SharedDbName, p => p
			.Add<ProjectionA>(opts => opts.ContainerName = "a_container")
			.Add<ProjectionB>(opts => opts.ContainerName = "b_container"));

		using var provider = services.BuildServiceProvider();

		// Were these configured on the unnamed options instance, the last registration would decide the
		// container for both and both reads would return "b_container".
		NamedOptions<ProjectionA>(provider).ContainerName.ShouldBe("a_container");
		NamedOptions<ProjectionB>(provider).ContainerName.ShouldBe("b_container");

		// Both still carry the shared connection -- the isolation must not cost them the shared settings.
		NamedOptions<ProjectionA>(provider).DatabaseName.ShouldBe(SharedDbName);
		NamedOptions<ProjectionB>(provider).DatabaseName.ShouldBe(SharedDbName);
	}

	/// <summary>
	/// Reads the options instance the registration configured for <typeparamref name="TProjection"/>, via
	/// the same key the store uses. The unnamed <c>IOptions&lt;T&gt;</c> instance is never configured by a
	/// projection registration, so reading it would only ever report the option defaults.
	/// </summary>
	private static CosmosDbProjectionStoreOptions NamedOptions<TProjection>(IServiceProvider provider)
		where TProjection : class =>
		provider.GetRequiredService<IOptionsMonitor<CosmosDbProjectionStoreOptions>>()
			.Get(CosmosDbProjectionStore<TProjection>.OptionsName);

	private sealed class ProjectionA { public string? Name { get; set; } }
	private sealed class ProjectionB { public int Count { get; set; } }
}