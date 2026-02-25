// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.Persistence;

using Npgsql;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Configuration options for Postgres persistence provider.
/// </summary>
/// <remarks>
/// Properties mandated by <see cref="IPersistenceOptions"/> remain on this root class.
/// Postgres-specific networking/connection settings are in <see cref="Connection"/>,
/// and prepared-statement settings are in <see cref="Statements"/>.
/// </remarks>
public sealed class PostgresPersistenceOptions : IPersistenceOptions, IPersistenceResilienceOptions, IPersistencePoolingOptions, IPersistenceObservabilityOptions
{
	/// <summary>
	/// Gets or sets the connection string for the Postgres database.
	/// </summary>
	/// <value>
	/// The connection string for the Postgres database.
	/// </value>
	[Required(ErrorMessage = "Connection string is required")]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the connection timeout in seconds. Default is 30 seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds. Default is 30 seconds.
	/// </value>
	[Range(1, 300, ErrorMessage = "Connection timeout must be between 1 and 300 seconds")]
	public int ConnectionTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the command timeout in seconds. Default is 30 seconds.
	/// </summary>
	/// <value>
	/// The command timeout in seconds. Default is 30 seconds.
	/// </value>
	[Range(1, 3600, ErrorMessage = "Command timeout must be between 1 and 3600 seconds")]
	public int CommandTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures. Default is 3.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts for transient failures. Default is 3.
	/// </value>
	[Range(0, 10, ErrorMessage = "Max retry attempts must be between 0 and 10")]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts in milliseconds. Default is 1000ms.
	/// </summary>
	/// <value>
	/// The delay between retry attempts in milliseconds. Default is 1000ms.
	/// </value>
	[Range(100, 30000, ErrorMessage = "Retry delay must be between 100 and 30000 milliseconds")]
	public int RetryDelayMilliseconds { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling. Default is true.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable connection pooling. Default is true.
	/// </value>
	public bool EnableConnectionPooling { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum pool size when connection pooling is enabled. Default is 100.
	/// </summary>
	/// <value>
	/// The maximum pool size when connection pooling is enabled. Default is 100.
	/// </value>
	[Range(1, 1000, ErrorMessage = "Max pool size must be between 1 and 1000")]
	public int MaxPoolSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum pool size when connection pooling is enabled. Default is 0.
	/// </summary>
	/// <value>
	/// The minimum pool size when connection pooling is enabled. Default is 0.
	/// </value>
	[Range(0, 100, ErrorMessage = "Min pool size must be between 0 and 100")]
	public int MinPoolSize { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed logging. Default is false.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable detailed logging. Default is false.
	/// </value>
	public bool EnableDetailedLogging { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection. Default is true.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable metrics collection. Default is true.
	/// </value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets provider-specific options as key-value pairs.
	/// </summary>
	/// <value>
	/// Provider-specific options as key-value pairs.
	/// </value>
	public IDictionary<string, object> ProviderSpecificOptions { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the Postgres-specific connection and networking options.
	/// </summary>
	/// <value>
	/// The connection options including TCP keepalive, idle lifetime, pruning, socket buffers, and application identity.
	/// </value>
	public PostgresConnectionOptions Connection { get; } = new();

	/// <summary>
	/// Gets the Postgres-specific prepared statement options.
	/// </summary>
	/// <value>
	/// The statement options including caching, auto-prepare, and max prepared statements.
	/// </value>
	public PostgresStatementOptions Statements { get; } = new();

	/// <summary>
	/// Validates the options and throws an exception if invalid.
	/// </summary>
	/// <exception cref="ValidationException"> Thrown when validation fails. </exception>
	[RequiresUnreferencedCode("Validator.TryValidateObject uses reflection to inspect object properties for validation attributes. Ensure all validated types are annotated with DynamicallyAccessedMembers.")]
	public void Validate()
	{
		var validationContext = new ValidationContext(this);
		var validationResults = new List<ValidationResult>();

		if (!Validator.TryValidateObject(this, validationContext, validationResults, validateAllProperties: true))
		{
			var errors = string.Join("; ", validationResults.Select(static r => r.ErrorMessage));
			throw new ValidationException($"Postgres persistence options validation failed: {errors}");
		}

		// Additional custom validation
		if (MinPoolSize > MaxPoolSize)
		{
			throw new ValidationException("MinPoolSize cannot be greater than MaxPoolSize");
		}

		if (EnableConnectionPooling && MaxPoolSize < 1)
		{
			throw new ValidationException("MaxPoolSize must be at least 1 when connection pooling is enabled");
		}

		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new ValidationException("ConnectionString cannot be null or empty");
		}

		// Validate sub-options
		Connection.Validate();
		Statements.Validate();
	}

	/// <summary>
	/// Builds a Npgsql connection string from the options.
	/// </summary>
	/// <returns> The formatted connection string. </returns>
	public string BuildConnectionString()
	{
		var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
		{
			Timeout = ConnectionTimeout,
			CommandTimeout = CommandTimeout,
			Pooling = EnableConnectionPooling,
			MaxPoolSize = MaxPoolSize,
			MinPoolSize = MinPoolSize,
			ConnectionIdleLifetime = Connection.ConnectionIdleLifetime,
			ConnectionPruningInterval = Connection.ConnectionPruningInterval,
			IncludeErrorDetail = Connection.IncludeErrorDetail,
		};

		if (!string.IsNullOrEmpty(Connection.ApplicationName))
		{
			builder.ApplicationName = Connection.ApplicationName;
		}

		if (Connection.EnableTcpKeepAlive)
		{
			builder.TcpKeepAliveTime = Connection.TcpKeepAliveTime;
			builder.TcpKeepAliveInterval = Connection.TcpKeepAliveInterval;
		}

		// Note: ReceiveBufferSize and SendBufferSize were removed in newer Npgsql versions These settings are now handled at the OS level
		if (Statements.EnablePreparedStatementCaching)
		{
			builder.MaxAutoPrepare = Statements.MaxPreparedStatements;
			builder.AutoPrepareMinUsages = Statements.AutoPrepareMinUsages;
		}

		return builder.ToString();
	}
}

/// <summary>
/// Postgres-specific connection and networking options.
/// </summary>
/// <remarks>
/// Groups TCP keepalive, idle lifetime, pruning interval, socket buffer sizes,
/// error detail, application name, and default database settings.
/// </remarks>
public sealed class PostgresConnectionOptions
{
	/// <summary>
	/// Gets or sets the application name for connection identification.
	/// </summary>
	/// <value>
	/// The application name for connection identification.
	/// </value>
	public string? ApplicationName { get; set; }

	/// <summary>
	/// Gets or sets the default database name for connections.
	/// </summary>
	/// <value>
	/// The default database name for connections.
	/// </value>
	public string? DefaultDatabase { get; set; }

	/// <summary>
	/// Gets or sets the connection idle lifetime in seconds. Default is 300 seconds.
	/// </summary>
	/// <value>
	/// The connection idle lifetime in seconds. Default is 300 seconds.
	/// </value>
	[Range(0, 3600, ErrorMessage = "Connection idle lifetime must be between 0 and 3600 seconds")]
	public int ConnectionIdleLifetime { get; set; } = 300;

	/// <summary>
	/// Gets or sets the connection pruning interval in seconds. Default is 10 seconds.
	/// </summary>
	/// <value>
	/// The connection pruning interval in seconds. Default is 10 seconds.
	/// </value>
	[Range(1, 60, ErrorMessage = "Connection pruning interval must be between 1 and 60 seconds")]
	public int ConnectionPruningInterval { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable TCP keepalive. Default is true.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable TCP keepalive. Default is true.
	/// </value>
	public bool EnableTcpKeepAlive { get; set; } = true;

	/// <summary>
	/// Gets or sets the TCP keepalive time in seconds. Default is 30 seconds.
	/// </summary>
	/// <value>
	/// The TCP keepalive time in seconds. Default is 30 seconds.
	/// </value>
	[Range(1, 300, ErrorMessage = "TCP keepalive time must be between 1 and 300 seconds")]
	public int TcpKeepAliveTime { get; set; } = 30;

	/// <summary>
	/// Gets or sets the TCP keepalive interval in seconds. Default is 1 second.
	/// </summary>
	/// <value>
	/// The TCP keepalive interval in seconds. Default is 1 second.
	/// </value>
	[Range(1, 60, ErrorMessage = "TCP keepalive interval must be between 1 and 60 seconds")]
	public int TcpKeepAliveInterval { get; set; } = 1;

	/// <summary>
	/// Gets or sets a value indicating whether to include error detail in exceptions. Default is true in development.
	/// </summary>
	/// <value>
	/// A value indicating whether to include error detail in exceptions. Default is true in development.
	/// </value>
	public bool IncludeErrorDetail { get; set; } = true;

	/// <summary>
	/// Gets or sets the socket receive buffer size. Default is system default (0).
	/// </summary>
	/// <value>
	/// The socket receive buffer size. Default is system default (0).
	/// </value>
	[Range(0, 1048576, ErrorMessage = "Socket receive buffer size must be between 0 and 1048576 bytes")]
	public int SocketReceiveBufferSize { get; set; }

	/// <summary>
	/// Gets or sets the socket send buffer size. Default is system default (0).
	/// </summary>
	/// <value>
	/// The socket send buffer size. Default is system default (0).
	/// </value>
	[Range(0, 1048576, ErrorMessage = "Socket send buffer size must be between 0 and 1048576 bytes")]
	public int SocketSendBufferSize { get; set; }

	/// <summary>
	/// Validates the connection options.
	/// </summary>
	/// <exception cref="ValidationException"> Thrown when validation fails. </exception>
	internal void Validate()
	{
		var validationContext = new ValidationContext(this);
		var validationResults = new List<ValidationResult>();

		if (!Validator.TryValidateObject(this, validationContext, validationResults, validateAllProperties: true))
		{
			var errors = string.Join("; ", validationResults.Select(static r => r.ErrorMessage));
			throw new ValidationException($"Postgres connection options validation failed: {errors}");
		}
	}
}

/// <summary>
/// Postgres-specific prepared statement options.
/// </summary>
/// <remarks>
/// Groups prepared statement caching, auto-prepare, and usage threshold settings.
/// </remarks>
public sealed class PostgresStatementOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable prepared statement caching. Default is true.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable prepared statement caching. Default is true.
	/// </value>
	public bool EnablePreparedStatementCaching { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of prepared statements to cache. Default is 200.
	/// </summary>
	/// <value>
	/// The maximum number of prepared statements to cache. Default is 200.
	/// </value>
	[Range(0, 1000, ErrorMessage = "Max prepared statements must be between 0 and 1000")]
	public int MaxPreparedStatements { get; set; } = 200;

	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic preparation of statements. Default is true.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable automatic preparation of statements. Default is true.
	/// </value>
	public bool EnableAutoPrepare { get; set; } = true;

	/// <summary>
	/// Gets or sets the auto prepare minimum usages before a statement is prepared. Default is 2.
	/// </summary>
	/// <value>
	/// The auto prepare minimum usages before a statement is prepared. Default is 2.
	/// </value>
	[Range(1, 10, ErrorMessage = "Auto prepare min usages must be between 1 and 10")]
	public int AutoPrepareMinUsages { get; set; } = 2;

	/// <summary>
	/// Validates the statement options.
	/// </summary>
	/// <exception cref="ValidationException"> Thrown when validation fails. </exception>
	internal void Validate()
	{
		var validationContext = new ValidationContext(this);
		var validationResults = new List<ValidationResult>();

		if (!Validator.TryValidateObject(this, validationContext, validationResults, validateAllProperties: true))
		{
			var errors = string.Join("; ", validationResults.Select(static r => r.ErrorMessage));
			throw new ValidationException($"Postgres statement options validation failed: {errors}");
		}
	}
}
