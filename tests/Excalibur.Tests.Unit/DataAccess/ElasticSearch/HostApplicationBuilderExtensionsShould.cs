using Excalibur.DataAccess.ElasticSearch;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.ElasticSearch;

public class HostApplicationBuilderExtensionsShould
{
	[Fact]
	public async Task ShouldThrowIfBuilderIsNull()
	{
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			HostApplicationBuilderExtensions.InitializeElasticsearchIndexesAsync(null!)).ConfigureAwait(true);
	}

	[Fact]
	public async Task ShouldResolveIndexInitializerAndCallInitialize()
	{
		// Arrange
		var indexInitializer = A.Fake<IIndexInitializer>();

		var services = new ServiceCollection();
		_ = services.AddSingleton(indexInitializer);

		var serviceProvider = services.BuildServiceProvider();

		var builder = A.Fake<IHostApplicationBuilder>();
		_ = A.CallTo(() => builder.Services).Returns(services);

		// Act
		await builder.InitializeElasticsearchIndexesAsync().ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => indexInitializer.InitializeIndexesAsync()).MustHaveHappenedOnceExactly();
	}
}
