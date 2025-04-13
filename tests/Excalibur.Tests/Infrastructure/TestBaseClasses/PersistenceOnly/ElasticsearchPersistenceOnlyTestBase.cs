using Excalibur.Tests.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;

[Collection(nameof(ElasticsearchPersistenceOnlyContainerCollection))]
public abstract class ElasticsearchPersistenceOnlyTestBase(ElasticsearchContainerFixture fixture, ITestOutputHelper output)
	: PersistenceOnlyTestBase<ElasticsearchContainerFixture>(fixture, output)
{
	protected override Task InitializePersistenceAsync() => Task.CompletedTask;
}
