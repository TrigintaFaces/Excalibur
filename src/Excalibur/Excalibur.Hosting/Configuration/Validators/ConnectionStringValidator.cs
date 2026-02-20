// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data.Common;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Validates database connection strings for various providers.
/// </summary>
public sealed partial class ConnectionStringValidator : ConfigurationValidatorBase
{
	private readonly string _connectionStringKey;
	private readonly DatabaseProvider _provider;
	private readonly bool _testConnection;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConnectionStringValidator" /> class.
	/// </summary>
	/// <param name="connectionStringKey"> The configuration key for the connection string. </param>
	/// <param name="provider"> The database provider type. </param>
	/// <param name="testConnection"> Whether to test the actual connection. </param>
	/// <param name="configurationName"> The name of the configuration being validated. </param>
	public ConnectionStringValidator(
		string connectionStringKey,
		DatabaseProvider provider,
		bool testConnection = false,
		string? configurationName = null)
		: base(configurationName ?? $"ConnectionString:{connectionStringKey}", priority: 10)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringKey);

		_connectionStringKey = connectionStringKey;
		_provider = provider;
		_testConnection = testConnection;
	}

	/// <inheritdoc />
	public override async Task<ConfigurationValidationResult> ValidateAsync(
		IConfiguration configuration,
		CancellationToken cancellationToken)
	{
		var errors = new List<ConfigurationValidationError>();

		// Check if connection string exists
		var connectionString = configuration.GetConnectionString(_connectionStringKey)
							   ?? configuration[$"ConnectionStrings:{_connectionStringKey}"];

		if (string.IsNullOrWhiteSpace(connectionString))
		{
			errors.Add(new ConfigurationValidationError(
				$"Connection string '{_connectionStringKey}' is missing or empty",
				$"ConnectionStrings:{_connectionStringKey}",
				value: null,
				$"Add a connection string for '{_connectionStringKey}' in appsettings.json or environment variables"));
			return ConfigurationValidationResult.Failure(errors);
		}

		// Validate connection string format based on provider
		ValidateConnectionStringFormat(connectionString, errors);

		// Test actual connection if requested
		if (_testConnection && errors.Count == 0)
		{
			await TestConnectionAsync(connectionString, errors, cancellationToken).ConfigureAwait(false);
		}

		return errors.Count == 0
			? ConfigurationValidationResult.Success()
			: ConfigurationValidationResult.Failure(errors);
	}

	private static void ValidateSqlServerConnectionString(DbConnectionStringBuilder builder, List<ConfigurationValidationError> errors)
	{
		// Required parameters for SQL Server
		_ = new[] { "Server", "Data Source", "Initial Catalog", "Database" };
		var hasServer = builder.ContainsKey("Server") || builder.ContainsKey("Data Source");
		var hasDatabase = builder.ContainsKey("Initial Catalog") || builder.ContainsKey("Database");

		if (!hasServer)
		{
			errors.Add(new ConfigurationValidationError(
				"SQL Server connection string missing server/data source",
				"ConnectionString",
				value: null,
				"Add 'Server=servername' or 'Data Source=servername' to the connection string"));
		}

		if (!hasDatabase)
		{
			errors.Add(new ConfigurationValidationError(
				"SQL Server connection string missing database name",
				"ConnectionString",
				value: null,
				"Add 'Initial Catalog=database' or 'Database=database' to the connection string"));
		}

		// Check authentication
		var hasIntegratedSecurity = builder.ContainsKey("Integrated Security") || builder.ContainsKey("Trusted_Connection");
		var hasUserPassword = builder.ContainsKey("User ID") && builder.ContainsKey("Password");

		if (!hasIntegratedSecurity && !hasUserPassword)
		{
			errors.Add(new ConfigurationValidationError(
				"SQL Server connection string missing authentication information",
				"ConnectionString",
				value: null,
				"Add either 'Integrated Security=true' or 'User ID=username;Password=password'"));
		}
	}

	private static void ValidatePostgresConnectionString(DbConnectionStringBuilder builder, List<ConfigurationValidationError> errors)
	{
		// Required parameters for Postgres
		if (!builder.ContainsKey("Host") && !builder.ContainsKey("Server"))
		{
			errors.Add(new ConfigurationValidationError(
				"Postgres connection string missing host/server",
				"ConnectionString",
				value: null,
				"Add 'Host=hostname' or 'Server=hostname' to the connection string"));
		}

		if (!builder.ContainsKey("Database"))
		{
			errors.Add(new ConfigurationValidationError(
				"Postgres connection string missing database name",
				"ConnectionString",
				value: null,
				"Add 'Database=dbname' to the connection string"));
		}

		// Check authentication
		if (!builder.ContainsKey("Username") && !builder.ContainsKey("User ID"))
		{
			errors.Add(new ConfigurationValidationError(
				"Postgres connection string missing username",
				"ConnectionString",
				value: null,
				"Add 'Username=user' or 'User ID=user' to the connection string"));
		}
	}

	private static void ValidateMySqlConnectionString(DbConnectionStringBuilder builder, List<ConfigurationValidationError> errors)
	{
		// Required parameters for MySQL
		if (!builder.ContainsKey("Server") && !builder.ContainsKey("Host"))
		{
			errors.Add(new ConfigurationValidationError(
				"MySQL connection string missing server/host",
				"ConnectionString",
				value: null,
				"Add 'Server=hostname' or 'Host=hostname' to the connection string"));
		}

		if (!builder.ContainsKey("Database"))
		{
			errors.Add(new ConfigurationValidationError(
				"MySQL connection string missing database name",
				"ConnectionString",
				value: null,
				"Add 'Database=dbname' to the connection string"));
		}

		if (!builder.ContainsKey("User") && !builder.ContainsKey("User ID") && !builder.ContainsKey("Uid"))
		{
			errors.Add(new ConfigurationValidationError(
				"MySQL connection string missing user",
				"ConnectionString",
				value: null,
				"Add 'User=username', 'User ID=username', or 'Uid=username' to the connection string"));
		}
	}

	private static void ValidateSqliteConnectionString(DbConnectionStringBuilder builder, List<ConfigurationValidationError> errors)
	{
		// Required parameters for SQLite
		if (!builder.ContainsKey("Data Source") && !builder.ContainsKey("Filename"))
		{
			errors.Add(new ConfigurationValidationError(
				"SQLite connection string missing data source",
				"ConnectionString",
				value: null,
				"Add 'Data Source=database.db' or 'Filename=database.db' to the connection string"));
		}
	}

	private static void ValidateMongoDbConnectionString(string connectionString, List<ConfigurationValidationError> errors)
	{
		// MongoDB connection strings follow a URI format
		if (!connectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase) &&
			!connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
		{
			errors.Add(new ConfigurationValidationError(
				"MongoDB connection string must start with 'mongodb://' or 'mongodb+srv://'",
				"ConnectionString",
				value: null,
				"Use format: mongodb://[username:password@]host[:port][/database][?options]"));
		}

		// Basic URI validation
		if (!Uri.TryCreate(connectionString, UriKind.Absolute, out _))
		{
			errors.Add(new ConfigurationValidationError(
				"MongoDB connection string is not a valid URI",
				"ConnectionString",
				value: null,
				"Ensure the connection string follows MongoDB URI format"));
		}
	}

	private static void ValidateRedisConnectionString(string connectionString, List<ConfigurationValidationError> errors)
	{
		// Redis connection strings can be simple (host:port) or complex (key=value pairs)
		if (connectionString.Contains('=', StringComparison.Ordinal))
		{
			// Configuration string format
			var parts = connectionString.Split(',', StringSplitOptions.RemoveEmptyEntries);
			var hasEndpoint = false;

			foreach (var part in parts)
			{
				if (part.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
					part.Contains("127.0.0.1", StringComparison.Ordinal) ||
					RedisEndpointRegex().IsMatch(part))
				{
					hasEndpoint = true;
					break;
				}
			}

			if (!hasEndpoint)
			{
				errors.Add(new ConfigurationValidationError(
					"Redis connection string missing endpoint",
					"ConnectionString",
					value: null,
					"Add an endpoint like 'localhost:6379' or use configuration format with proper keys"));
			}
		}
		else
		{
			// Simple format: host:port
			if (!connectionString.Contains(':', StringComparison.Ordinal))
			{
				errors.Add(new ConfigurationValidationError(
					"Redis connection string missing port",
					"ConnectionString",
					connectionString,
					"Use format 'host:port' (e.g., 'localhost:6379')"));
			}
		}
	}

	[GeneratedRegex(@"^[^:]+:\d+$")]
	private static partial Regex RedisEndpointRegex();

	private void ValidateConnectionStringFormat(string connectionString, List<ConfigurationValidationError> errors)
	{
		// MongoDB and Redis use URI format, not key=value pairs
		// Handle them separately without DbConnectionStringBuilder
		switch (_provider)
		{
			case DatabaseProvider.MongoDb:
				ValidateMongoDbConnectionString(connectionString, errors);
				return;

			case DatabaseProvider.Redis:
				ValidateRedisConnectionString(connectionString, errors);
				return;
		}

		// For other providers, use DbConnectionStringBuilder
		try
		{
			var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

			switch (_provider)
			{
				case DatabaseProvider.SqlServer:
					ValidateSqlServerConnectionString(builder, errors);
					break;

				case DatabaseProvider.Postgres:
					ValidatePostgresConnectionString(builder, errors);
					break;

				case DatabaseProvider.MySql:
					ValidateMySqlConnectionString(builder, errors);
					break;

				case DatabaseProvider.Sqlite:
					ValidateSqliteConnectionString(builder, errors);
					break;
			}
		}
		catch (ArgumentException ex)
		{
			errors.Add(new ConfigurationValidationError(
				$"Invalid connection string format: {ex.Message}",
				$"ConnectionStrings:{_connectionStringKey}",
				value: null,
				"Ensure the connection string follows the correct format for the database provider"));
		}
	}

	// Placeholder for future connection testing implementation
	// R0.8: Remove unused parameter - this is a stub method
#pragma warning disable IDE0060
	private static async Task TestConnectionAsync(
		string connectionString,
		List<ConfigurationValidationError> errors,
		CancellationToken cancellationToken) =>

		// This would require provider-specific connection testing For now, we'll just validate the format was correct
		await Task.CompletedTask.ConfigureAwait(false);
#pragma warning restore IDE0060 // Remove unused parameter
}
