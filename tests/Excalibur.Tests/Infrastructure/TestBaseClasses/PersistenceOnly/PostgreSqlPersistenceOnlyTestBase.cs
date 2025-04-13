using Excalibur.Tests.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;

[Collection(nameof(PostgreSqlPersistenceOnlyContainerCollection))]
public abstract class PostgreSqlPersistenceOnlyTestBase(PostgreSqlContainerFixture fixture, ITestOutputHelper output)
	: PersistenceOnlyTestBase<PostgreSqlContainerFixture>(fixture, output);
