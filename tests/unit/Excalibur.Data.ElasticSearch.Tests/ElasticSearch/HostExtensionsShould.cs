// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class HostExtensionsShould
{
	[Fact]
	public async Task ThrowWhenHostIsNull()
	{
		IHost host = null!;
		await Should.ThrowAsync<ArgumentNullException>(
			() => host.InitializeElasticsearchIndexesAsync());
	}

	[Fact]
	public async Task CallIndexInitializerFromHost()
	{
		// Arrange
		var initializer = A.Fake<IIndexInitializer>();
		A.CallTo(() => initializer.InitializeIndexesAsync())
			.Returns(Task.CompletedTask);

		var host = new HostBuilder()
			.ConfigureServices(services =>
			{
				services.AddSingleton(initializer);
			})
			.Build();

		// Act
		await host.InitializeElasticsearchIndexesAsync();

		// Assert
		A.CallTo(() => initializer.InitializeIndexesAsync())
			.MustHaveHappenedOnceExactly();

		host.Dispose();
	}
}
