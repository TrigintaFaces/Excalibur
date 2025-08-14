using System.Data;

using Excalibur.DataAccess.SqlServer.Cdc;

using FakeItEasy;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Tests.Unit.Jobs.Quartz.Cdc;

/// <summary>
/// Helper class for mocking SQL connections in Cdc job tests
/// </summary>
public class MockCdcDataChangeEventProcessorFactory : IDataChangeEventProcessorFactory
{
	private readonly IDataChangeEventProcessor _processor;

	public MockCdcDataChangeEventProcessorFactory(IDataChangeEventProcessor processor)
	{
		_processor = processor;
	}

	/// <summary>
	/// Method not used in tests - we only need the overload that accepts IDbConnection
	/// </summary>
	public IDataChangeEventProcessor Create(IDatabaseConfig dbConfig, Microsoft.Data.SqlClient.SqlConnection cdcConnection, Microsoft.Data.SqlClient.SqlConnection stateStoreConnection)
	{
		throw new NotImplementedException("This method should not be called in tests");
	}

	/// <summary>
	/// Creates a mock processor that can be used in tests without requiring a real SqlConnection
	/// </summary>
	public IDataChangeEventProcessor Create(IDatabaseConfig dbConfig, IDbConnection cdcConnection, IDbConnection stateStoreConnection)
	{
		return _processor;
	}
}

/// <summary>
/// Extensions for IConfiguration to provide a test friendly interface
/// </summary>
public static class MockConfigurationExtensions
{
	/// <summary>
	/// Provides a way to get an IDbConnection from configuration for testing
	/// </summary>
	public static IDbConnection GetSqlConnection(this IConfiguration configuration, string connectionName)
	{
		return A.Fake<IDbConnection>();
	}
}

/// <summary>
/// Helper to create test instances of logger
/// </summary>
public static class LoggerHelper
{
	/// <summary>
	/// Creates a fake logger for testing
	/// </summary>
	public static ILogger<T> CreateLogger<T>()
	{
		return A.Fake<ILogger<T>>();
	}
}
