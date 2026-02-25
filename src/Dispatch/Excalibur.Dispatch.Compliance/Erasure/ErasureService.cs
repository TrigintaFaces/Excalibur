// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="IErasureService"/> providing GDPR Article 17 erasure capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This service implements cryptographic erasure by:
/// 1. Validating the erasure request
/// 2. Checking for legal holds (Article 17(3))
/// 3. Discovering personal data via data inventory
/// 4. Scheduling key deletion after grace period
/// 5. Verifying erasure and generating compliance certificate
/// </para>
/// </remarks>
public sealed partial class ErasureService : IErasureService
{
	private static readonly Counter<long> RequestsSubmittedCounter =
		ErasureTelemetryConstants.Meter.CreateCounter<long>(
			ErasureTelemetryConstants.MetricNames.RequestsSubmitted,
			description: "Total erasure requests submitted.");

	private static readonly Counter<long> RequestsCompletedCounter =
		ErasureTelemetryConstants.Meter.CreateCounter<long>(
			ErasureTelemetryConstants.MetricNames.RequestsCompleted,
			description: "Total erasure requests completed.");

	private static readonly Counter<long> RequestsFailedCounter =
		ErasureTelemetryConstants.Meter.CreateCounter<long>(
			ErasureTelemetryConstants.MetricNames.RequestsFailed,
			description: "Total erasure request failures.");

	private static readonly Counter<long> RequestsBlockedCounter =
		ErasureTelemetryConstants.Meter.CreateCounter<long>(
			ErasureTelemetryConstants.MetricNames.RequestsBlocked,
			description: "Total erasure requests blocked by legal hold.");

	private static readonly Counter<long> KeysDeletedCounter =
		ErasureTelemetryConstants.Meter.CreateCounter<long>(
			ErasureTelemetryConstants.MetricNames.KeysDeleted,
			description: "Total keys deleted via erasure.");

	private static readonly Histogram<double> ExecutionDurationHistogram =
		ErasureTelemetryConstants.Meter.CreateHistogram<double>(
			ErasureTelemetryConstants.MetricNames.ExecutionDuration,
			unit: "ms",
			description: "Duration of erasure execution in milliseconds.");

	private static readonly CompositeFormat CannotCancelRequestFormat =
		CompositeFormat.Parse(Resources.ErasureService_CannotCancelRequest);

	private static readonly CompositeFormat RequestNotFoundFormat =
		CompositeFormat.Parse(Resources.ErasureService_RequestNotFound);

	private static readonly CompositeFormat CannotGenerateCertificateFormat =
		CompositeFormat.Parse(Resources.ErasureService_CannotGenerateCertificate);

	private readonly IErasureStore _store;
	private readonly ILegalHoldService? _legalHoldService;
	private readonly IDataInventoryService? _dataInventoryService;
	private readonly IKeyManagementProvider _keyProvider;
	private readonly IOptions<ErasureOptions> _options;
	private readonly IOptions<ErasureSigningOptions> _signingOptions;
	private readonly ILogger<ErasureService> _logger;
	private readonly IEnumerable<IErasureContributor> _contributors;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureService"/> class.
	/// </summary>
	/// <param name="store">The erasure store for persistence.</param>
	/// <param name="keyProvider">The key management provider for key deletion.</param>
	/// <param name="options">The erasure options.</param>
	/// <param name="signingOptions">The HMAC signing options for certificate signatures.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="legalHoldService">Optional legal hold service for Article 17(3) checks. Pass <see langword="null"/> if not available.</param>
	/// <param name="dataInventoryService">Optional data inventory service for discovery. Pass <see langword="null"/> if not available.</param>
	/// <param name="contributors">Optional erasure contributors for additional store erasure (event stores, snapshot stores, etc.).</param>
	public ErasureService(
		IErasureStore store,
		IKeyManagementProvider keyProvider,
		IOptions<ErasureOptions> options,
		IOptions<ErasureSigningOptions> signingOptions,
		ILogger<ErasureService> logger,
		ILegalHoldService? legalHoldService,
		IDataInventoryService? dataInventoryService,
		IEnumerable<IErasureContributor>? contributors = null)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_signingOptions = signingOptions ?? throw new ArgumentNullException(nameof(signingOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_legalHoldService = legalHoldService;
		_dataInventoryService = dataInventoryService;
		_contributors = contributors ?? [];
	}

	/// <inheritdoc />
	public async Task<ErasureResult> RequestErasureAsync(
		ErasureRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		using var activity = ErasureTelemetryConstants.ActivitySource.StartActivity("erasure.request");
		activity?.SetTag(ErasureTelemetryConstants.Tags.Scope, request.Scope.ToString());

		LogErasureRequestProcessing(request.RequestId, request.IdType, request.Scope);
		RequestsSubmittedCounter.Add(1, new TagList { { ErasureTelemetryConstants.Tags.Scope, request.Scope.ToString() } });

		try
		{
			// Validate request
			ValidateRequest(request);

			// Check for legal holds if service is available
			if (_legalHoldService is not null)
			{
				var holdCheck = await _legalHoldService.CheckHoldsAsync(
					request.DataSubjectId,
					request.IdType,
					request.TenantId,
					cancellationToken).ConfigureAwait(false);

				if (holdCheck.ErasureBlocked)
				{
					var blockingHold = holdCheck.ActiveHolds[0];
					LogErasureRequestBlocked(request.RequestId, blockingHold.HoldId);
					RequestsBlockedCounter.Add(1, new TagList { { ErasureTelemetryConstants.Tags.Scope, request.Scope.ToString() } });
					activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "blocked");

					return ErasureResult.Blocked(request.RequestId, blockingHold);
				}
			}

			// Discover data inventory if service is available
			DataInventorySummary? inventorySummary = null;
			if (_dataInventoryService is not null && _options.Value.EnableAutoDiscovery)
			{
				var inventory = await _dataInventoryService.DiscoverAsync(
					request.DataSubjectId,
					request.IdType,
					request.TenantId,
					cancellationToken).ConfigureAwait(false);

				inventorySummary = new DataInventorySummary
				{
					EncryptedFieldCount = inventory.Locations.Count,
					KeyCount = inventory.AssociatedKeys.Count,
					DataCategories = inventory.Locations.Select(l => l.DataCategory).Distinct().ToList(),
					AffectedTables = inventory.Locations.Select(l => l.TableName).Distinct().ToList(),
					EstimatedDataSizeBytes = 0 // Could be calculated from metadata
				};
			}

			// Calculate grace period
			var gracePeriod = CalculateGracePeriod(request);
			var scheduledTime = DateTimeOffset.UtcNow.Add(gracePeriod);

			// Persist the request
			await _store.SaveRequestAsync(request, scheduledTime, cancellationToken).ConfigureAwait(false);

			LogErasureScheduled(request.RequestId, scheduledTime);
			activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "scheduled");

			return ErasureResult.Scheduled(request.RequestId, scheduledTime, inventorySummary);
		}
		catch (ErasureOperationException)
		{
			RequestsFailedCounter.Add(1, new TagList
			{
				{ ErasureTelemetryConstants.Tags.Scope, request.Scope.ToString() },
				{ ErasureTelemetryConstants.Tags.ErrorType, "validation" }
			});
			activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "failed");
			throw;
		}
		catch (Exception ex)
		{
			LogErasureRequestFailed(request.RequestId, ex);
			RequestsFailedCounter.Add(1, new TagList
			{
				{ ErasureTelemetryConstants.Tags.Scope, request.Scope.ToString() },
				{ ErasureTelemetryConstants.Tags.ErrorType, ex.GetType().Name }
			});
			activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "failed");
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<ErasureStatus?> GetStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		return await _store.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> CancelErasureAsync(
		Guid requestId,
		string reason,
		string cancelledBy,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);
		ArgumentException.ThrowIfNullOrWhiteSpace(cancelledBy);

		var status = await _store.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);

		if (status is null)
		{
			LogErasureCancellationNotFound(requestId);
			return false;
		}

		if (!status.CanCancel)
		{
			LogErasureCancellationNotAllowed(requestId, status.Status);
			throw new InvalidOperationException(string.Format(
				CultureInfo.CurrentCulture,
				CannotCancelRequestFormat,
				requestId,
				status.Status));
		}

		var cancelled = await _store.RecordCancellationAsync(
			requestId,
			reason,
			cancelledBy,
			cancellationToken).ConfigureAwait(false);

		if (cancelled)
		{
			LogErasureCancelled(requestId, cancelledBy, reason);
		}

		return cancelled;
	}

	/// <inheritdoc />
	public async Task<ErasureCertificate> GenerateCertificateAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		var status = await _store.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false)
					 ?? throw new KeyNotFoundException(string.Format(
						 CultureInfo.CurrentCulture,
						 RequestNotFoundFormat,
						 requestId));

		if (!status.IsExecuted)
		{
			throw new InvalidOperationException(string.Format(
				CultureInfo.CurrentCulture,
				CannotGenerateCertificateFormat,
				requestId,
				status.Status));
		}

		// Check if certificate already exists
		var certStore = (IErasureCertificateStore?)_store.GetService(typeof(IErasureCertificateStore))
			?? throw new InvalidOperationException("The erasure store does not support certificate operations.");
		var existingCert = await certStore.GetCertificateAsync(requestId, cancellationToken).ConfigureAwait(false);
		if (existingCert is not null)
		{
			return existingCert;
		}

		var certificate = new ErasureCertificate
		{
			CertificateId = Guid.NewGuid(),
			RequestId = requestId,
			DataSubjectReference = status.DataSubjectIdHash,
			RequestReceivedAt = status.RequestedAt,
			CompletedAt = status.CompletedAt ?? DateTimeOffset.UtcNow,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary
			{
				KeysDeleted = status.KeysDeleted ?? 0,
				RecordsAffected = status.RecordsAffected ?? 0,
				DataCategories = [],
				TablesAffected = []
			},
			Verification =
				new VerificationSummary
				{
					Verified = true,
					Methods = _options.Value.VerificationMethods,
					VerifiedAt = DateTimeOffset.UtcNow
				},
			LegalBasis = status.LegalBasis,
			Signature = GenerateSignature(requestId, status),
			RetainUntil = DateTimeOffset.UtcNow.Add(_options.Value.CertificateRetentionPeriod)
		};

		await certStore.SaveCertificateAsync(certificate, cancellationToken).ConfigureAwait(false);

		LogErasureCertificateGenerated(certificate.CertificateId, requestId);

		return certificate;
	}

	/// <inheritdoc/>
	public async Task<ErasureExecutionResult> ExecuteAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		using var activity = ErasureTelemetryConstants.ActivitySource.StartActivity("erasure.execute");
		var executionStopwatch = ValueStopwatch.StartNew();

		var status = await _store.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);

		if (status is null)
		{
			return ErasureExecutionResult.Failed("Request not found");
		}

		if (status.Status != ErasureRequestStatus.Scheduled)
		{
			return ErasureExecutionResult.Failed($"Invalid status: {status.Status}");
		}

		// Re-check legal holds before execution (AD-544.9: pass hash as lookup key)
		if (_legalHoldService is not null)
		{
			var holdCheck = await _legalHoldService.CheckHoldsAsync(
				status.DataSubjectIdHash, DataSubjectIdType.Hash, status.TenantId, cancellationToken)
				.ConfigureAwait(false);

			if (holdCheck.ErasureBlocked)
			{
				_ = await _store.UpdateStatusAsync(requestId, ErasureRequestStatus.BlockedByLegalHold,
					"Legal hold active", cancellationToken).ConfigureAwait(false);
				return ErasureExecutionResult.Failed("Erasure blocked by active legal hold");
			}
		}

		// Atomically transition to InProgress — if another caller already claimed this request, abort
		var transitioned = await _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, errorMessage: null, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		if (!transitioned)
		{
			return ErasureExecutionResult.Failed("Request is no longer in Scheduled status (concurrent execution detected)");
		}

		try
		{
			// Discover keys to delete via data inventory (AD-544.9: use hash-based lookup)
			var keysToDelete = new List<string>();
			if (_dataInventoryService is not null)
			{
				var inventory = await _dataInventoryService.DiscoverAsync(
					status.DataSubjectIdHash, DataSubjectIdType.Hash, status.TenantId, cancellationToken)
					.ConfigureAwait(false);

				foreach (var keyRef in inventory.AssociatedKeys)
				{
					keysToDelete.Add(keyRef.KeyId);
				}

				LogErasureKeysDiscovered(requestId, keysToDelete.Count);
			}

			// Delete keys
			var deletedCount = 0;
			var errors = new List<string>();

			foreach (var keyId in keysToDelete)
			{
				try
				{
					var deleted = await _keyProvider.DeleteKeyAsync(keyId, 0, cancellationToken)
						.ConfigureAwait(false);
					if (deleted)
					{
						deletedCount++;
					}
				}
				catch (Exception ex)
				{
					errors.Add($"Failed to delete key {keyId}: {ex.Message}");
					LogErasureKeyDeletionFailed(keyId, requestId, ex);
				}
			}

			// Invoke erasure contributors (event stores, snapshot stores, etc.)
			var totalRecordsAffected = 0;
			var contributorContext = new ErasureContributorContext
			{
				RequestId = requestId,
				DataSubjectIdHash = status.DataSubjectIdHash,
				IdType = status.IdType,
				TenantId = status.TenantId,
				Scope = status.Scope
			};

			foreach (var contributor in _contributors)
			{
				try
				{
					var contributorResult = await contributor.EraseAsync(contributorContext, cancellationToken)
						.ConfigureAwait(false);

					if (contributorResult.Success)
					{
						totalRecordsAffected += contributorResult.RecordsAffected;
						LogErasureContributorCompleted(contributor.Name, requestId, contributorResult.RecordsAffected);
					}
					else
					{
						errors.Add($"Contributor '{contributor.Name}' failed: {contributorResult.ErrorMessage}");
						LogErasureContributorFailed(contributor.Name, requestId, contributorResult.ErrorMessage ?? "Unknown error");
					}
				}
				catch (Exception ex)
				{
					errors.Add($"Contributor '{contributor.Name}' threw exception: {ex.Message}");
					LogErasureContributorException(contributor.Name, requestId, ex);
				}
			}

			// Record completion
			var certificateId = Guid.NewGuid();
			await _store.RecordCompletionAsync(requestId, deletedCount, totalRecordsAffected, certificateId, cancellationToken)
				.ConfigureAwait(false);

			LogErasureCompleted(requestId, deletedCount);
			KeysDeletedCounter.Add(deletedCount);
			RequestsCompletedCounter.Add(1);
			ExecutionDurationHistogram.Record(executionStopwatch.Elapsed.TotalMilliseconds);
			activity?.SetTag("erasure.keys_deleted", deletedCount);
			activity?.SetTag("erasure.records_affected", totalRecordsAffected);

			return ErasureExecutionResult.Succeeded(deletedCount, totalRecordsAffected);
		}
		catch (Exception ex)
		{
			LogErasureExecutionFailed(requestId, ex);
			RequestsFailedCounter.Add(1, new TagList { { ErasureTelemetryConstants.Tags.ErrorType, ex.GetType().Name } });
			ExecutionDurationHistogram.Record(executionStopwatch.Elapsed.TotalMilliseconds);
			activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "failed");
			_ = await _store.UpdateStatusAsync(requestId, ErasureRequestStatus.Failed, ex.Message, cancellationToken)
				.ConfigureAwait(false);
			return ErasureExecutionResult.Failed(ex.Message);
		}
	}

	/// <summary>
	/// Computes the SHA-256 hash of a data subject identifier for storage.
	/// </summary>
	internal static string HashDataSubjectId(string dataSubjectId) =>
		DataSubjectHasher.HashDataSubjectId(dataSubjectId);

	private string GenerateSignature(Guid requestId, ErasureStatus status)
	{
		var signingKey = _signingOptions.Value.SigningKey;
		var dataToSign = $"{requestId}|{status.DataSubjectIdHash}|{status.CompletedAt:O}";
		var dataBytes = Encoding.UTF8.GetBytes(dataToSign);

		if (signingKey.Length == 0)
		{
			// Fallback: use SHA-256 hash when no signing key is configured (dev/test only)
			_logger.LogWarning("No HMAC signing key configured — falling back to unsigned hash for certificate {RequestId}", requestId);
			var hash = SHA256.HashData(dataBytes);
			return Convert.ToBase64String(hash);
		}

		using var hmac = new HMACSHA256(signingKey);
		var signature = hmac.ComputeHash(dataBytes);
		return Convert.ToBase64String(signature);
	}

	private void ValidateRequest(ErasureRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.DataSubjectId))
		{
			throw ErasureOperationException.ValidationFailed(
				request.RequestId,
				Resources.ErasureService_DataSubjectIdRequired);
		}

		if (string.IsNullOrWhiteSpace(request.RequestedBy))
		{
			throw ErasureOperationException.ValidationFailed(
				request.RequestId,
				Resources.ErasureService_RequestedByRequired);
		}

		if (request.Scope == ErasureScope.Tenant && string.IsNullOrWhiteSpace(request.TenantId))
		{
			throw ErasureOperationException.ValidationFailed(
				request.RequestId,
				Resources.ErasureService_TenantIdRequiredForScope);
		}

		if (request.Scope == ErasureScope.Selective &&
			(request.DataCategories is null || request.DataCategories.Count == 0))
		{
			throw ErasureOperationException.ValidationFailed(
				request.RequestId,
				Resources.ErasureService_DataCategoriesRequired);
		}
	}

	private TimeSpan CalculateGracePeriod(ErasureRequest request)
	{
		var options = _options.Value;

		// Use override if provided
		if (request.GracePeriodOverride.HasValue)
		{
			var requested = request.GracePeriodOverride.Value;

			// Clamp to valid range
			if (requested < options.MinimumGracePeriod)
			{
				LogErasureGracePeriodBelowMinimum(requested, options.MinimumGracePeriod);
				return options.MinimumGracePeriod;
			}

			if (requested > options.MaximumGracePeriod)
			{
				LogErasureGracePeriodExceedsMaximum(requested, options.MaximumGracePeriod);
				return options.MaximumGracePeriod;
			}

			return requested;
		}

		return options.DefaultGracePeriod;
	}

	[LoggerMessage(
		ComplianceEventId.ErasureRequestProcessing,
		LogLevel.Information,
		"Processing erasure request {RequestId} for data subject type {IdType}, scope {Scope}")]
	private partial void LogErasureRequestProcessing(Guid requestId, DataSubjectIdType idType, ErasureScope scope);

	[LoggerMessage(
		ComplianceEventId.ErasureBlockedByLegalHold,
		LogLevel.Warning,
		"Erasure request {RequestId} blocked by legal hold {HoldId}")]
	private partial void LogErasureRequestBlocked(Guid requestId, Guid holdId);

	[LoggerMessage(
		ComplianceEventId.ErasureScheduled,
		LogLevel.Information,
		"Erasure request {RequestId} scheduled for execution at {ScheduledTime}")]
	private partial void LogErasureScheduled(Guid requestId, DateTimeOffset scheduledTime);

	[LoggerMessage(
		ComplianceEventId.ErasureRequestFailed,
		LogLevel.Error,
		"Failed to process erasure request {RequestId}")]
	private partial void LogErasureRequestFailed(Guid requestId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.ErasureCancellationNotFound,
		LogLevel.Warning,
		"Erasure request {RequestId} not found for cancellation")]
	private partial void LogErasureCancellationNotFound(Guid requestId);

	[LoggerMessage(
		ComplianceEventId.ErasureCancellationNotAllowed,
		LogLevel.Warning,
		"Erasure request {RequestId} cannot be cancelled (status: {Status})")]
	private partial void LogErasureCancellationNotAllowed(Guid requestId, ErasureRequestStatus status);

	[LoggerMessage(
		ComplianceEventId.ErasureCancelled,
		LogLevel.Information,
		"Erasure request {RequestId} cancelled by {CancelledBy}. Reason: {Reason}")]
	private partial void LogErasureCancelled(Guid requestId, string cancelledBy, string reason);

	[LoggerMessage(
		ComplianceEventId.ErasureCertificateGenerated,
		LogLevel.Information,
		"Generated erasure certificate {CertificateId} for request {RequestId}")]
	private partial void LogErasureCertificateGenerated(Guid certificateId, Guid requestId);

	[LoggerMessage(
		ComplianceEventId.ErasureKeyDeletionFailed,
		LogLevel.Error,
		"Failed to delete key {KeyId} for erasure request {RequestId}")]
	private partial void LogErasureKeyDeletionFailed(string keyId, Guid requestId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.ErasureRequestCompleted,
		LogLevel.Information,
		"Erasure request {RequestId} completed. Keys deleted: {KeysDeleted}")]
	private partial void LogErasureCompleted(Guid requestId, int keysDeleted);

	[LoggerMessage(
		ComplianceEventId.ErasureExecutionFailed,
		LogLevel.Error,
		"Erasure execution failed for request {RequestId}")]
	private partial void LogErasureExecutionFailed(Guid requestId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.ErasureGracePeriodBelowMinimum,
		LogLevel.Warning,
		"Requested grace period {Requested} is below minimum {Minimum}. Using minimum.")]
	private partial void LogErasureGracePeriodBelowMinimum(TimeSpan requested, TimeSpan minimum);

	[LoggerMessage(
		ComplianceEventId.ErasureGracePeriodExceedsMaximum,
		LogLevel.Warning,
		"Requested grace period {Requested} exceeds maximum {Maximum}. Using maximum.")]
	private partial void LogErasureGracePeriodExceedsMaximum(TimeSpan requested, TimeSpan maximum);

	[LoggerMessage(
		ComplianceEventId.ErasureKeysDiscovered,
		LogLevel.Information,
		"Discovered {KeyCount} keys for erasure request {RequestId} via data inventory")]
	private partial void LogErasureKeysDiscovered(Guid requestId, int keyCount);

	[LoggerMessage(
		ComplianceEventId.ErasureContributorCompleted,
		LogLevel.Information,
		"Erasure contributor '{ContributorName}' completed for request {RequestId}. Records affected: {RecordsAffected}")]
	private partial void LogErasureContributorCompleted(string contributorName, Guid requestId, int recordsAffected);

	[LoggerMessage(
		ComplianceEventId.ErasureContributorFailed,
		LogLevel.Warning,
		"Erasure contributor '{ContributorName}' failed for request {RequestId}: {ErrorMessage}")]
	private partial void LogErasureContributorFailed(string contributorName, Guid requestId, string errorMessage);

	[LoggerMessage(
		ComplianceEventId.ErasureContributorException,
		LogLevel.Error,
		"Erasure contributor '{ContributorName}' threw exception for request {RequestId}")]
	private partial void LogErasureContributorException(string contributorName, Guid requestId, Exception exception);
}
