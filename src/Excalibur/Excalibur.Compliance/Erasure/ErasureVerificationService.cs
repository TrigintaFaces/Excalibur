// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Compliance.Diagnostics;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Implementation of <see cref="IErasureVerificationService"/> for verifying erasure completeness.
/// </summary>
/// <remarks>
/// <para>
/// Verification uses defense-in-depth with multiple methods:
/// </para>
/// <list type="bullet">
/// <item><description>KMS key deletion confirmation</description></item>
/// <item><description>Audit log analysis</description></item>
/// <item><description>Decryption failure testing (when enabled)</description></item>
/// </list>
/// </remarks>
public sealed partial class ErasureVerificationService : IErasureVerificationService
{
	private readonly IErasureStore _erasureStore;
	private readonly IKeyManagementProvider _keyProvider;
	private int _cannotConfirmLogged;
	private readonly IDataInventoryService _inventoryService;
	private readonly IAuditStore _auditStore;
	private readonly IOptions<ErasureOptions> _options;
	private readonly ILogger<ErasureVerificationService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureVerificationService"/> class.
	/// </summary>
	public ErasureVerificationService(
		IErasureStore erasureStore,
		IKeyManagementProvider keyProvider,
		IDataInventoryService inventoryService,
		IAuditStore auditStore,
		IOptions<ErasureOptions> options,
		ILogger<ErasureVerificationService> logger)
	{
		_erasureStore = erasureStore ?? throw new ArgumentNullException(nameof(erasureStore));
		_keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
		_inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
		_auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<VerificationResult> VerifyErasureAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var failures = new List<VerificationFailure>();
		var warnings = new List<string>();
		var deletedKeyIds = new List<string>();
		var methodsUsed = VerificationMethod.None;

		LogErasureVerificationStarted(requestId);

		try
		{
			// Get the erasure request status
			var status = await _erasureStore.GetStatusAsync(requestId, cancellationToken)
				.ConfigureAwait(false);

			if (status is null)
			{
				return VerificationResult.Failed(
					new VerificationFailure
					{
						Subject = "ErasureRequest",
						Reason = $"Erasure request {requestId} not found",
						Severity = VerificationSeverity.Critical
					},
					stopwatch.Elapsed);
			}

			if (status.Status != ErasureRequestStatus.Completed)
			{
				return VerificationResult.Failed(
					new VerificationFailure
					{
						Subject = "ErasureRequest",
						Reason = $"Erasure request is not completed (status: {status.Status})",
						Severity = VerificationSeverity.Critical
					},
					stopwatch.Elapsed);
			}

			// Get certificate for key information
			ErasureCertificate? certificate = null;
			if (status.CertificateId.HasValue)
			{
				var certStore = (IErasureCertificateStore?)_erasureStore.GetService(typeof(IErasureCertificateStore));
				if (certStore is not null)
				{
					certificate = await certStore.GetCertificateByIdAsync(
						status.CertificateId.Value, cancellationToken).ConfigureAwait(false);
				}
			}

			var keyIdsToVerify = certificate?.Payload.Verification.DeletedKeyIds ?? [];

			// a certificate claiming keys were deleted but carrying NO deleted-key IDs is vacuous —
			// key-deletion verification would otherwise "pass" having checked zero keys. Fail it explicitly.
			if (certificate is { Payload.Summary.KeysDeleted: > 0 } && keyIdsToVerify.Count == 0)
			{
				failures.Add(new VerificationFailure
				{
					Subject = certificate.Payload.CertificateId.ToString(),
					Reason = "Certificate claims keys were deleted but carries no deleted-key IDs; erasure cannot be confirmed (vacuous certificate).",
					Severity = VerificationSeverity.Critical,
					FailedMethod = VerificationMethod.KeyManagementSystem
				});
			}

			// Verify via KMS if enabled
			var options = _options.Value;
			if ((options.VerificationMethods & VerificationMethod.KeyManagementSystem) != 0)
			{
				var kmsResult = await VerifyKeyDeletionsAsync(
					keyIdsToVerify,
					cancellationToken).ConfigureAwait(false);

				if (kmsResult.Success)
				{
					methodsUsed |= VerificationMethod.KeyManagementSystem;
					deletedKeyIds.AddRange(kmsResult.ConfirmedKeyIds);
				}
				else
				{
					failures.AddRange(kmsResult.Failures);
				}
			}

			// Verify via audit log if enabled
			if ((options.VerificationMethods & VerificationMethod.AuditLog) != 0)
			{
				var auditResult = await VerifyAuditTrailAsync(
					status,
					certificate,
					cancellationToken).ConfigureAwait(false);

				if (auditResult.Success)
				{
					methodsUsed |= VerificationMethod.AuditLog;
				}
				else
				{
					// Audit log failures are warnings, not critical failures
					warnings.AddRange(auditResult.Warnings);
				}
			}

			// Verify via decryption failure test if enabled
			if ((options.VerificationMethods & VerificationMethod.DecryptionFailure) != 0)
			{
				var decryptResult = await VerifyDecryptionFailsAsync(
					status,
					keyIdsToVerify,
					cancellationToken).ConfigureAwait(false);

				if (decryptResult.Success)
				{
					methodsUsed |= VerificationMethod.DecryptionFailure;
				}
				else
				{
					failures.AddRange(decryptResult.Failures);
				}
			}

			// Determine overall result
			var criticalFailures = failures.Where(f => f.Severity == VerificationSeverity.Critical).ToList();
			if (criticalFailures.Count > 0)
			{
				LogErasureVerificationFailed(requestId, criticalFailures.Count);

				return new VerificationResult
				{
					Verified = false,
					Methods = methodsUsed,
					DeletedKeyIds = deletedKeyIds,
					Failures = failures,
					Warnings = warnings,
					Duration = stopwatch.Elapsed,
					ResultHash = ComputeResultHash(requestId, false, methodsUsed, failures)
				};
			}

			LogErasureVerificationPassed(requestId, methodsUsed);

			return new VerificationResult
			{
				Verified = true,
				Methods = methodsUsed,
				DeletedKeyIds = deletedKeyIds,
				Failures = failures,
				Warnings = warnings,
				Duration = stopwatch.Elapsed,
				ResultHash = ComputeResultHash(requestId, true, methodsUsed, failures)
			};
		}
		catch (Exception ex)
		{
			LogErasureVerificationError(requestId, ex);

			return VerificationResult.Failed(
				new VerificationFailure
				{
					Subject = "Verification",
					Reason = $"Verification failed with exception: {ex.Message}",
					Severity = VerificationSeverity.Critical,
					Details = ex.ToString()
				},
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc />
	public async Task<VerificationReport> GenerateReportAsync(
		Guid requestId,
		CancellationToken cancellationToken)
	{
		var steps = new List<VerificationStep>();
		var stopwatch = ValueStopwatch.StartNew();

		// Step 1: Get erasure status
		var stepStart = ValueStopwatch.StartNew();
		var status = await _erasureStore.GetStatusAsync(requestId, cancellationToken)
			.ConfigureAwait(false);

		steps.Add(new VerificationStep
		{
			Name = "Retrieve Erasure Request",
			Method = VerificationMethod.None,
			Passed = status is not null,
			Details = status is null
				? "Request not found"
				: $"Status: {status.Status}, Keys: {status.KeysDeleted}",
			Duration = stepStart.Elapsed
		});

		if (status is null)
		{
			return CreateFailedReport(requestId, steps, stopwatch.Elapsed);
		}

		// Step 2: Get certificate if available
		ErasureCertificate? certificate = null;
		var reportCertStore = (IErasureCertificateStore?)_erasureStore.GetService(typeof(IErasureCertificateStore));
		if (status.CertificateId.HasValue && reportCertStore is not null)
		{
			stepStart = ValueStopwatch.StartNew();
			certificate = await reportCertStore.GetCertificateByIdAsync(
				status.CertificateId.Value, cancellationToken).ConfigureAwait(false);

			steps.Add(new VerificationStep
			{
				Name = "Retrieve Certificate",
				Method = VerificationMethod.None,
				Passed = certificate is not null,
				Details = certificate is null
					? "Certificate not found"
					: $"Certificate: {certificate.Payload.CertificateId}, Keys: {certificate.Payload.Verification.DeletedKeyIds.Count}",
				Duration = stepStart.Elapsed
			});
		}

		var keyIdsToVerify = certificate?.Payload.Verification.DeletedKeyIds ?? [];

		// Step 3: KMS verification
		var options = _options.Value;
		if ((options.VerificationMethods & VerificationMethod.KeyManagementSystem) != 0)
		{
			stepStart = ValueStopwatch.StartNew();
			var kmsResult = await VerifyKeyDeletionsAsync(keyIdsToVerify, cancellationToken)
				.ConfigureAwait(false);

			steps.Add(new VerificationStep
			{
				Name = "Key Management System Verification",
				Method = VerificationMethod.KeyManagementSystem,
				Passed = kmsResult.Success,
				Details = kmsResult.Success
					? $"Confirmed {kmsResult.ConfirmedKeyIds.Count} keys deleted"
					: $"Failed: {string.Join(", ", kmsResult.Failures.Select(f => f.Reason))}",
				Duration = stepStart.Elapsed
			});
		}

		// Step 4: Audit log verification
		if ((options.VerificationMethods & VerificationMethod.AuditLog) != 0)
		{
			stepStart = ValueStopwatch.StartNew();
			var auditResult = await VerifyAuditTrailAsync(status, certificate, cancellationToken)
				.ConfigureAwait(false);

			steps.Add(new VerificationStep
			{
				Name = "Audit Log Verification",
				Method = VerificationMethod.AuditLog,
				Passed = auditResult.Success,
				Details = auditResult.Success
					? "Audit trail verified"
					: $"Warnings: {string.Join(", ", auditResult.Warnings)}",
				Duration = stepStart.Elapsed
			});
		}

		// Step 5: Decryption failure verification
		if ((options.VerificationMethods & VerificationMethod.DecryptionFailure) != 0)
		{
			stepStart = ValueStopwatch.StartNew();
			var decryptResult = await VerifyDecryptionFailsAsync(
				status, keyIdsToVerify, cancellationToken).ConfigureAwait(false);

			steps.Add(new VerificationStep
			{
				Name = "Decryption Failure Verification",
				Method = VerificationMethod.DecryptionFailure,
				Passed = decryptResult.Success,
				Details = decryptResult.Success
					? "Confirmed encrypted data is irrecoverable"
					: $"Failed: {string.Join(", ", decryptResult.Failures.Select(f => f.Reason))}",
				Duration = stepStart.Elapsed
			});
		}

		// Get overall verification result
		var verificationResult = await VerifyErasureAsync(requestId, cancellationToken)
			.ConfigureAwait(false);

		var report = new VerificationReport
		{
			ReportId = Guid.NewGuid(),
			RequestId = requestId,
			Result = verificationResult,
			Steps = steps,
			GeneratedAt = DateTimeOffset.UtcNow
		};

		// Add integrity hash
		return report with { ReportHash = ComputeReportHash(report) };
	}

	/// <inheritdoc />
	public async Task<bool> VerifyKeyDeletionAsync(
		string keyId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		try
		{
			LogErasureKeyDeletionVerificationStarted(keyId);

			// A provider that can answer authoritatively is asked directly. This matters because the lookup below
			// cannot tell "destroyed" from "deleted but still recoverable" on a backend that reports a recoverable
			// key as not found -- Azure Key Vault answers a soft-deleted key with 404 for its whole retention
			// period -- and confirming on that answer would attest an erasure while the key can still be restored.
			if (_keyProvider.GetService(typeof(IKeyDestructionStatusProvider))
				is IKeyDestructionStatusProvider destructionStatus)
			{
				var destroyed = await destructionStatus.IsKeyDestroyedAsync(keyId, cancellationToken)
					.ConfigureAwait(false);
				LogErasureKeyDestructionStatusReported(keyId, destroyed);
				return destroyed;
			}

			// A provider that cannot answer is never read as having answered "destroyed". Its key lookup is not a
			// substitute: "not found" is exactly what a backend with a recovery window says about a key it can
			// still restore, so confirming on it would certify an erasure while the key is recoverable. The
			// request stays awaiting destruction instead, and the provider is named once so an operator knows
			// what to implement.
			LogKeyProviderCannotConfirmDestructionOnce();
			return false;
		}
		catch (Exception ex)
		{
			LogErasureKeyDeletionError(keyId, ex);
			return false;
		}
	}

	private static string ComputeResultHash(
		Guid requestId,
		bool verified,
		VerificationMethod methods,
		List<VerificationFailure> failures)
	{
		var data = new VerificationResultHashData(
			requestId,
			verified,
			methods.ToString(),
			failures.Count,
			DateTimeOffset.UtcNow.ToString("O"));

		var json = JsonSerializer.Serialize(data, ErasureVerificationJsonContext.Default.VerificationResultHashData);
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
		return Convert.ToBase64String(hash);
	}

	private static string ComputeReportHash(VerificationReport report)
	{
		var data = new VerificationReportHashData(
			report.ReportId,
			report.RequestId,
			report.Result.Verified,
			report.Result.Methods.ToString(),
			report.Steps.Count,
			report.GeneratedAt);

		var json = JsonSerializer.Serialize(data, ErasureVerificationJsonContext.Default.VerificationReportHashData);
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
		return Convert.ToBase64String(hash);
	}

	private static VerificationReport CreateFailedReport(
		Guid requestId,
		List<VerificationStep> steps,
		TimeSpan duration)
	{
		return new VerificationReport
		{
			ReportId = Guid.NewGuid(),
			RequestId = requestId,
			Result = VerificationResult.Failed(
				new VerificationFailure
				{
					Subject = "ErasureRequest",
					Reason = "Erasure request not found",
					Severity = VerificationSeverity.Critical
				},
				duration),
			Steps = steps,
			GeneratedAt = DateTimeOffset.UtcNow
		};
	}

	[LoggerMessage(
		ComplianceEventId.ErasureVerificationStarted,
		LogLevel.Debug,
		"Starting verification for erasure request {RequestId}")]
	private partial void LogErasureVerificationStarted(Guid requestId);

	[LoggerMessage(
		ComplianceEventId.ErasureVerificationFailed,
		LogLevel.Warning,
		"Verification failed for erasure request {RequestId}: {FailureCount} critical failures")]
	private partial void LogErasureVerificationFailed(Guid requestId, int failureCount);

	[LoggerMessage(
		ComplianceEventId.ErasureVerificationPassed,
		LogLevel.Information,
		"Verification passed for erasure request {RequestId} using methods: {Methods}")]
	private partial void LogErasureVerificationPassed(Guid requestId, VerificationMethod methods);

	[LoggerMessage(
		ComplianceEventId.ErasureVerificationError,
		LogLevel.Error,
		"Error during verification of erasure request {RequestId}")]
	private partial void LogErasureVerificationError(Guid requestId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.ErasureKeyDeletionVerificationStarted,
		LogLevel.Debug,
		"Verifying deletion of key {KeyId}")]
	private partial void LogErasureKeyDeletionVerificationStarted(string keyId);

	private void LogKeyProviderCannotConfirmDestructionOnce()
	{
		if (Interlocked.Exchange(ref _cannotConfirmLogged, 1) == 0)
		{
			LogKeyProviderCannotConfirmDestruction(_keyProvider.GetType().FullName ?? _keyProvider.GetType().Name);
		}
	}

	[LoggerMessage(
		ComplianceEventId.ErasureKeyProviderCannotConfirmDestruction,
		LogLevel.Warning,
		"Key provider {ProviderType} does not implement IKeyDestructionStatusProvider, so erasure cannot confirm that "
		+ "its keys are destroyed. Erasures that depend on it stay AwaitingKeyDestruction and are never certified. "
		+ "Implement IKeyDestructionStatusProvider on the provider to let them complete.")]
	private partial void LogKeyProviderCannotConfirmDestruction(string providerType);

	[LoggerMessage(
		ComplianceEventId.ErasureKeyDeletionError,
		LogLevel.Error,
		"Error verifying deletion of key {KeyId}")]
	private partial void LogErasureKeyDeletionError(string keyId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.ErasureKeyDestructionStatusReported,
		LogLevel.Debug,
		"Key {KeyId} destruction reported by the key-management provider: destroyed = {Destroyed}")]
	private partial void LogErasureKeyDestructionStatusReported(string keyId, bool destroyed);

	[LoggerMessage(
		ComplianceEventId.ErasureKeyDeletionExpectedError,
		LogLevel.Debug,
		"Expected error accessing deleted key {KeyId}")]
	private partial void LogErasureKeyDeletionExpectedError(string keyId, Exception exception);

	private async Task<KmsVerificationResult> VerifyKeyDeletionsAsync(
		IReadOnlyList<string> keyIds,
		CancellationToken cancellationToken)
	{
		var confirmedKeyIds = new List<string>();
		var failures = new List<VerificationFailure>();

		if (keyIds.Count == 0)
		{
			// Nothing was checked, so nothing is confirmed. This used to return Success = true, which
			// reaches the public report as VerificationStep.Passed with "Confirmed 0 keys deleted" --
			// read by an auditor as a pass. For an erasure report the absence of any recorded key
			// deletion IS the finding: key destruction is the mechanism that makes erasure
			// irreversible, and if none is on record it cannot be evidenced.
			return new KmsVerificationResult
			{
				Success = false,
				ConfirmedKeyIds = [],
				Failures =
				[
					new VerificationFailure
					{
						Subject = "key-deletions",
						// Warning, not Error: the type already distinguishes these and documents Warning as
						// "erasure likely complete but cannot fully verify", which is exactly this case.
						Severity = VerificationSeverity.Warning,
						Reason =
							"No key deletions were on record for this erasure, so key destruction could not be "
							+ "verified. This is not a confirmed failure to delete; it is an absence of evidence "
							+ "that deletion happened."
					}
				]
			};
		}

		foreach (var keyId in keyIds)
		{
			var isDeleted = await VerifyKeyDeletionAsync(keyId, cancellationToken)
				.ConfigureAwait(false);

			if (isDeleted)
			{
				confirmedKeyIds.Add(keyId);
			}
			else
			{
				failures.Add(new VerificationFailure
				{
					Subject = $"Key:{keyId}",
					Reason = "Key was not confirmed deleted in KMS",
					Severity = VerificationSeverity.Critical,
					FailedMethod = VerificationMethod.KeyManagementSystem
				});
			}
		}

		return new KmsVerificationResult { Success = failures.Count == 0, ConfirmedKeyIds = confirmedKeyIds, Failures = failures };
	}

	private async Task<AuditVerificationResult> VerifyAuditTrailAsync(
		ErasureStatus status,
		ErasureCertificate? certificate,
		CancellationToken cancellationToken)
	{
		var warnings = new List<string>();

		// Build audit query for erasure-related events
		var query = new AuditQuery
		{
			ResourceId = status.RequestId.ToString(),
			ResourceType = "ErasureRequest",
			EventTypes = [AuditEventType.Compliance],
			StartDate = status.RequestedAt,
			EndDate = DateTimeOffset.UtcNow,
			MaxResults = 1000
		};

		// Query the audit store for erasure events
		var events = await _auditStore.QueryAsync(query, cancellationToken)
			.ConfigureAwait(false);

		// Check for completion event
		var completionEvent = events.FirstOrDefault(e =>
			e.Action == ErasureAuditActions.Completed);

		// Check for failure/rollback events
		var failureEvents = events.Where(e =>
			e.Action is ErasureAuditActions.Failed or ErasureAuditActions.RolledBack).ToList();

		// Check for key deletion events
		var keyDeletionEvents = events.Where(e =>
			e.Action == ErasureAuditActions.KeyDeleted).ToList();

		// Determine result
		if (completionEvent is null)
		{
			warnings.Add("No erasure completion event found in audit log");
		}

		if (failureEvents.Count > 0)
		{
			warnings.Add($"Found {failureEvents.Count} failure/rollback events in audit log");
			return new AuditVerificationResult
			{
				Success = false,
				Warnings = warnings
			};
		}

		// Check if we have the expected number of key deletion events
		var expectedKeyCount = certificate?.Payload.Verification.DeletedKeyIds.Count ?? 0;
		if (expectedKeyCount > 0 && keyDeletionEvents.Count < expectedKeyCount)
		{
			warnings.Add($"Expected {expectedKeyCount} key deletion events, found {keyDeletionEvents.Count}");
		}

		// Also check the status object
		if (status.KeysDeleted is null or 0)
		{
			warnings.Add("No keys were deleted as part of this erasure");
		}

		return new AuditVerificationResult
		{
			Success = completionEvent is not null,
			Warnings = warnings
		};
	}

	private async Task<DecryptionVerificationResult> VerifyDecryptionFailsAsync(
		ErasureStatus status,
		IReadOnlyList<string> deletedKeyIds,
		CancellationToken cancellationToken)
	{
		var failures = new List<VerificationFailure>();

		// Get data inventory for this data subject
		var inventory = await _inventoryService.DiscoverAsync(
			status.DataSubjectIdHash, // Using hash since we don't have original
			status.IdType,
			status.TenantId,
			cancellationToken).ConfigureAwait(false);

		// For each location with an associated key, verify decryption fails
		foreach (var location in inventory.Locations.Where(l => !string.IsNullOrEmpty(l.KeyId)))
		{
			// A key counts as irrecoverable only when the provider positively confirms its destruction. A key lookup
			// that finds nothing is not that confirmation: a backend with a recovery window answers "not found" for a
			// key it can still restore, and passing here on that answer would report the data irrecoverable while its
			// key can be brought back.
			if (deletedKeyIds.Contains(location.KeyId)
				&& !await VerifyKeyDeletionAsync(location.KeyId, cancellationToken).ConfigureAwait(false))
			{
				failures.Add(new VerificationFailure
				{
					Subject = $"Location:{location.TableName}.{location.FieldName}",
					Reason = $"Key {location.KeyId} is not confirmed destroyed - data may still be decryptable",
					Severity = VerificationSeverity.Critical,
					FailedMethod = VerificationMethod.DecryptionFailure
				});
			}
		}

		return new DecryptionVerificationResult { Success = failures.Count == 0, Failures = failures };
	}

	/// <summary>
	/// Internal result from KMS verification.
	/// </summary>
	private sealed record KmsVerificationResult
	{
		public required bool Success { get; init; }
		public required List<string> ConfirmedKeyIds { get; init; }
		public required IReadOnlyList<VerificationFailure> Failures { get; init; }
	}

	/// <summary>
	/// Internal result from audit trail verification.
	/// </summary>
	private sealed record AuditVerificationResult
	{
		public required bool Success { get; init; }
		public IReadOnlyList<string> Warnings { get; init; } = [];
	}

	/// <summary>
	/// Internal result from decryption failure verification.
	/// </summary>
	private sealed record DecryptionVerificationResult
	{
		public required bool Success { get; init; }
		public required IReadOnlyList<VerificationFailure> Failures { get; init; }
	}

	/// <summary>
	/// Constants for erasure-related audit event actions.
	/// </summary>
	private static class ErasureAuditActions
	{
		/// <summary>
		/// Action for when a key was deleted as part of erasure.
		/// </summary>
		public const string KeyDeleted = "DataErasure.KeyDeleted";

		/// <summary>
		/// Action for when erasure completed successfully.
		/// </summary>
		public const string Completed = "DataErasure.Completed";

		/// <summary>
		/// Action for when erasure operation failed.
		/// </summary>
		public const string Failed = "DataErasure.Failed";

		/// <summary>
		/// Action for when erasure was rolled back.
		/// </summary>
		public const string RolledBack = "DataErasure.RolledBack";
	}
}

/// <summary>
/// Hash data for verification result integrity — AOT-compatible record type.
/// </summary>
internal sealed record VerificationResultHashData(
	Guid RequestId,
	bool Verified,
	string Methods,
	int FailureCount,
	string Timestamp);

/// <summary>
/// Hash data for verification report integrity — AOT-compatible record type.
/// </summary>
internal sealed record VerificationReportHashData(
	Guid ReportId,
	Guid RequestId,
	bool Verified,
	string Methods,
	int StepCount,
	DateTimeOffset GeneratedAt);

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible erasure verification hashing.
/// </summary>
[JsonSerializable(typeof(VerificationResultHashData))]
[JsonSerializable(typeof(VerificationReportHashData))]
internal sealed partial class ErasureVerificationJsonContext : JsonSerializerContext;
