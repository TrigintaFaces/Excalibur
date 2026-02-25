// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Confluent.SchemaRegistry;

using Microsoft.Extensions.Logging;

using ConfluentSchemaRegistryException = Confluent.SchemaRegistry.SchemaRegistryException;
using IConfluentSchemaRegistryClient = Confluent.SchemaRegistry.ISchemaRegistryClient;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Confluent Schema Registry client implementation wrapping the Confluent SDK.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps the Confluent.SchemaRegistry SDK to provide a consistent
/// interface for schema registry operations. It should be registered as a singleton.
/// </para>
/// </remarks>
public sealed partial class ConfluentSchemaRegistryClient : ISchemaRegistryClient, IDisposable
{
	private readonly IConfluentSchemaRegistryClient _innerClient;
	private readonly ILogger<ConfluentSchemaRegistryClient> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentSchemaRegistryClient"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public ConfluentSchemaRegistryClient(
		ConfluentSchemaRegistryOptions options,
		ILogger<ConfluentSchemaRegistryClient> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_logger = logger;

		var config = new SchemaRegistryConfig
		{
			Url = options.Url,
			MaxCachedSchemas = options.MaxCachedSchemas,
			RequestTimeoutMs = (int)options.RequestTimeout.TotalMilliseconds,
			EnableSslCertificateVerification = options.EnableSslCertificateVerification
		};

		if (!string.IsNullOrEmpty(options.BasicAuthUserInfo))
		{
			config.BasicAuthUserInfo = options.BasicAuthUserInfo;
		}

		if (!string.IsNullOrEmpty(options.SslCaLocation))
		{
			config.SslCaLocation = options.SslCaLocation;
		}

		_innerClient = new CachedSchemaRegistryClient(config);

		LogClientCreated(options.Url);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentSchemaRegistryClient"/> class
	/// with an existing Confluent client.
	/// </summary>
	/// <param name="innerClient">The inner Confluent client.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// This constructor is primarily for testing purposes.
	/// </remarks>
	internal ConfluentSchemaRegistryClient(
		IConfluentSchemaRegistryClient innerClient,
		ILogger<ConfluentSchemaRegistryClient> logger)
	{
		_innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<int> GetSchemaIdAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		ObjectDisposedException.ThrowIf(_disposed, this);

		try
		{
			LogGettingSchemaId(subject);
			var schemaId = await _innerClient.GetSchemaIdAsync(subject, schema).ConfigureAwait(false);
			LogSchemaIdRetrieved(subject, schemaId);
			return schemaId;
		}
		catch (ConfluentSchemaRegistryException ex)
		{
			LogSchemaRegistryError(subject, ex.Message, ex);
			throw new SchemaRegistryException($"Failed to get schema ID for subject '{subject}'", ex)
			{
				Subject = subject,
				ErrorCode = ex.ErrorCode
			};
		}
	}

	/// <inheritdoc/>
	public async Task<string> GetSchemaByIdAsync(
		int schemaId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		try
		{
			LogGettingSchemaById(schemaId);
			var schema = await _innerClient.GetSchemaAsync(schemaId).ConfigureAwait(false);
			LogSchemaRetrieved(schemaId);
			return schema.SchemaString;
		}
		catch (ConfluentSchemaRegistryException ex)
		{
			LogSchemaRetrievalError(schemaId, ex.Message, ex);
			throw new SchemaRegistryException($"Failed to get schema for ID {schemaId}", ex)
			{
				SchemaId = schemaId,
				ErrorCode = ex.ErrorCode
			};
		}
	}

	/// <inheritdoc/>
	public async Task<int> RegisterSchemaAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		ObjectDisposedException.ThrowIf(_disposed, this);

		try
		{
			LogRegisteringSchema(subject);
			var schemaId = await _innerClient.RegisterSchemaAsync(subject, schema).ConfigureAwait(false);
			LogSchemaRegistered(subject, schemaId);
			return schemaId;
		}
		catch (ConfluentSchemaRegistryException ex)
		{
			LogSchemaRegistrationError(subject, ex.Message, ex);
			throw new SchemaRegistryException($"Failed to register schema for subject '{subject}'", ex)
			{
				Subject = subject,
				ErrorCode = ex.ErrorCode
			};
		}
	}

	/// <inheritdoc/>
	public async Task<bool> IsCompatibleAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		ObjectDisposedException.ThrowIf(_disposed, this);

		try
		{
			LogCheckingCompatibility(subject);
			var isCompatible = await _innerClient.IsCompatibleAsync(subject, schema).ConfigureAwait(false);
			LogCompatibilityResult(subject, isCompatible);
			return isCompatible;
		}
		catch (ConfluentSchemaRegistryException ex)
		{
			LogCompatibilityCheckError(subject, ex.Message, ex);
			throw new SchemaRegistryException($"Failed to check compatibility for subject '{subject}'", ex)
			{
				Subject = subject,
				ErrorCode = ex.ErrorCode
			};
		}
	}

	/// <summary>
	/// Disposes the client and releases resources.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_innerClient is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	// Source-generated logging methods
	[LoggerMessage(KafkaEventId.SchemaRegistryClientCreated, LogLevel.Information,
		"Confluent Schema Registry client created for {Url}")]
	private partial void LogClientCreated(string url);

	[LoggerMessage(KafkaEventId.GettingSchemaId, LogLevel.Debug,
		"Getting schema ID for subject {Subject}")]
	private partial void LogGettingSchemaId(string subject);

	[LoggerMessage(KafkaEventId.SchemaIdRetrieved, LogLevel.Debug,
		"Retrieved schema ID {SchemaId} for subject {Subject}")]
	private partial void LogSchemaIdRetrieved(string subject, int schemaId);

	[LoggerMessage(KafkaEventId.SchemaRegistryError, LogLevel.Error,
		"Schema registry error for subject {Subject}: {Message}")]
	private partial void LogSchemaRegistryError(string subject, string message, Exception ex);

	[LoggerMessage(KafkaEventId.GettingSchemaById, LogLevel.Debug,
		"Getting schema for ID {SchemaId}")]
	private partial void LogGettingSchemaById(int schemaId);

	[LoggerMessage(KafkaEventId.SchemaRetrieved, LogLevel.Debug,
		"Retrieved schema for ID {SchemaId}")]
	private partial void LogSchemaRetrieved(int schemaId);

	[LoggerMessage(KafkaEventId.SchemaRetrievalError, LogLevel.Error,
		"Failed to retrieve schema for ID {SchemaId}: {Message}")]
	private partial void LogSchemaRetrievalError(int schemaId, string message, Exception ex);

	[LoggerMessage(KafkaEventId.RegisteringSchema, LogLevel.Debug,
		"Registering schema for subject {Subject}")]
	private partial void LogRegisteringSchema(string subject);

	[LoggerMessage(KafkaEventId.SchemaRegistered, LogLevel.Information,
		"Registered schema for subject {Subject} with ID {SchemaId}")]
	private partial void LogSchemaRegistered(string subject, int schemaId);

	[LoggerMessage(KafkaEventId.SchemaRegistrationError, LogLevel.Error,
		"Failed to register schema for subject {Subject}: {Message}")]
	private partial void LogSchemaRegistrationError(string subject, string message, Exception ex);

	[LoggerMessage(KafkaEventId.CheckingCompatibility, LogLevel.Debug,
		"Checking schema compatibility for subject {Subject}")]
	private partial void LogCheckingCompatibility(string subject);

	[LoggerMessage(KafkaEventId.CompatibilityResult, LogLevel.Debug,
		"Compatibility check for subject {Subject}: {IsCompatible}")]
	private partial void LogCompatibilityResult(string subject, bool isCompatible);

	[LoggerMessage(KafkaEventId.CompatibilityCheckError, LogLevel.Error,
		"Failed to check compatibility for subject {Subject}: {Message}")]
	private partial void LogCompatibilityCheckError(string subject, string message, Exception ex);
}
