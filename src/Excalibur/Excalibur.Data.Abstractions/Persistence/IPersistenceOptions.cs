// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Core interface for persistence provider options. Contains connection
/// fundamentals and extensibility — at most 5 members per ISP gate.
/// </summary>
/// <remarks>
/// <para>
/// Follows the ISP (Interface Segregation Principle) pattern established in
/// Sprint 553 (<c>IPersistenceProvider</c> / <c>IPersistenceProviderHealth</c> /
/// <c>IPersistenceProviderTransaction</c>). Optional capabilities are in
/// focused sub-interfaces:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="IPersistenceResilienceOptions"/> — retry attempts and delay</description></item>
/// <item><description><see cref="IPersistencePoolingOptions"/> — connection pool sizing</description></item>
/// <item><description><see cref="IPersistenceObservabilityOptions"/> — logging and metrics toggles</description></item>
/// </list>
/// </remarks>
public interface IPersistenceOptions
{
	/// <summary>
	/// Gets or sets the connection string for the persistence provider.
	/// </summary>
	/// <value>
	/// The connection string for the persistence provider.
	/// </value>
	string ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the connection timeout in seconds.
	/// </summary>
	/// <value>
	/// The connection timeout in seconds.
	/// </value>
	int ConnectionTimeout { get; set; }

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	/// <value>
	/// The command timeout in seconds.
	/// </value>
	int CommandTimeout { get; set; }

	/// <summary>
	/// Gets provider-specific options as key-value pairs.
	/// </summary>
	/// <value>
	/// Provider-specific options as key-value pairs.
	/// </value>
	IDictionary<string, object> ProviderSpecificOptions { get; }

	/// <summary>
	/// Validates the options and throws an exception if invalid.
	/// </summary>
	void Validate();
}

/// <summary>
/// Resilience options for persistence providers. Controls retry behavior
/// for transient failure recovery.
/// </summary>
public interface IPersistenceResilienceOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts for transient failures.
	/// </value>
	int MaxRetryAttempts { get; set; }

	/// <summary>
	/// Gets or sets the delay between retry attempts in milliseconds.
	/// </summary>
	/// <value>
	/// The delay between retry attempts in milliseconds.
	/// </value>
	int RetryDelayMilliseconds { get; set; }
}

/// <summary>
/// Connection pooling options for persistence providers. Controls pool sizing
/// and enablement. Not all providers support pooling (e.g., in-memory).
/// </summary>
public interface IPersistencePoolingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable connection pooling.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable connection pooling.
	/// </value>
	bool EnableConnectionPooling { get; set; }

	/// <summary>
	/// Gets or sets the maximum pool size when connection pooling is enabled.
	/// </summary>
	/// <value>
	/// The maximum pool size when connection pooling is enabled.
	/// </value>
	int MaxPoolSize { get; set; }

	/// <summary>
	/// Gets or sets the minimum pool size when connection pooling is enabled.
	/// </summary>
	/// <value>
	/// The minimum pool size when connection pooling is enabled.
	/// </value>
	int MinPoolSize { get; set; }
}

/// <summary>
/// Observability options for persistence providers. Controls diagnostic
/// logging verbosity and metrics collection.
/// </summary>
public interface IPersistenceObservabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed logging.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable detailed logging.
	/// </value>
	bool EnableDetailedLogging { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable metrics collection.
	/// </value>
	bool EnableMetrics { get; set; }
}
