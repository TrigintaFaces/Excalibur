using Excalibur.Tests.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses.Host;

[Collection(nameof(ElasticsearchHostContainerCollection))]
public abstract class ElasticSearchHostTestBase(ElasticsearchContainerFixture fixture, ITestOutputHelper output)
	: HostTestBase<ElasticsearchContainerFixture>(fixture, output)
{
	protected override Task InitializePersistenceAsync() => Task.CompletedTask;
}
