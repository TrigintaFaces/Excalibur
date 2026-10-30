// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Postgres;

/// <summary>
/// Unit tests for <see cref="PostgresProjectionRegistrar"/> and <c>AddPostgresProjections</c> batch DI extensions.
/// Validates both string-based and Action-based overloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
[Trait("Database", "Postgres")]
public sealed class PostgresProjectionRegistrarShould : UnitTestBase
{
	private const string SharedConnectionString = "Host=localhost;Database=TestDb;";

    [Fact]
    public void KeepPerModelOptionsIndependentInTheSameProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgresProjections(SharedConnectionString, projections =>
        {
            projections.Add<ProjectionA>(options => options.TableName = "first_model");
            projections.Add<ProjectionB>(options => options.TableName = "second_model");
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IProjectionStore<ProjectionA>>();
        var second = scope.ServiceProvider.GetRequiredService<IProjectionStore<ProjectionB>>();
        var firstIdentity = (IProjectionDestinationIdentity)first.GetService(typeof(IProjectionDestinationIdentity))!;
        var secondIdentity = (IProjectionDestinationIdentity)second.GetService(typeof(IProjectionDestinationIdentity))!;
        firstIdentity.Identity.ShouldContain("first_model");
        secondIdentity.Identity.ShouldContain("second_model");
        firstIdentity.Identity.ShouldNotBe(secondIdentity.Identity);
    }

	#region String-based overload

	[Fact]
	public void RegisterMultipleProjections_WithStringOverload()
	{
		var services = new ServiceCollection();

		services.AddPostgresProjections(SharedConnectionString, projections =>
		{
			projections.Add<ProjectionA>().Add<ProjectionB>();
		});

		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionA>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
		services.Any(sd => sd.ServiceType == typeof(IProjectionStore<ProjectionB>)
			&& sd.Lifetime == ServiceLifetime.Scoped).ShouldBeTrue();
	}

	[Fact]
	public void ApplySharedConnectionString_WithStringOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddPostgresProjections(SharedConnectionString, p => p.Add<ProjectionA>());

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<PostgresProjectionStoreOptions>>().Get(typeof(ProjectionA).AssemblyQualifiedName!);
		options.ConnectionString.ShouldBe(SharedConnectionString);
	}

	[Fact]
	public void ApplyPerProjectionOverride_WithStringOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddPostgresProjections(SharedConnectionString, p =>
			p.Add<ProjectionA>(opts => opts.TableName = "custom_table"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<PostgresProjectionStoreOptions>>().Get(typeof(ProjectionA).AssemblyQualifiedName!);
		options.TableName.ShouldBe("custom_table");
	}

	#endregion

	#region Action<Options> overload

	[Fact]
	public void RegisterMultipleProjections_WithActionOverload()
	{
		var services = new ServiceCollection();

		services.AddPostgresProjections(
			opts => opts.ConnectionString = SharedConnectionString,
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

		services.AddPostgresProjections(
			opts => { opts.ConnectionString = SharedConnectionString; opts.TableName = "shared"; },
			p => p.Add<ProjectionA>(opts => opts.TableName = "override"));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<PostgresProjectionStoreOptions>>().Get(typeof(ProjectionA).AssemblyQualifiedName!);
		options.TableName.ShouldBe("override");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddPostgresProjections(SharedConnectionString, p =>
		{
			var registrar = p.Add<ProjectionA>();
			registrar.ShouldNotBeNull();
		});
		result.ShouldBeSameAs(services);
	}

	#endregion

	private sealed class ProjectionA { public string? Name { get; set; } }
	private sealed class ProjectionB { public int Count { get; set; } }
}
