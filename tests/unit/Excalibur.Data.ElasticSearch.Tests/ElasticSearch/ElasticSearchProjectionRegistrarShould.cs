// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch;

/// <summary>
/// Unit tests for <see cref="ElasticSearchProjectionRegistrar"/> and <c>AddElasticSearchProjections</c> batch DI extensions.
/// Validates both string-based and Action-based overloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ElasticSearch")]
public sealed class ElasticSearchProjectionRegistrarShould : UnitTestBase
{
	private const string SharedNodeUri = "http://localhost:9200";

	#region String-based overload

	[Fact]
	public void RegisterMultipleProjections_WithStringOverload()
	{
		var services = new ServiceCollection();

		services.AddElasticSearchProjections(SharedNodeUri, p =>
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

		services.AddElasticSearchProjections(SharedNodeUri, p => p.Add<ProjectionA>());

		// ES uses named options keyed by projection type name
		using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		var options = monitor.Get(nameof(ProjectionA));
		options.NodeUri.ShouldBe(SharedNodeUri);
	}

	[Fact]
	public void ApplyPerProjectionOverride_WithStringOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddElasticSearchProjections(SharedNodeUri, p =>
			p.Add<ProjectionA>(opts => opts.IndexPrefix = "custom-prefix"));

		using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		var options = monitor.Get(nameof(ProjectionA));
		options.IndexPrefix.ShouldBe("custom-prefix");
	}

	#endregion

	#region Action<Options> overload

	[Fact]
	public void RegisterMultipleProjections_WithActionOverload()
	{
		var services = new ServiceCollection();

		services.AddElasticSearchProjections(
			opts => opts.NodeUri = SharedNodeUri,
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

		services.AddElasticSearchProjections(
			opts => { opts.NodeUri = SharedNodeUri; opts.IndexPrefix = "shared"; },
			p => p.Add<ProjectionA>(opts => opts.IndexPrefix = "override"));

		using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		var options = monitor.Get(nameof(ProjectionA));
		options.IndexPrefix.ShouldBe("override");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddElasticSearchProjections(SharedNodeUri, p =>
		{
			p.Add<ProjectionA>().ShouldNotBeNull();
		});
		result.ShouldBeSameAs(services);
	}

	#endregion

	private sealed class ProjectionA { public string? Name { get; set; } }
	private sealed class ProjectionB { public int Count { get; set; } }
}
