// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Configuration options for the SQL Server persistence provider.
/// </summary>
/// <remarks>
/// Root options contain <see cref="IPersistenceOptions"/> interface properties plus sub-option groups
/// for SQL Server-specific connection, security, and resiliency settings.
/// Follows the Microsoft pattern of grouping related options into sub-option classes
/// (e.g., <c>ServiceBusClientOptions</c>).
/// </remarks>
public sealed class SqlServerPersistenceOptions : IPersistenceOptions, IPersistenceResilienceOptions, IPersistencePoolingOptions, IPersistenceObservabilityOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerPersistenceOptions" /> class.
	/// </summary>
	/// <inheritdoc />
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <inheritdoc />
	[Range(1, 300)]
	public int ConnectionTimeout { get; set; } = 30;

	/// <inheritdoc />
	[Range(1, 3600)]
	public int CommandTimeout { get; set; } = 30;

	/// <inheritdoc />
	[Range(0, 10)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <inheritdoc />
	[Range(0, 60000)]
	public int RetryDelayMilliseconds { get; set; } = 1000;

	/// <inheritdoc />
	public bool EnableConnectionPooling { get; set; } = true;

	/// <inheritdoc />
	[Range(1, 1000)]
	public int MaxPoolSize { get; set; } = 100;

	/// <inheritdoc />
	[Range(0, 100)]
	public int MinPoolSize { get; set; } = 10;

	/// <inheritdoc />
	public bool EnableDetailedLogging { get; set; }

	/// <inheritdoc />
	public bool EnableMetrics { get; set; } = true;

	/// <inheritdoc />
	public IDictionary<string, object> ProviderSpecificOptions { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets the SQL Server connection options for application identity, protocol, and transport settings.
	/// </summary>
	/// <value>
	/// The connection options. This property is initialized by default and cannot be replaced.
	/// </value>
	public SqlServerConnectionOptions Connection { get; } = new();

	/// <summary>
	/// Gets the SQL Server security options for encryption, authentication, and Always Encrypted settings.
	/// </summary>
	/// <value>
	/// The security options. This property is initialized by default and cannot be replaced.
	/// </value>
	public SqlServerSecurityOptions Security { get; } = new();

	/// <summary>
	/// Gets the SQL Server resiliency options for connection retry and resilience settings.
	/// </summary>
	/// <value>
	/// The resiliency options. This property is initialized by default and cannot be replaced.
	/// </value>
	public SqlServerResiliencyOptions Resiliency { get; } = new();

	/// <inheritdoc />
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("Connection string required for SQL Server.");
		}

		if (ConnectionTimeout <= 0)
		{
			throw new InvalidOperationException("Connection timeout must be greater than zero.");
		}

		if (CommandTimeout <= 0)
		{
			throw new InvalidOperationException("Command timeout must be greater than zero.");
		}

		if (MaxRetryAttempts < 0)
		{
			throw new InvalidOperationException("Max retry attempts cannot be negative.");
		}

		if (RetryDelayMilliseconds < 0)
		{
			throw new InvalidOperationException("Retry delay milliseconds cannot be negative.");
		}

		if (EnableConnectionPooling)
		{
			if (MaxPoolSize <= 0)
			{
				throw new InvalidOperationException("Max pool size must be greater than zero.");
			}

			if (MinPoolSize < 0)
			{
				throw new InvalidOperationException("Min pool size cannot be negative.");
			}

			if (MinPoolSize > MaxPoolSize)
			{
				throw new InvalidOperationException("Min pool size cannot be greater than max pool size.");
			}
		}

		Connection.Validate();
		Resiliency.Validate();
	}
}

/// <summary>
/// SQL Server connection options for application identity, protocol, and transport settings.
/// </summary>
public sealed class SqlServerConnectionOptions
{
	/// <summary>
	/// Gets or sets the application name to be associated with the connection.
	/// </summary>
	/// <value>
	/// The application name to be associated with the connection.
	/// </value>
	public string ApplicationName { get; set; } = "Excalibur";

	/// <summary>
	/// Gets or sets the default database name for the connection.
	/// </summary>
	/// <value>
	/// The default database name for the connection.
	/// </value>
	public string? DefaultDatabase { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether multi-subnet failover is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if multi-subnet failover is enabled; otherwise, <c>false</c>.
	/// </value>
	public bool MultiSubnetFailover { get; set; }

	/// <summary>
	/// Gets or sets the workstation ID for the connection.
	/// </summary>
	/// <value>
	/// The workstation ID for the connection.
	/// </value>
	public string? WorkstationId { get; set; }

	/// <summary>
	/// Gets or sets the packet size for the connection.
	/// </summary>
	/// <value>
	/// The packet size for the connection.
	/// </value>
	[Range(512, 32768)]
	public int PacketSize { get; set; } = 8192;

	/// <summary>
	/// Gets or sets a value indicating whether to enable Multiple Active Result Sets (MARS).
	/// </summary>
	/// <value>
	/// <see langword="true"/> if Multiple Active Result Sets (MARS) is enabled; otherwise, <c>false</c>.
	/// </value>
	public bool EnableMars { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether transparent network IP resolution is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if transparent network IP resolution is enabled; otherwise, <c>false</c>.
	/// </value>
	public bool EnableTransparentNetworkIPResolution { get; set; } = true;

	/// <summary>
	/// Gets or sets the application intent for the connection (ReadWrite or ReadOnly).
	/// </summary>
	/// <value>
	/// The application intent for the connection (ReadWrite or ReadOnly).
	/// </value>
	public ApplicationIntent ApplicationIntent { get; set; } = ApplicationIntent.ReadWrite;

	/// <summary>
	/// Gets or sets the load balance timeout in seconds.
	/// </summary>
	/// <value>
	/// The load balance timeout in seconds.
	/// </value>
	public int LoadBalanceTimeout { get; set; }

	/// <summary>
	/// Gets or sets the connection pool blocking period.
	/// </summary>
	/// <value>
	/// The connection pool blocking period.
	/// </value>
	public PoolBlockingPeriod PoolBlockingPeriod { get; set; } = PoolBlockingPeriod.Auto;

	/// <summary>
	/// Validates the connection options.
	/// </summary>
	internal void Validate()
	{
		if (PacketSize is < 512 or > 32768)
		{
			throw new InvalidOperationException("Packet size must be between 512 and 32768.");
		}
	}
}

/// <summary>
/// SQL Server security options for encryption, authentication, and Always Encrypted settings.
/// </summary>
public sealed class SqlServerSecurityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether Always Encrypted is enabled for the connection.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if Always Encrypted is enabled for the connection; otherwise, <c>false</c>.
	/// </value>
	public bool EnableAlwaysEncrypted { get; set; }

	/// <summary>
	/// Gets or sets the column encryption setting for Always Encrypted.
	/// </summary>
	/// <value>
	/// The column encryption setting for Always Encrypted.
	/// </value>
	public SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to trust the server certificate without validation.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the server certificate should be trusted without validation; otherwise, <c>false</c>.
	/// </value>
	public bool TrustServerCertificate { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to encrypt the connection.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the connection should be encrypted; otherwise, <c>false</c>.
	/// </value>
	public bool EncryptConnection { get; set; } = true;

	/// <summary>
	/// Gets or sets the enclave attestation URL for Always Encrypted with secure enclaves.
	/// </summary>
	/// <value>
	/// The enclave attestation URL for Always Encrypted with secure enclaves.
	/// </value>
	public string? EnclaveAttestationUrl { get; set; }

	/// <summary>
	/// Gets or sets the attestation protocol for Always Encrypted with secure enclaves.
	/// </summary>
	/// <value>
	/// The attestation protocol for Always Encrypted with secure enclaves.
	/// </value>
	public SqlAttestationProtocol AttestationProtocol { get; set; } = SqlAttestationProtocol.NotSpecified;

	/// <summary>
	/// Gets or sets the authentication method for the connection.
	/// </summary>
	/// <value>
	/// The authentication method for the connection.
	/// </value>
	public SqlAuthenticationMethod Authentication { get; set; } = SqlAuthenticationMethod.NotSpecified;
}

/// <summary>
/// SQL Server resiliency options for connection retry and resilience settings.
/// </summary>
public sealed class SqlServerResiliencyOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable connection resiliency.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if connection resiliency is enabled; otherwise, <c>false</c>.
	/// </value>
	public bool EnableConnectionResiliency { get; set; } = true;

	/// <summary>
	/// Gets or sets the connect retry count for connection resiliency.
	/// </summary>
	/// <value>
	/// The connect retry count for connection resiliency.
	/// </value>
	[Range(0, 255)]
	public int ConnectRetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets the connect retry interval in seconds.
	/// </summary>
	/// <value>
	/// The connect retry interval in seconds.
	/// </value>
	[Range(1, 60)]
	public int ConnectRetryInterval { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable statistics collection.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if statistics collection is enabled; otherwise, <c>false</c>.
	/// </value>
	public bool EnableStatisticsCollection { get; set; }

	/// <summary>
	/// Validates the resiliency options.
	/// </summary>
	internal void Validate()
	{
		if (ConnectRetryCount is < 0 or > 255)
		{
			throw new InvalidOperationException("Connect retry count must be between 0 and 255.");
		}

		if (ConnectRetryInterval is < 1 or > 60)
		{
			throw new InvalidOperationException("Connect retry interval must be between 1 and 60 seconds.");
		}
	}
}
