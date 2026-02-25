// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Outbox;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchOutboxExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticsearchOutboxStore(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticsearchOutboxStore(null!));
	}

	[Fact]
	public void RegisterOutboxOptionsWithValidation()
	{
		var services = new ServiceCollection();
		services.AddElasticsearchOutboxStore(o => o.IndexName = "test-outbox");

		using var sp = services.BuildServiceProvider();
		var options = sp.GetService<IOptions<ElasticsearchOutboxOptions>>();
		options.ShouldNotBeNull();
		options.Value.IndexName.ShouldBe("test-outbox");
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddElasticsearchOutboxStore(_ => { });
		result.ShouldBe(services);
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForUseMethod()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(
			() => builder.UseElasticsearchOutboxStore(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNullForUseMethod()
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());
		Should.Throw<ArgumentNullException>(
			() => builder.UseElasticsearchOutboxStore(null!));
	}

	[Fact]
	public void ReturnBuilderForChaining()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		var result = builder.UseElasticsearchOutboxStore(_ => { });
		result.ShouldBe(builder);
	}
}
