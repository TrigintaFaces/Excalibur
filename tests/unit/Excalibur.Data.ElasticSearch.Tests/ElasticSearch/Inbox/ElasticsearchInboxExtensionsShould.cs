// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Inbox;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Inbox;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchInboxExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticsearchInboxStore(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticsearchInboxStore(null!));
	}

	[Fact]
	public void RegisterInboxOptionsWithValidation()
	{
		var services = new ServiceCollection();
		services.AddElasticsearchInboxStore(o => o.IndexName = "test-inbox");

		using var sp = services.BuildServiceProvider();
		var options = sp.GetService<IOptions<ElasticsearchInboxOptions>>();
		options.ShouldNotBeNull();
		options.Value.IndexName.ShouldBe("test-inbox");
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddElasticsearchInboxStore(_ => { });
		result.ShouldBe(services);
	}

	[Fact]
	public void ThrowWhenBuilderIsNullForUseMethod()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(
			() => builder.UseElasticsearchInboxStore(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNullForUseMethod()
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(new ServiceCollection());
		Should.Throw<ArgumentNullException>(
			() => builder.UseElasticsearchInboxStore(null!));
	}

	[Fact]
	public void ReturnBuilderForChaining()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		var result = builder.UseElasticsearchInboxStore(_ => { });
		result.ShouldBe(builder);
	}
}
