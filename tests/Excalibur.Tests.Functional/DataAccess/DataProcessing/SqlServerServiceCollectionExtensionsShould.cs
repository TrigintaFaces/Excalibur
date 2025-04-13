using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Functional.DataAccess.DataProcessing;

public class SqlServerServiceCollectionExtensionsShould : SqlServerPersistenceOnlyTestBase
{
	public SqlServerServiceCollectionExtensionsShould(SqlServerContainerFixture fixture, ITestOutputHelper output) : base(fixture, output)
	{
		ArgumentNullException.ThrowIfNull(fixture);
		using var connection = fixture.CreateDbConnection() as SqlConnection;
		DataProcessingMother.EnsureDatabaseInitializedAsync(connection!, DatabaseEngine.SqlServer).GetAwaiter().GetResult();
	}

	[Fact]
	public void ShouldResolveAllRegisteredServices()
	{
		// Act & Assert
		_ = ServiceProvider.GetRequiredService<IDataProcessorDb>().ShouldNotBeNull();
		_ = ServiceProvider.GetRequiredService<IDataToProcessDb>().ShouldNotBeNull();
		_ = ServiceProvider.GetRequiredService<IDataProcessorRegistry>().ShouldNotBeNull();
		_ = ServiceProvider.GetRequiredService<IDataOrchestrationManager>().ShouldNotBeNull();
	}

	[Fact]
	public void ShouldRetrieveRegisteredDataProcessorFactoryFromRegistry()
	{
		// Arrange
		var registry = ServiceProvider.GetRequiredService<IDataProcessorRegistry>();

		// Act
		var found = registry.TryGetFactory("User", out var factory);

		// Assert
		found.ShouldBeTrue();
		_ = factory.ShouldNotBeNull();
	}

	[Fact]
	public async Task ShouldProcessDataTasksThroughOrchestrationManager()
	{
		// Arrange
		var manager = ServiceProvider.GetRequiredService<IDataOrchestrationManager>();

		// Act
		var taskId = await manager.AddDataTaskForRecordType("User").ConfigureAwait(true);

		// Assert
		taskId.ShouldNotBe(Guid.Empty);
	}

	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		_ = services.AddDataProcessing<TestDb, TestDb>(configuration, "DataProcessing", typeof(AssemblyMarker).Assembly);
	}
}
