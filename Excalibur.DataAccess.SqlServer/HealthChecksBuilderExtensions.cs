using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.DataAccess.SqlServer;

/// <summary>
///     Provides extension methods to configure health checks for SQL Server connections.
/// </summary>
public static class HealthChecksBuilderExtensions
{
	/// <summary>
	///     Adds a SQL Server health check to the health checks builder using a connection string from configuration.
	/// </summary>
	/// <param name="healthChecks"> The health checks builder to which the health check will be added. </param>
	/// <param name="configuration"> The application configuration containing the connection string. </param>
	/// <param name="name"> The name of the health check and the connection string key in the configuration. </param>
	/// <param name="timeout"> The timeout duration for the health check operation. </param>
	/// <returns> The updated health checks builder. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="name" /> is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidOperationException">
	///     Thrown if the connection string with the specified name is not found in the configuration.
	/// </exception>
	public static IHealthChecksBuilder AddSqlHealthCheck(
		this IHealthChecksBuilder healthChecks,
		IConfiguration configuration,
		string name,
		TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

		var connectionString = configuration.GetConnectionString(name);
		return AddSqlHealthCheck(healthChecks, connectionString, name, timeout);
	}

	/// <summary>
	///     Adds a SQL Server health check to the health checks builder using the specified connection string.
	/// </summary>
	/// <param name="healthChecks"> The health checks builder to which the health check will be added. </param>
	/// <param name="connectionString"> The connection string for the SQL Server. </param>
	/// <param name="name"> The name of the health check. </param>
	/// <param name="timeout"> The timeout duration for the health check operation. </param>
	/// <returns> The updated health checks builder. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="name" /> is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidOperationException"> Thrown if <paramref name="connectionString" /> is null or empty. </exception>
	public static IHealthChecksBuilder AddSqlHealthCheck(
		this IHealthChecksBuilder healthChecks,
		string? connectionString,
		string name,
		TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

		if (string.IsNullOrEmpty(connectionString))
		{
			throw new InvalidOperationException($"Could not find a connection string named '{name}");
		}

		_ = healthChecks.AddSqlServer(
			connectionString,
			healthQuery: "select 1",
			name: name,
			failureStatus: HealthStatus.Unhealthy,
			tags: ["Feedback", "Database"],
			timeout: timeout);

		return healthChecks;
	}
}
