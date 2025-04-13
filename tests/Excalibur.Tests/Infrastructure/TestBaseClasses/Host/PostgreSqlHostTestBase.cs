using Excalibur.Tests.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses.Host;

[Collection(nameof(PostgreSqlHostContainerCollection))]
public abstract class PostgreSqlHostTestBase(PostgreSqlContainerFixture fixture, ITestOutputHelper output)
	: HostTestBase<PostgreSqlContainerFixture>(fixture, output);
