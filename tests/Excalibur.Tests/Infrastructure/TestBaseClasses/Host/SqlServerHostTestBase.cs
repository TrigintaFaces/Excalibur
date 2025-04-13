using Excalibur.Tests.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses.Host;

[Collection(nameof(SqlServerHostContainerCollection))]
public abstract class SqlServerHostTestBase(SqlServerContainerFixture fixture, ITestOutputHelper output)
	: HostTestBase<SqlServerContainerFixture>(fixture, output);
