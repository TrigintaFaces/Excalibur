using Excalibur.Tests.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;

[Collection(nameof(SqlServerPersistenceOnlyContainerCollection))]
public abstract class SqlServerPersistenceOnlyTestBase(SqlServerContainerFixture fixture, ITestOutputHelper output) :
	PersistenceOnlyTestBase<SqlServerContainerFixture>(fixture, output);
