// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Security-enhanced base class for Elasticsearch repositories with comprehensive data protection, access controls, audit logging, and
/// threat detection capabilities.
/// </summary>
/// <typeparam name="TEntity"> The type of entity managed by this repository. </typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="SecureElasticRepositoryBase{TEntity}" /> class.
/// </remarks>
/// <param name="client"> The Elasticsearch client for data operations. </param>
/// <param name="fieldEncryptor"> The field encryption service for sensitive data protection. </param>
/// <param name="securityAuditor"> The security auditing service for compliance and monitoring. </param>
/// <param name="securityMonitor"> The security monitoring service for threat detection. </param>
/// <param name="securityOptions"> The security configuration options. </param>
/// <param name="logger"> The logger for security and operational events. </param>
/// <exception cref="ArgumentNullException"> Thrown when required dependencies are null. </exception>
public abstract class SecureElasticRepositoryBase<TEntity>(
	ElasticsearchClient client,
	IElasticsearchFieldEncryptor fieldEncryptor,
	IElasticsearchSecurityAuditor securityAuditor,
	IElasticsearchSecurityMonitor securityMonitor,
	IOptions<ElasticsearchSecurityOptions> securityOptions,
	ILogger<SecureElasticRepositoryBase<TEntity>> logger) : ElasticRepositoryBase<TEntity>(client, typeof(TEntity).Name.ToLowerInvariant())
	where TEntity : class
{
	private readonly IElasticsearchFieldEncryptor _fieldEncryptor =
		fieldEncryptor ?? throw new ArgumentNullException(nameof(fieldEncryptor));

	private readonly IElasticsearchSecurityAuditor _securityAuditor =
		securityAuditor ?? throw new ArgumentNullException(nameof(securityAuditor));

	private readonly IElasticsearchSecurityMonitor _securityMonitor =
		securityMonitor ?? throw new ArgumentNullException(nameof(securityMonitor));

	private readonly ElasticsearchSecurityOptions _securitySettings =
		securityOptions?.Value ?? throw new ArgumentNullException(nameof(securityOptions));

	private readonly ILogger<SecureElasticRepositoryBase<TEntity>> _securityLogger =
		logger ?? throw new ArgumentNullException(nameof(logger));

	private readonly ElasticsearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly string _indexName = typeof(TEntity).Name.ToLowerInvariant();

	/// <summary>
	/// Securely indexes a document with field-level encryption and comprehensive audit logging.
	/// </summary>
	/// <param name="document"> The document to index with security processing. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous indexing operation with security enhancements applied. </returns>
	/// <exception cref="SecurityException"> Thrown when security validation fails or access is denied. </exception>
	public async Task<IndexResponse> SecureIndexAsync(TEntity document, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(document);

		var userId = GetCurrentUserId();
		var sourceIpAddress = GetCurrentSourceIpAddress();

		try
		{
			// Pre-operation security checks
			await ValidateSecurityContextAsync("Index", userId, sourceIpAddress, cancellationToken).ConfigureAwait(false);

			// Monitor security event
			await _securityMonitor.MonitorSecurityEventAsync(
				new SecurityMonitoringEvent
				{
					EventType = "DataWrite",
					Timestamp = DateTimeOffset.UtcNow,
					Details = $"User {userId} attempting write operation",
				}, cancellationToken).ConfigureAwait(false);

			// Apply field-level encryption
			object processedDocument = document;
			if (_securitySettings.Encryption.FieldLevelEncryption)
			{
				processedDocument = await _fieldEncryptor.EncryptDocumentAsync(document, cancellationToken).ConfigureAwait(false);
				_securityLogger.LogDebug("Document encrypted before indexing for user {UserId}", userId);
			}

			// Perform the indexing operation
			var indexRequest = new IndexRequest<TEntity>(_indexName) { Document = (TEntity)processedDocument };
			var response = await _client.IndexAsync(indexRequest, cancellationToken)
				.ConfigureAwait(false);

			// Audit the security activity
			await _securityAuditor.AuditSecurityActivityAsync(
				new SecurityActivityEvent { ActivityType = "DataCreate", UserId = userId ?? "Unknown", Timestamp = DateTimeOffset.UtcNow },
				cancellationToken).ConfigureAwait(false);

			_securityLogger.LogInformation(
				"Secure indexing completed for user {UserId} on index {IndexName}",
				userId, _indexName);

			return response;
		}
		catch (Exception ex)
		{
			_securityLogger.LogError(ex, "Secure indexing failed for user {UserId} on index {IndexName}", userId, _indexName);

			// Audit security failure
			await AuditSecurityFailureAsync("Index", userId, sourceIpAddress, ex, cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	/// <summary>
	/// Securely retrieves a document by ID with decryption and access logging.
	/// </summary>
	/// <param name="id"> The document identifier to retrieve. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> The decrypted document if found and accessible, null otherwise. </returns>
	/// <exception cref="SecurityException"> Thrown when security validation fails or access is denied. </exception>
	/// <exception cref="ArgumentException"></exception>
	public async Task<TEntity?> SecureGetByIdAsync(string id, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(id))
		{
			throw new ArgumentException("Document ID cannot be null or empty", nameof(id));
		}

		var userId = GetCurrentUserId();
		var sourceIpAddress = GetCurrentSourceIpAddress();

		try
		{
			// Pre-operation security checks
			await ValidateSecurityContextAsync("GetById", userId, sourceIpAddress, cancellationToken).ConfigureAwait(false);

			// Monitor security event
			await _securityMonitor.MonitorSecurityEventAsync(
				new SecurityMonitoringEvent
				{
					EventType = "DataRead",
					Timestamp = DateTimeOffset.UtcNow,
					Details = $"User {userId} attempting read operation",
				}, cancellationToken).ConfigureAwait(false);

			// Perform the retrieval operation
			var document = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

			if (document == null)
			{
				// Audit the access attempt
				await _securityAuditor.AuditSecurityActivityAsync(
					new SecurityActivityEvent
					{
						ActivityType = "DataRead",
						UserId = userId ?? "Unknown",
						Timestamp = DateTimeOffset.UtcNow,
					}, cancellationToken).ConfigureAwait(false);

				return null;
			}

			// Apply field-level decryption if needed
			var processedDocument = document;
			if (_securitySettings.Encryption.FieldLevelEncryption)
			{
				var decryptedDocument = await _fieldEncryptor.DecryptDocumentAsync(document, cancellationToken).ConfigureAwait(false);
				processedDocument = (TEntity)decryptedDocument;
				_securityLogger.LogDebug("Document decrypted after retrieval for user {UserId}", userId);
			}

			// Audit the successful access
			await _securityAuditor.AuditSecurityActivityAsync(
				new SecurityActivityEvent { ActivityType = "DataRead", UserId = userId ?? "Unknown", Timestamp = DateTimeOffset.UtcNow },
				cancellationToken).ConfigureAwait(false);

			_securityLogger.LogDebug(
				"Secure retrieval completed for user {UserId} on document {DocumentId}",
				userId, id);

			return processedDocument;
		}
		catch (Exception ex)
		{
			_securityLogger.LogError(ex, "Secure retrieval failed for user {UserId} on document {DocumentId}", userId, id);

			// Audit security failure
			await AuditSecurityFailureAsync("GetById", userId, sourceIpAddress, ex, cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	/// <summary>
	/// Securely updates a document with encryption and comprehensive audit logging.
	/// </summary>
	/// <param name="id"> The document identifier to update. </param>
	/// <param name="document"> The updated document data. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous update operation with security enhancements applied. </returns>
	/// <exception cref="SecurityException"> Thrown when security validation fails or access is denied. </exception>
	/// <exception cref="ArgumentException"></exception>
	public async Task<UpdateResponse<TEntity>> SecureUpdateAsync(
		string id,
		TEntity document,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(id))
		{
			throw new ArgumentException("Document ID cannot be null or empty", nameof(id));
		}

		ArgumentNullException.ThrowIfNull(document);

		var userId = GetCurrentUserId();
		var sourceIpAddress = GetCurrentSourceIpAddress();

		try
		{
			// Pre-operation security checks
			await ValidateSecurityContextAsync("Update", userId, sourceIpAddress, cancellationToken).ConfigureAwait(false);

			// Monitor security event
			await _securityMonitor.MonitorSecurityEventAsync(
				new SecurityMonitoringEvent
				{
					EventType = "DataWrite",
					Timestamp = DateTimeOffset.UtcNow,
					Details = $"User {userId} attempting write operation",
				}, cancellationToken).ConfigureAwait(false);

			// Apply field-level encryption
			object processedDocument = document;
			if (_securitySettings.Encryption.FieldLevelEncryption)
			{
				processedDocument = await _fieldEncryptor.EncryptDocumentAsync(document, cancellationToken).ConfigureAwait(false);
				_securityLogger.LogDebug("Document encrypted before updating for user {UserId}", userId);
			}

			// Perform the update operation using the client directly
			var updateRequest = new UpdateRequest<TEntity, object>(_indexName, id) { Doc = processedDocument, Refresh = Refresh.True };
			var response = await _client.UpdateAsync(updateRequest, cancellationToken).ConfigureAwait(false);

			// Audit the security activity
			await _securityAuditor.AuditSecurityActivityAsync(
				new SecurityActivityEvent { ActivityType = "DataUpdate", UserId = userId ?? "Unknown", Timestamp = DateTimeOffset.UtcNow },
				cancellationToken).ConfigureAwait(false);

			_securityLogger.LogInformation(
				"Secure update completed for user {UserId} on document {DocumentId}",
				userId, id);

			return response;
		}
		catch (Exception ex)
		{
			_securityLogger.LogError(ex, "Secure update failed for user {UserId} on document {DocumentId}", userId, id);

			// Audit security failure
			await AuditSecurityFailureAsync("Update", userId, sourceIpAddress, ex, cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	/// <summary>
	/// Securely deletes a document with comprehensive audit logging.
	/// </summary>
	/// <param name="id"> The document identifier to delete. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous deletion operation with security enhancements applied. </returns>
	/// <exception cref="SecurityException"> Thrown when security validation fails or access is denied. </exception>
	/// <exception cref="ArgumentException"></exception>
	public async Task<DeleteResponse> SecureDeleteAsync(string id, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(id))
		{
			throw new ArgumentException("Document ID cannot be null or empty", nameof(id));
		}

		var userId = GetCurrentUserId();
		var sourceIpAddress = GetCurrentSourceIpAddress();

		try
		{
			// Pre-operation security checks
			await ValidateSecurityContextAsync("Delete", userId, sourceIpAddress, cancellationToken).ConfigureAwait(false);

			// Monitor security event
			await _securityMonitor.MonitorSecurityEventAsync(
				new SecurityMonitoringEvent
				{
					EventType = "DataDelete",
					Timestamp = DateTimeOffset.UtcNow,
					Details = $"User {userId} attempting delete operation",
				}, cancellationToken).ConfigureAwait(false);

			// Perform the deletion operation using the client directly
			var deleteRequest = new DeleteRequest(_indexName, id);
			var response = await _client.DeleteAsync(deleteRequest, cancellationToken).ConfigureAwait(false);

			// Audit the security activity
			await _securityAuditor.AuditSecurityActivityAsync(
				new SecurityActivityEvent { ActivityType = "DataDelete", UserId = userId ?? "Unknown", Timestamp = DateTimeOffset.UtcNow },
				cancellationToken).ConfigureAwait(false);

			_securityLogger.LogInformation(
				"Secure deletion completed for user {UserId} on document {DocumentId}",
				userId, id);

			return response;
		}
		catch (Exception ex)
		{
			_securityLogger.LogError(ex, "Secure deletion failed for user {UserId} on document {DocumentId}", userId, id);

			// Audit security failure
			await AuditSecurityFailureAsync("Delete", userId, sourceIpAddress, ex, cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	/// <summary>
	/// Performs a secure search operation with result decryption and access logging.
	/// </summary>
	/// <param name="searchRequest"> The search request configuration. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> The search results with decrypted documents if encryption is enabled. </returns>
	/// <exception cref="SecurityException"> Thrown when security validation fails or access is denied. </exception>
	public async Task<SearchResponse<TEntity>> SecureSearchAsync(
		Action<SearchRequestDescriptor<TEntity>> searchRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(searchRequest);

		var userId = GetCurrentUserId();
		var sourceIpAddress = GetCurrentSourceIpAddress();

		try
		{
			// Pre-operation security checks
			await ValidateSecurityContextAsync("Search", userId, sourceIpAddress, cancellationToken).ConfigureAwait(false);

			// Monitor security event
			await _securityMonitor.MonitorSecurityEventAsync(
				new SecurityMonitoringEvent
				{
					EventType = "DataRead",
					Timestamp = DateTimeOffset.UtcNow,
					Details = $"User {userId} attempting read operation",
				}, cancellationToken).ConfigureAwait(false);

			// Perform the search operation using the client directly
			var response = await _client.SearchAsync(searchRequest, cancellationToken).ConfigureAwait(false);

			// Decrypt results if encryption is enabled
			if (_securitySettings.Encryption.FieldLevelEncryption && response.IsValidResponse)
			{
				var decryptedDocuments = new List<TEntity>();
				foreach (var hit in response.Hits)
				{
					if (hit.Source != null)
					{
						var decryptedDocument =
							await _fieldEncryptor.DecryptDocumentAsync(hit.Source, cancellationToken).ConfigureAwait(false);
						decryptedDocuments.Add((TEntity)decryptedDocument);
					}
				}

				_securityLogger.LogDebug(
					"Search results decrypted for user {UserId}, {DocumentCount} documents processed",
					userId, decryptedDocuments.Count);
			}

			// Audit the search operation
			await _securityAuditor.AuditSecurityActivityAsync(
				new SecurityActivityEvent { ActivityType = "DataSearch", UserId = userId ?? "Unknown", Timestamp = DateTimeOffset.UtcNow },
				cancellationToken).ConfigureAwait(false);

			_securityLogger.LogDebug(
				"Secure search completed for user {UserId}, returned {HitCount} results",
				userId, response.Hits.Count);

			return response;
		}
		catch (Exception ex)
		{
			_securityLogger.LogError(ex, "Secure search failed for user {UserId}", userId);

			// Audit security failure
			await AuditSecurityFailureAsync("Search", userId, sourceIpAddress, ex, cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	/// <summary>
	/// Gets the current user identifier from the security context.
	/// </summary>
	private static string? GetCurrentUserId() =>

		// This would typically retrieve the user ID from the current thread's security context For demonstration, returning a placeholder
		Environment.UserName;

	/// <summary>
	/// Gets the current source IP address from the request context.
	/// </summary>
	private static string? GetCurrentSourceIpAddress() =>

		// This would typically retrieve the source IP from the current HTTP context For demonstration, returning localhost
		"127.0.0.1";

	/// <summary>
	/// Checks if an IP address falls within a specified range or matches exactly.
	/// </summary>
	private static bool IsIpInRange(string ipAddress, string rangeOrIp)
	{
		// Simple implementation - would need proper CIDR range checking in production
		if (rangeOrIp.Contains('/', StringComparison.Ordinal))
		{
			// CIDR notation - implement proper subnet checking
			return false; // Placeholder
		}

		return ipAddress.Equals(rangeOrIp, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Validates the current security context for the requested operation.
	/// </summary>
	/// <exception cref="SecurityException"></exception>
	private async Task ValidateSecurityContextAsync(
		string operation,
		string? userId,
		string? sourceIpAddress,
		CancellationToken cancellationToken)
	{
		// Check if security is enabled
		if (!_securitySettings.Enabled)
		{
			return;
		}

		// Validate user context
		if (string.IsNullOrEmpty(userId))
		{
			throw new SecurityException("User context is required for secure operations");
		}

		// Network security validation
		if (_securitySettings.NetworkSecurity.Enabled && !string.IsNullOrEmpty(sourceIpAddress))
		{
			await ValidateNetworkAccessAsync(sourceIpAddress, cancellationToken).ConfigureAwait(false);
		}

		// Monitor for threats
		await _securityMonitor.MonitorSecurityEventAsync(
			new SecurityMonitoringEvent
			{
				EventType = "AccessAttempt",
				Timestamp = DateTimeOffset.UtcNow,
				Details = $"Operation: {operation}, Index: {_indexName}, User: {userId}",
			}, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Validates network access based on security policies.
	/// </summary>
	/// <exception cref="SecurityException"></exception>
	private Task ValidateNetworkAccessAsync(string sourceIpAddress, CancellationToken cancellationToken)
	{
		var networkSettings = _securitySettings.NetworkSecurity;

		// Check IP blacklist
		if (networkSettings.IpBlacklist.Exists(blocked => IsIpInRange(sourceIpAddress, blocked)))
		{
			throw new SecurityException($"Access denied: IP address {sourceIpAddress} is blacklisted");
		}

		// Check IP whitelist if configured
		if (networkSettings.IpWhitelist.Count != 0 &&
			!networkSettings.IpWhitelist.Exists(allowed => IsIpInRange(sourceIpAddress, allowed)))
		{
			throw new SecurityException($"Access denied: IP address {sourceIpAddress} is not whitelisted");
		}

		_securityLogger.LogDebug("Network access validation passed for IP {SourceIpAddress}", sourceIpAddress);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Audits security failures for compliance and monitoring.
	/// </summary>
	private async Task AuditSecurityFailureAsync(
		string operation,
		string? userId,
		string? sourceIpAddress,
		Exception exception,
		CancellationToken cancellationToken)
	{
		try
		{
			await _securityAuditor.AuditSecurityActivityAsync(
				new SecurityActivityEvent
				{
					ActivityType = $"Failed{operation}",
					UserId = userId ?? "Unknown",
					Timestamp = DateTimeOffset.UtcNow,
				}, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception auditEx)
		{
			_securityLogger.LogError(auditEx, "Failed to audit security failure for operation {Operation}", operation);
		}
	}
}
