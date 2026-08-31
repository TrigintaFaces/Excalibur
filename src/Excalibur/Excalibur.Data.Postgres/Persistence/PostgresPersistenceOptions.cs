// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Persistence;

using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Configuration options for Postgres persistence provider.
/// </summary>
/// <remarks>
/// <para>
/// Properties mandated by <see cref="IPersistenceOptions"/> remain on this root class.
/// Postgres-specific networking/connection settings are in <see cref="Connection"/>,
/// prepared-statement settings are in <see cref="Statements"/>,
/// pooling settings are in <see cref="Pooling"/>,
/// and resilience settings are in <see cref="Resilience"/>.
/// </para>
/// </remarks>
public sealed class PostgresPersistenceOptions : IPersistenceOptions
{
	/// <summary>
	/// Gets or sets the provider name. This is the consumer-configured instance name, reported as
	/// <c>Name</c> on the provider and in its metrics. It identifies <i>which configured instance</i>
	/// answered, and is distinct from the engine identity reported under the <c>Provider</c> metrics
	/// key, which is a fixed literal. When unset it defaults to <c>"postgres"</c>.
	/// </summary>
	/// <value>The provider name, or <see langword="null"/> to use the default.</value>
	public string? Name { get; set; }

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
	[ValidateObjectMembers]
	public PostgresConnectionOptions Connection { get; } = new();

	/// <summary>
	/// Gets the Postgres-specific prepared statement options.
	/// </summary>
	/// <value>
	/// The statement options including caching, auto-prepare, and max prepared statements.
	/// </value>
	[ValidateObjectMembers]
	public PostgresStatementOptions Statements { get; } = new();

	/// <summary>
	/// Gets the connection pooling options.
	/// </summary>
	/// <value>
	/// The pooling options including pool sizing and enablement.
	/// </value>
	[ValidateObjectMembers]
	public PostgresPersistencePoolingOptions Pooling { get; } = new();

	/// <summary>
	/// Gets the resilience options for retry behavior.
	/// </summary>
	/// <value>
	/// The resilience options including retry attempts and delay.
	/// </value>
	[ValidateObjectMembers]
	public PostgresPersistenceResilienceOptions Resilience { get; } = new();

	private static readonly PostgresPersistenceOptionsAnnotationValidator AnnotationValidator = new();

	/// <summary>
	/// Validates the options and throws an exception if invalid.
	/// </summary>
	/// <exception cref="ValidationException"> Thrown when validation fails. </exception>
	public void Validate()
	{
		var annotations = AnnotationValidator.Validate(name: null, this);
		if (annotations.Failed)
		{
			var errors = string.Join("; ", annotations.Failures ?? []);
			throw new ValidationException($"Postgres persistence options validation failed: {errors}");
		}

		// Additional custom validation
		if (Pooling.MinPoolSize > Pooling.MaxPoolSize)
		{
			throw new ValidationException("MinPoolSize cannot be greater than MaxPoolSize");
		}

		if (Pooling.EnableConnectionPooling && Pooling.MaxPoolSize < 1)
		{
			throw new ValidationException("MaxPoolSize must be at least 1 when connection pooling is enabled");
		}

		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new ValidationException("ConnectionString cannot be null or empty");
		}

		// The generated validator covers every annotation on the nested option objects; this call carries
		// the one cross-property rule that no annotation can express.
		Resilience.Validate();
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
			Pooling = Pooling.EnableConnectionPooling,
			MaxPoolSize = Pooling.MaxPoolSize,
			MinPoolSize = Pooling.MinPoolSize,
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
}
