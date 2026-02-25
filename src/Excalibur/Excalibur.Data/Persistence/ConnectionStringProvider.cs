// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Data.Common;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Implementation of connection string provider with configuration and secret management support.
/// </summary>
internal sealed partial class ConnectionStringProvider : IConnectionStringProvider, IDisposable
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<ConnectionStringProvider> _logger;
	private readonly ConcurrentDictionary<string, string> _connectionStrings = new(StringComparer.Ordinal);
	private readonly SemaphoreSlim _refreshLock = new(1, 1);

	[LoggerMessage(DataEventId.ConnectionStringSet, LogLevel.Debug, "Set connection string for '{ConnectionStringName}'")]
	private partial void LogSetConnectionString(string connectionStringName);

	[LoggerMessage(DataEventId.ConnectionStringRemoved, LogLevel.Debug, "Removed connection string '{ConnectionStringName}'")]
	private partial void LogRemovedConnectionString(string connectionStringName);

	[LoggerMessage(DataEventId.RefreshingConnectionStrings, LogLevel.Information, "Refreshing connection strings from configuration")]
	private partial void LogRefreshingConnectionStrings();

	[LoggerMessage(DataEventId.ConnectionStringsRefreshed, LogLevel.Information, "Connection strings refreshed successfully")]
	private partial void LogConnectionStringsRefreshed();

	[LoggerMessage(DataEventId.ValidationFailed, LogLevel.Warning, "Failed to validate connection string for provider type {ProviderType}")]
	private partial void LogValidationFailed(Exception ex, string providerType);

	[LoggerMessage(DataEventId.ConnectionStringsLoaded, LogLevel.Debug, "Loaded {Count} connection strings from configuration")]
	private partial void LogLoadedConnectionStrings(int count);

	[LoggerMessage(DataEventId.ResolvedFromEnvironment, LogLevel.Debug, "Resolved connection string '{Name}' from environment variable")]
	private partial void LogResolvedFromEnvironment(string name);

	[LoggerMessage(DataEventId.ReferencesSecretStore, LogLevel.Debug, "Connection string '{Name}' references external secret store")]
	private partial void LogReferencesSecretStore(string name);

	[LoggerMessage(DataEventId.CheckingExternalSources, LogLevel.Debug, "Checking for connection strings in external sources")]
	private partial void LogCheckingExternalSources();

	/// <summary>
	/// Initializes a new instance of the <see cref="ConnectionStringProvider" /> class.
	/// </summary>
	public ConnectionStringProvider(
		IConfiguration configuration,
		ILogger<ConnectionStringProvider> logger)
	{
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Load initial connection strings from configuration
		LoadConnectionStrings();
	}

	/// <inheritdoc />
	public string GetConnectionString(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (_connectionStrings.TryGetValue(name, out var connectionString))
		{
			return connectionString;
		}

		// Try to get from configuration
		var configValue = _configuration.GetConnectionString(name);
		if (!string.IsNullOrWhiteSpace(configValue))
		{
			_connectionStrings[name] = configValue;
			return configValue;
		}

		throw new InvalidOperationException($"Connection string '{name}' not found.");
	}

	/// <inheritdoc />
	public async Task<string> GetConnectionStringAsync(string name, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		// Check cache first
		if (_connectionStrings.TryGetValue(name, out var connectionString))
		{
			return connectionString;
		}

		// Try to resolve from external sources (e.g., Key Vault, Secrets Manager)
		connectionString = await ResolveFromExternalSourceAsync(name, cancellationToken).ConfigureAwait(false);

		if (!string.IsNullOrWhiteSpace(connectionString))
		{
			_connectionStrings[name] = connectionString;
			return connectionString;
		}

		// Fall back to synchronous method
		return GetConnectionString(name);
	}

	/// <inheritdoc />
	public bool TryGetConnectionString(string name, out string? connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (_connectionStrings.TryGetValue(name, out connectionString))
		{
			return true;
		}

		// Try to get from configuration
		connectionString = _configuration.GetConnectionString(name);
		if (!string.IsNullOrWhiteSpace(connectionString))
		{
			_connectionStrings[name] = connectionString;
			return true;
		}

		connectionString = null;
		return false;
	}

	/// <inheritdoc />
	public void SetConnectionString(string name, string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_connectionStrings[name] = connectionString;
		LogSetConnectionString(name);
	}

	/// <inheritdoc />
	public bool RemoveConnectionString(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var removed = _connectionStrings.TryRemove(name, out _);
		if (removed)
		{
			LogRemovedConnectionString(name);
		}

		return removed;
	}

	/// <inheritdoc />
	public IEnumerable<string> GetConnectionStringNames()
	{
		// Combine cached and configuration connection strings
		var cachedNames = _connectionStrings.Keys;
		var configSection = _configuration.GetSection("ConnectionStrings");
		var configNames = configSection.GetChildren().Select(static c => c.Key);

		return cachedNames.Union(configNames, StringComparer.Ordinal).Distinct(StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public async Task RefreshAsync(CancellationToken cancellationToken)
	{
		await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			LogRefreshingConnectionStrings();

			// Clear existing cache
			_connectionStrings.Clear();

			// Reload from configuration
			LoadConnectionStrings();

			// Reload from external sources if configured
			await LoadFromExternalSourcesAsync(cancellationToken).ConfigureAwait(false);

			LogConnectionStringsRefreshed();
		}
		finally
		{
			_ = _refreshLock.Release();
		}
	}

	/// <inheritdoc />
	public bool ConnectionStringExists(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		// Check cache first
		if (_connectionStrings.ContainsKey(name))
		{
			return true;
		}

		// Check configuration
		var configValue = _configuration.GetConnectionString(name);
		return !string.IsNullOrWhiteSpace(configValue);
	}

	/// <inheritdoc />
	public string BuildConnectionString(IDictionary<string, string> parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);

		var builder = new DbConnectionStringBuilder();
		foreach (var kvp in parameters)
		{
			builder[kvp.Key] = kvp.Value;
		}

		return builder.ConnectionString;
	}

	/// <inheritdoc />
	public IDictionary<string, string> ParseConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (string key in builder.Keys)
		{
			var value = builder[key];
			if (value != null)
			{
				result[key] = value.ToString() ?? string.Empty;
			}
		}

		return result;
	}

	/// <inheritdoc />
	public bool ValidateConnectionString(string connectionString, string providerType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(providerType);

		try
		{
			// Handle providers that don't use standard ADO.NET connection string format
			// before attempting DbConnectionStringBuilder parsing (which throws for URIs/non-standard formats)
			switch (providerType.ToUpperInvariant())
			{
				case "MONGODB" or "MONGO":
					return ValidateMongoDbConnectionString(connectionString);
				case "REDIS":
					return ValidateRedisConnectionString(connectionString);
				case "INMEMORY" or "MEMORY":
					return true;
			}

			// Use DbConnectionStringBuilder for standard ADO.NET providers
			var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

			return providerType.ToUpperInvariant() switch
			{
				"SQLSERVER" or "MSSQL" => ValidateSqlServerConnectionString(builder),
				"Postgres" or "POSTGRES" or "PGSQL" => ValidatePostgresConnectionString(builder),
				_ => builder.Count > 0, // Basic validation for unknown providers
			};
		}
		catch (Exception ex)
		{
			LogValidationFailed(ex, providerType);
			return false;
		}
	}

	/// <summary>
	/// Loads connection strings from configuration.
	/// </summary>
	private void LoadConnectionStrings()
	{
		var connectionStringsSection = _configuration.GetSection("ConnectionStrings");
		foreach (var child in connectionStringsSection.GetChildren())
		{
			var value = child.Value;
			if (!string.IsNullOrWhiteSpace(value))
			{
				_connectionStrings[child.Key] = value;
			}
		}

		LogLoadedConnectionStrings(_connectionStrings.Count);
	}

	/// <summary>
	/// Resolves a connection string from external sources.
	/// </summary>
	// R0.8: Async method lacks 'await' operators
#pragma warning disable CS1998
	// R0.8: Remove unused parameter - cancellationToken reserved for future Key Vault integration
#pragma warning disable IDE0060
	private async ValueTask<string?> ResolveFromExternalSourceAsync(string name, CancellationToken cancellationToken)
#pragma warning restore IDE0060, CS1998 // Async method lacks 'await' operators
	{
		// Check for environment variable override
		var envVarName = $"CONNECTIONSTRINGS__{name.ToUpperInvariant().Replace(":", "__", StringComparison.Ordinal)}";
		var envValue = Environment.GetEnvironmentVariable(envVarName);
		if (!string.IsNullOrWhiteSpace(envValue))
		{
			LogResolvedFromEnvironment(name);
			return envValue;
		}

		// Check for key vault reference
		var keyVaultKey = $"ConnectionStrings:{name}:KeyVault";
		var keyVaultRef = _configuration[keyVaultKey];
		if (!string.IsNullOrWhiteSpace(keyVaultRef))
		{
			// Secret store integration (Azure Key Vault, AWS Secrets Manager, etc.)
			// is provided by cloud-specific packages.
			LogReferencesSecretStore(name);
		}

		return null;
	}

	/// <summary>
	/// Loads connection strings from external sources.
	/// </summary>
	// R0.8: Remove unused parameter - cancellationToken reserved for future external secret store integration
#pragma warning disable IDE0060
	private ValueTask LoadFromExternalSourcesAsync(CancellationToken cancellationToken)
#pragma warning restore IDE0060
	{
		// This method would integrate with external secret stores For now, just log that it was called
		LogCheckingExternalSources();
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Validates a SQL Server connection string.
	/// </summary>
	private static bool ValidateSqlServerConnectionString(DbConnectionStringBuilder builder) =>

		// Check for required SQL Server parameters
		builder.ContainsKey("Data Source") || builder.ContainsKey("Server");

	/// <summary>
	/// Validates a Postgres connection string.
	/// </summary>
	private static bool ValidatePostgresConnectionString(DbConnectionStringBuilder builder) =>

		// Check for required Postgres parameters
		builder.ContainsKey("Host") || builder.ContainsKey("Server");

	/// <summary>
	/// Validates a MongoDB connection string.
	/// </summary>
	private static bool ValidateMongoDbConnectionString(string connectionString) =>

		// MongoDB connection strings typically start with mongodb:// or mongodb+srv://
		connectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase) ||
		connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Validates a Redis connection string.
	/// </summary>
	private static bool ValidateRedisConnectionString(string connectionString) =>

		// Redis connection strings can be simple (host:port) or complex
		!string.IsNullOrWhiteSpace(connectionString) &&
		(connectionString.Contains(':', StringComparison.Ordinal) || connectionString.Contains(',', StringComparison.Ordinal));

	/// <inheritdoc/>
	public void Dispose() => _refreshLock?.Dispose();
}
