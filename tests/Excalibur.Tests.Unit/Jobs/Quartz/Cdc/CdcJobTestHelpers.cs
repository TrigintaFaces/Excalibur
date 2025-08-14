using System.Collections.ObjectModel;
using System.Data;

using Excalibur.DataAccess.SqlServer.Cdc;
using Excalibur.Jobs.Quartz.Cdc;

using FakeItEasy;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Unit.Jobs.Quartz.Cdc;

/// <summary>
/// Contains extension methods to help with testing CDC job functionality
/// </summary>
public static class CdcJobTestHelpers
{
	/// <summary>
	/// Extension method to mock getting a connection from configuration
	/// </summary>
	public static IDbConnection GetSqlConnection(this IConfiguration configuration, string connectionName)
	{
		return A.Fake<IDbConnection>();
	}

	/// <summary>
	/// Creates a test DatabaseConfig with the specified values
	/// </summary>
	public static DatabaseConfig CreateDatabaseConfig(
		string databaseName = "TestDb",
		string dbConnectionId = "TestDbConnection",
		string stateConnectionId = "TestStateConnection")
	{
		return new DatabaseConfig
		{
			DatabaseName = databaseName,
			DatabaseConnectionIdentifier = dbConnectionId,
			StateConnectionIdentifier = stateConnectionId,
			CaptureInstances = new[] { "dbo_TestTable" }
		};
	}

	/// <summary>
	/// Creates a test CdcJobConfig with the specified values
	/// </summary>
	public static CdcJobConfig CreateCdcJobConfig(
		string jobName = "TestCdcJob",
		string jobGroup = "TestGroup",
		string cronSchedule = "0 */5 * * *",
		bool disabled = false)
	{
		return new CdcJobConfig
		{
			JobName = jobName,
			JobGroup = jobGroup,
			CronSchedule = cronSchedule,
			Disabled = disabled,
			DegradedThreshold = TimeSpan.FromMinutes(10),
			UnhealthyThreshold = TimeSpan.FromMinutes(30),
			DatabaseConfigs = new Collection<DatabaseConfig>
			{
				CreateDatabaseConfig()
			}
		};
	}
}
