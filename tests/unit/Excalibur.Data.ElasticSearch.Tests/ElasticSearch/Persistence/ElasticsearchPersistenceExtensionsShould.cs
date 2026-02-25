// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Persistence;

namespace Excalibur.Data.Tests.ElasticSearch.Persistence;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchPersistenceExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticsearchPersistence(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticsearchPersistence(null!));
	}

	[Fact]
	public void RegisterPersistenceOptionsWithConfiguration()
	{
		var services = new ServiceCollection();
		services.AddElasticsearchPersistence(o => o.IndexPrefix = "test-");

		using var sp = services.BuildServiceProvider();
		var options = sp.GetService<IOptions<ElasticsearchPersistenceOptions>>();
		options.ShouldNotBeNull();
		options.Value.IndexPrefix.ShouldBe("test-");
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddElasticsearchPersistence(_ => { });
		result.ShouldBe(services);
	}
}
