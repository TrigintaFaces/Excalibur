using Excalibur.Core.Exceptions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Excalibur.DataAccess.SqlServer;

/// <summary>
///     Provides extension methods for retrieving SQL connections and SQL database instances using configuration settings.
/// </summary>
public static class DataAccessExtensions
{
	/// <summary>
	///     Retrieves and opens a <see cref="SqlConnection" /> based on the specified connection name in the configuration.
	/// </summary>
	/// <param name="configuration"> The configuration object containing connection strings. </param>
	/// <param name="connectionName"> The name of the connection string in the configuration. </param>
	/// <returns> An open <see cref="SqlConnection" /> instance. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="connectionName" /> is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidConfigurationException"> Thrown if the connection string is not found or is empty in the configuration. </exception>
	public static SqlConnection GetSqlConnection(this IConfiguration configuration, string connectionName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

		var connectionString = GetConnectionString(configuration, connectionName);

		var connection = new SqlConnection(connectionString);
		connection.Open();

		return connection;
	}

	/// <summary>
	///     Retrieves a factory function for creating a <see cref="SqlDb" /> instance based on the specified connection name in the configuration.
	/// </summary>
	/// <param name="configuration"> The configuration object containing connection strings. </param>
	/// <param name="connectionName"> The name of the connection string in the configuration. </param>
	/// <returns> A factory function that, when invoked, returns a <see cref="SqlDb" /> instance initialized with an open SQL connection. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="connectionName" /> is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidConfigurationException"> Thrown if the connection string is not found or is empty in the configuration. </exception>
	public static Func<SqlDb> GetSqlDb(this IConfiguration configuration, string connectionName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

		return () =>
		{
			SqlConnection? connection = null;

			try
			{
				connection = configuration.GetSqlConnection(connectionName);
				return new SqlDb(connection);
			}
			catch (Exception)
			{
				connection?.Dispose();
				throw;
			}
		};
	}

	/// <summary>
	///     Retrieves the connection string for the specified connection name from the configuration.
	/// </summary>
	/// <param name="configuration"> The configuration object containing connection strings. </param>
	/// <param name="connectionName"> The name of the connection string in the configuration. </param>
	/// <returns> The connection string associated with the specified connection name. </returns>
	/// <exception cref="InvalidConfigurationException"> Thrown if the connection string is not found or is empty in the configuration. </exception>
	private static string GetConnectionString(IConfiguration configuration, string connectionName)
	{
		var connectionString = configuration.GetConnectionString(connectionName);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new InvalidConfigurationException("ConnectionStrings", message: $"ConnectionStrings missing value for {connectionName}.");
		}

		return connectionString;
	}
}
