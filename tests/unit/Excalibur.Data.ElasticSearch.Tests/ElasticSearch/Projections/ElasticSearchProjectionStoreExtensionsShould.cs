// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticSearchProjectionStoreExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNullForConfigureOverload()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>(
				(Action<ElasticSearchProjectionStoreOptions>)null!));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForUriOverload()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>("http://localhost:9200"));
	}

	[Fact]
	public void ThrowWhenNodeUriIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>((string)null!));
	}

	[Fact]
	public void ThrowWhenNodeUriIsEmpty()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>(""));
	}

	[Fact]
	public void ThrowWhenNodeUriIsWhitespace()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>("   "));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForClientFactoryOverload()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>(
				_ => new ElasticsearchClient(), _ => { }));
	}

	[Fact]
	public void ThrowWhenClientFactoryIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>(
				(Func<IServiceProvider, ElasticsearchClient>)null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNullForClientFactoryOverload()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticSearchProjectionStore<TestProjection>(
				_ => new ElasticsearchClient(),
				(Action<ElasticSearchProjectionStoreOptions>)null!));
	}

	[Fact]
	public void ReturnServiceCollectionForChainingFromConfigureOverload()
	{
		var services = new ServiceCollection();
		var result = services.AddElasticSearchProjectionStore<TestProjection>(_ => { });
		result.ShouldBe(services);
	}

	[Fact]
	public void ReturnServiceCollectionForChainingFromUriOverload()
	{
		var services = new ServiceCollection();
		var result = services.AddElasticSearchProjectionStore<TestProjection>("http://localhost:9200");
		result.ShouldBe(services);
	}

	[Fact]
	public void ReturnServiceCollectionForChainingFromClientFactoryOverload()
	{
		var services = new ServiceCollection();
		var result = services.AddElasticSearchProjectionStore<TestProjection>(
			_ => new ElasticsearchClient(), _ => { });
		result.ShouldBe(services);
	}

	[Fact]
	public void RegisterOptionsWithConfiguration()
	{
		var services = new ServiceCollection();
		services.AddElasticSearchProjectionStore<TestProjection>(
			o => o.IndexPrefix = "test-proj");

		using var sp = services.BuildServiceProvider();
		var options = sp.GetService<IOptions<ElasticSearchProjectionStoreOptions>>();
		options.ShouldNotBeNull();
		options.Value.IndexPrefix.ShouldBe("test-proj");
	}

	[Fact]
	public void ApplyUriFromUriOverload()
	{
		var services = new ServiceCollection();
		services.AddElasticSearchProjectionStore<TestProjection>("http://custom:9200");

		using var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<ElasticSearchProjectionStoreOptions>>();
		options.Value.NodeUri.ShouldBe("http://custom:9200");
	}

	[Fact]
	public void ApplyAdditionalConfigFromUriOverload()
	{
		var services = new ServiceCollection();
		services.AddElasticSearchProjectionStore<TestProjection>(
			"http://custom:9200",
			o => o.IndexPrefix = "custom-proj");

		using var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<ElasticSearchProjectionStoreOptions>>();
		options.Value.NodeUri.ShouldBe("http://custom:9200");
		options.Value.IndexPrefix.ShouldBe("custom-proj");
	}

	private sealed class TestProjection
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
	}
}
