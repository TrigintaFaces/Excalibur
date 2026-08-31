// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Compliance.Diagnostics;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

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
public sealed partial class ErasureService: IErasureService, IErasureExecutor
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
	private readonly IKeyManagementAdmin _keyAdmin;
	private readonly IOptions<ErasureOptions> _options;
	private readonly ILogger<ErasureService> _logger;
	private readonly IReadOnlyList<IErasureContributor> _contributors;
	private readonly IPersonalDataAnnotationSource _annotationSource;
	private readonly IDataSubjectHasher _dataSubjectHasher;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureService"/> class.
	/// </summary>
	/// <param name="store">The erasure store for persistence.</param>
	/// <param name="keyAdmin">The key management admin provider for key deletion.</param>
	/// <param name="options">The erasure options (includes signing configuration via <see cref="ErasureRetentionOptions.SigningKey"/>).</param>
	/// <param name="logger">The logger.</param>
	/// <param name="dataSubjectHasher">The keyed hasher used to pseudonymize data-subject identifiers.</param>
	/// <param name="legalHoldService">Optional legal hold service for Article 17(3) checks. Pass <see langword="null"/> if not available.</param>
	/// <param name="dataInventoryService">Optional data inventory service for discovery. Pass <see langword="null"/> if not available.</param>
	/// <param name="contributors">Optional erasure contributors for additional store erasure (event stores, snapshot stores, etc.).</param>
	public ErasureService(
 IErasureStore store,
 IKeyManagementAdmin keyAdmin,
 IOptions<ErasureOptions> options,
 ILogger<ErasureService> logger,
 IDataSubjectHasher dataSubjectHasher,
 ILegalHoldService? legalHoldService,
 IDataInventoryService? dataInventoryService,
 IEnumerable<IErasureContributor>? contributors = null)
: this(store, keyAdmin, options, logger, dataSubjectHasher, legalHoldService, dataInventoryService,
 IPersonalDataAnnotationSource.CreateDefault(), contributors)
	{
	}

	/// <summary>
	/// Test/internal constructor allowing the <see cref="IPersonalDataAnnotationSource"/> to be injected
	/// so the annotated-coverage gate is deterministically verifiable without assembly scanning.
	/// </summary>
	internal ErasureService(
 IErasureStore store,
 IKeyManagementAdmin keyAdmin,
 IOptions<ErasureOptions> options,
 ILogger<ErasureService> logger,
 IDataSubjectHasher dataSubjectHasher,
 ILegalHoldService? legalHoldService,
 IDataInventoryService? dataInventoryService,
 IPersonalDataAnnotationSource annotationSource,
 IEnumerable<IErasureContributor>? contributors = null)
	{
 _store = store ?? throw new ArgumentNullException(nameof(store));
 _keyAdmin = keyAdmin ?? throw new ArgumentNullException(nameof(keyAdmin));
 _options = options ?? throw new ArgumentNullException(nameof(options));
 _logger = logger ?? throw new ArgumentNullException(nameof(logger));
 _dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
 _legalHoldService = legalHoldService;
 _dataInventoryService = dataInventoryService;
 _annotationSource = annotationSource ?? throw new ArgumentNullException(nameof(annotationSource));
 // materialize once — the injected IEnumerable is enumerated in both ExecuteAsync and
		// EvaluateCoverage, so a lazy/once-only sequence would yield inconsistent results (or re-run
		// factories). Matches RetentionEnforcementService's [.. contributors].
		_contributors = contributors is null ? [] : [.. contributors];
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
 RequestsBlockedCounter.Add(1, new TagList { { ErasureTelemetryConstants.Tags.Scope, request.Scope.ToString() } });
 activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "blocked");

 if (holdCheck.ActiveHolds.Count > 0)
 {
 var blockingHold = holdCheck.ActiveHolds[0];
 LogErasureRequestBlocked(request.RequestId, blockingHold.HoldId);
 return ErasureResult.Blocked(request.RequestId, blockingHold);
 }

 LogErasureRequestBlockedNoHold(request.RequestId);
 return ErasureResult.Blocked(request.RequestId, new LegalHoldInfo
 {
 HoldId = Guid.Empty,
 Basis = LegalHoldBasis.LegalClaims,
 CaseReference = "unknown",
 CreatedAt = DateTimeOffset.UtcNow
 });
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

 if (status.Status != ErasureRequestStatus.Completed)
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
 Method = DetermineErasureMethod(status.KeysDeleted ?? 0, status.RecordsAffected ?? 0),
 Summary = new ErasureSummary
 {
 KeysDeleted = status.KeysDeleted ?? 0,
 RecordsAffected = status.RecordsAffected ?? 0,
 DataCategories = [],
 TablesAffected = []
 },
 // this fallback reconstructs the certificate from persisted status alone, which carries the
 // KeysDeleted COUNT but not the deleted key IDs (those exist only on the live execution path via
 // PersistCompletionCertificateAsync). Without the key IDs we cannot substantiate key-deletion, so we
 // must NOT claim Verified=true with an empty DeletedKeyIds (a trivially-passing, dishonest attestation).
 // Verified is true only when there were no keys to delete (nothing to substantiate); otherwise the
 // reconstructed certificate honestly reports unverified.
 Verification =
 new VerificationSummary
 {
 Verified = (status.KeysDeleted ?? 0) == 0,
 Methods = _options.Value.VerificationMethods,
 VerifiedAt = DateTimeOffset.UtcNow,
 DeletedKeyIds = []
 },
 LegalBasis = status.LegalBasis,
 Signature = GenerateSignature(requestId, status),
 RetainUntil = DateTimeOffset.UtcNow.Add(_options.Value.Retention.CertificateRetentionPeriod)
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

 // Atomically transition to InProgress FIRST — if another caller already claimed this request, abort.
 // This prevents concurrent execution and establishes exclusive ownership before any further checks.
 var transitioned = await _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, errorMessage: null, cancellationToken: cancellationToken)
.ConfigureAwait(false);

 if (!transitioned)
 {
 return ErasureExecutionResult.Failed("Request is no longer in Scheduled status (concurrent execution detected)");
 }

 // Re-check legal holds AFTER the atomic InProgress transition (TOCTOU fix: tightens the window
 // by ensuring we own the request exclusively before checking holds, and check holds immediately
 // before executing erasure operations)
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

 try
 {
 // Discover keys to delete via data inventory (use hash-based lookup)
 var keysToDelete = new List<string>();
 IReadOnlyList<DataLocation> discoveredLocations = [];
 if (_dataInventoryService is not null)
 {
 var inventory = await _dataInventoryService.DiscoverAsync(
 status.DataSubjectIdHash, DataSubjectIdType.Hash, status.TenantId, cancellationToken)
.ConfigureAwait(false);

 foreach (var keyRef in inventory.AssociatedKeys)
 {
 keysToDelete.Add(keyRef.KeyId);
 }

 // the discovered locations drive the structural coverage gate below.
 discoveredLocations = inventory.Locations;

 LogErasureKeysDiscovered(requestId, keysToDelete.Count);
 }

 // Per-subject crypto-shred: a subject's dedicated key handle is deterministically the
 // subject-id hash (SubjectKeyManager derives keyId = IDataSubjectHasher.HashDataSubjectId, the
 // same hash as status.DataSubjectIdHash). Always destroy it -- even when the data inventory does
 // not enumerate it -- so destroying the key erases the subject regardless of inventory coverage.
 if (!keysToDelete.Contains(status.DataSubjectIdHash))
 {
 	keysToDelete.Add(status.DataSubjectIdHash);
 }

 // Delete keys via admin interface. Track WHICH keys were actually deleted (not just the count):
 // the deleted-key set is the crypto-shred coverage mechanism for the gate (a location whose KeyId
 // was deleted is cryptographically unreadable) and is recorded on the certificate.
 var deletedKeyIds = new List<string>();
 var errors = new List<string>();

 var deletedCount = await ExecuteKeyDeletionsAsync(keysToDelete, deletedKeyIds, errors, requestId, cancellationToken)
.ConfigureAwait(false);

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

 // Amendment 1/1a — STRUCTURAL key-aware coverage gate (computed by EvaluateCoverage).
 // A location is Covered iff its key was deleted (crypto-shred), its store-kind has a registered
 // contributor, or its store-kind is a declared exemption. Any Uncovered location means personal data
 // would survive, so the Completed outcome is made UNREACHABLE below (enforce-invariants-structurally):
 // the branch that records completion is the only one that does NOT run when an uncovered location
 // exists. Exemptions actually in play are carried onto the certificate.
 // Structural key-aware coverage gate + affirmative-proof, assembled in
 // AppendCoverageGateErrors (extracted to keep ExecuteAsync within its class-coupling budget). Any
 // uncovered, undiscovered-annotated, or unverified-coverage condition adds an error, which makes the
 // Completed branch below (errors.Count == 0) UNREACHABLE (enforce-invariants-structurally) — a
 // "Completed" certificate over uncovered or unverified personal data is structurally inexpressible.
 var coverage = EvaluateCoverage(discoveredLocations, deletedKeyIds);
 AppendCoverageGateErrors(errors, coverage);

 // Determine outcome. Completed is reachable ONLY when there are zero errors AND zero uncovered
 // locations — the structural invariant: a silent Completed over an uncovered store is inexpressible
 // because this branch is the only path that does NOT call RecordCompletionAsync.
 if (errors.Count > 0)
 {
 // A contributor/key-deletion failed OR a location is uncovered -- do NOT mark fully Completed.
 // PartiallyCompleted if some work succeeded, Failed if nothing succeeded.
 var hasAnySuccess = deletedCount > 0 || totalRecordsAffected > 0;
 var partialStatus = hasAnySuccess
 ? ErasureRequestStatus.PartiallyCompleted
: ErasureRequestStatus.Failed;
 var errorSummary = string.Join("; ", errors);

 _ = await _store.UpdateStatusAsync(requestId, partialStatus, errorSummary, cancellationToken)
.ConfigureAwait(false);

 LogErasurePartiallyCompleted(requestId, errors.Count, deletedCount);
 KeysDeletedCounter.Add(deletedCount);
 RequestsFailedCounter.Add(1, new TagList { { ErasureTelemetryConstants.Tags.ErrorType, "partial_failure" } });
 ExecutionDurationHistogram.Record(executionStopwatch.Elapsed.TotalMilliseconds);
 activity?.SetTag("erasure.keys_deleted", deletedCount);
 activity?.SetTag("erasure.records_affected", totalRecordsAffected);
 activity?.SetTag("erasure.uncovered_store_kinds", coverage.UncoveredStoreKinds.Count);
 activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "partial");

 return ErasureExecutionResult.PartiallySucceeded(deletedCount, totalRecordsAffected, errorSummary);
 }

 // Record full completion -- all contributors and key deletions succeeded, all locations covered.
 // Build + persist the populated certificate (real DeletedKeyIds + actual Method) so the recorded
 // certificate ID resolves to a NON-vacuous certificate that verification can confirm.
 var certificateId = await PersistCompletionCertificateAsync(
 requestId, status, deletedKeyIds, deletedCount, totalRecordsAffected, coverage.Exemptions, cancellationToken)
.ConfigureAwait(false);
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
	/// Destroys each crypto-shred key and classifies the tri-state outcome: only a <see cref="KeyDestructionState.Completed"/>
	/// key is counted as erased (added to <paramref name="deletedKeyIds"/>); a <see cref="KeyDestructionState.ScheduledIrreversible"/>
	/// key registers an error so the completion gate cannot attest it as irrecoverable; <see cref="KeyDestructionState.NotFound"/>
	/// is an idempotent no-op. Returns the count of keys actually destroyed.
	/// </summary>
	private async Task<int> ExecuteKeyDeletionsAsync(
		IEnumerable<string> keysToDelete,
		List<string> deletedKeyIds,
		List<string> errors,
		Guid requestId,
		CancellationToken cancellationToken)
	{
		var deletedCount = 0;

		foreach (var keyId in keysToDelete)
		{
			try
			{
				var outcome = await _keyAdmin.DeleteKeyAsync(keyId, 0, cancellationToken).ConfigureAwait(false);
				switch (outcome.State)
				{
					case KeyDestructionState.Completed:
						// Irrecoverable NOW — the only state that may be attested as erased.
						deletedCount++;
						deletedKeyIds.Add(keyId);
						break;

					case KeyDestructionState.ScheduledIrreversible:
						// Still recoverable until the disclosed instant. Register an error so the structural gate
						// cannot mark the request Completed or stamp a Verified=true certificate — a false
						// irrecoverable-now attestation is thereby structurally inexpressible.
						errors.Add(
							$"Key '{keyId}' is scheduled for irreversible destruction at {outcome.IrreversibleAt:O} "
							+ "but remains recoverable until then — erasure is not yet complete and MUST NOT be attested "
							+ "as irrecoverable.");
						break;

					case KeyDestructionState.NotFound:
						// Idempotent no-op: already erased or never created. Nothing to attest, no error.
						break;

					default:
						// An unrecognised destruction state must never be read as success. Attesting erasure
						// requires positive evidence of destruction; anything else fails closed, so a state
						// added to the enum later cannot silently inherit "erased" here.
						errors.Add(
							$"Key '{keyId}' returned an unrecognised destruction state '{outcome.State}' — "
							+ "erasure cannot be attested.");
						break;
				}
			}
			catch (Exception ex)
			{
				errors.Add($"Failed to delete key {keyId}: {ex.Message}");
				LogErasureKeyDeletionFailed(keyId, requestId, ex);
			}
		}

		return deletedCount;
	}

	/// <summary>Evaluates erasure coverage for the discovered locations (delegates to the evaluator).</summary>
	private void AppendCoverageGateErrors(List<string> errors, CoverageOutcome coverage)
	{
 // AFFIRMATIVE coverage proof. A completion certificate is a compliance PROOF, so it fails
 // CLOSED: the absence of discovered-uncovered stores is NOT proof of coverage when discovery never ran.
 // Without a data-inventory discovery source, store-level coverage is UNVERIFIED; feeding it into the
 // same errors gate makes a "Completed" certificate over unverified coverage structurally inexpressible.
 // KeyShredOnlyErasure opts into key-destruction-only erasure (the per-subject key is still shredded);
 // startup fail-fast blocks the no-discovery + no-opt-in configuration before it ever runs (backstop).
 if (_dataInventoryService is null && !_options.Value.KeyShredOnlyErasure)
 {
 errors.Add(
 "GDPR erasure coverage is UNVERIFIED: no data-inventory discovery source is registered, so "
 + "store-level coverage could not be proven. Register a discovery source (AddDataInventoryService / "
 + "RegisterDataLocationAsync) or set ErasureOptions.KeyShredOnlyErasure = true to accept "
 + "key-destruction-only erasure. A completion certificate is not issued over unverified coverage.");
 }

 // a discovered location not covered by crypto-shred, a contributor, or an exemption means
 // personal data would survive the erasure.
 if (coverage.UncoveredStoreKinds.Count > 0)
 {
 errors.Add(
 $"Personal data remains in uncovered store(s): {string.Join(", ", coverage.UncoveredStoreKinds)} — "
 + "no erasure contributor, crypto-shred, or declared exemption covers these locations.");
 }

 // [PersonalData]-annotated categories with no discovered/registered location are annotated
 // personal data the inventory never located.
 if (coverage.UncoveredAnnotatedCategories.Count > 0)
 {
 errors.Add(
 "[PersonalData]-annotated personal data was not located by the inventory (categories: "
 + $"{string.Join(", ", coverage.UncoveredAnnotatedCategories)}) — register these data locations "
 + "(RegisterDataLocationAsync) so erasure can cover them. Erasure is not Completed while "
 + "annotated personal data remains undiscovered.");
 }
	}

	private CoverageOutcome EvaluateCoverage(
 IReadOnlyList<DataLocation> locations,
 IReadOnlyCollection<string> deletedKeyIds) =>
 ErasureCoverageEvaluator.Evaluate(
 locations, deletedKeyIds, _contributors, _annotationSource.GetAnnotatedCategories());

	/// <summary>
	/// Computes the keyed pseudonymization hash of a data subject identifier for storage.
	/// </summary>
	internal string HashDataSubjectId(string dataSubjectId) =>
 _dataSubjectHasher.HashDataSubjectId(dataSubjectId);

	/// <summary>
	/// Derives the erasure <see cref="ErasureMethod"/> actually used from what was erased: key deletion
	/// (cryptographic), contributor row-delete/tombstone (physical), or both (hybrid). Replaces the prior
	/// hardcoded <see cref="ErasureMethod.CryptographicErasure"/> so the certificate reflects reality.
	/// </summary>
	private static ErasureMethod DetermineErasureMethod(int keysDeleted, int recordsAffected) =>
 (keysDeleted > 0, recordsAffected > 0) switch
 {
 (true, true) => ErasureMethod.Hybrid,
 (false, true) => ErasureMethod.PhysicalDeletion,
 _ => ErasureMethod.CryptographicErasure,
 };

	/// <summary>
	/// Builds and persists the completion certificate at execution time, carrying the REAL deleted-key IDs
	/// so verification is non-vacuous — the prior code recorded only a count and left the cert's
	/// <see cref="VerificationSummary.DeletedKeyIds"/> empty, making key-deletion verification trivially pass.
	/// The certificate is saved under the returned ID, which is then recorded on the request status so
	/// <see cref="GenerateCertificateAsync"/> returns this populated certificate.
	/// </summary>
	/// <returns>The certificate ID to record on the completed request.</returns>
	private async Task<Guid> PersistCompletionCertificateAsync(
 Guid requestId,
 ErasureStatus status,
 IReadOnlyList<string> deletedKeyIds,
 int keysDeleted,
 int recordsAffected,
 IReadOnlyList<ErasureException> exemptions,
 CancellationToken cancellationToken)
	{
 var certificateId = Guid.NewGuid();

 // Some stores do not support certificate operations; record the ID without an eager certificate.
 if (_store.GetService(typeof(IErasureCertificateStore)) is not IErasureCertificateStore certStore)
 {
 return certificateId;
 }

 var completedAt = DateTimeOffset.UtcNow;
 var certificate = new ErasureCertificate
 {
 CertificateId = certificateId,
 RequestId = requestId,
 DataSubjectReference = status.DataSubjectIdHash,
 RequestReceivedAt = status.RequestedAt,
 CompletedAt = completedAt,
 Method = DetermineErasureMethod(keysDeleted, recordsAffected),
 Summary = new ErasureSummary
 {
 KeysDeleted = keysDeleted,
 RecordsAffected = recordsAffected,
 DataCategories = [],
 TablesAffected = []
 },
 Verification = new VerificationSummary
 {
 Verified = true,
 Methods = _options.Value.VerificationMethods,
 VerifiedAt = completedAt,
 DeletedKeyIds = deletedKeyIds
 },
 LegalBasis = status.LegalBasis,
 // enumerate exemptions (e.g. audit store) with their legal basis — explicit, never silent.
 Exceptions = exemptions,
 Signature = GenerateSignature(requestId, status.DataSubjectIdHash, completedAt),
 RetainUntil = completedAt.Add(_options.Value.Retention.CertificateRetentionPeriod)
 };

 await certStore.SaveCertificateAsync(certificate, cancellationToken).ConfigureAwait(false);
 return certificateId;
	}

	private string GenerateSignature(Guid requestId, ErasureStatus status) =>
 GenerateSignature(requestId, status.DataSubjectIdHash, status.CompletedAt ?? DateTimeOffset.UtcNow);

	private string GenerateSignature(Guid requestId, string dataSubjectIdHash, DateTimeOffset completedAt)
	{
 var signingKey = _options.Value.Retention.SigningKey;
 var dataToSign = $"{requestId}|{dataSubjectIdHash}|{completedAt:O}";
 var dataBytes = Encoding.UTF8.GetBytes(dataToSign);

 if (signingKey.Length == 0)
 {
 // Fail closed: a keyless SHA-256 hash is not a signature — anyone can recompute it, so it is
 // forgeable and provides no tamper evidence. Refuse to emit an unsigned "signature" rather than
 // produce an erasure certificate that cannot be authenticated.
 throw new InvalidOperationException(
 "Cannot generate a tamper-evident erasure certificate signature: no HMAC signing key is configured. " +
 "Configure the erasure retention signing key (supplied from a secret manager) so certificates are " +
 "signed with HMAC-SHA256.");
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
 ComplianceEventId.ErasureBlockedNoHoldDetails,
 LogLevel.Warning,
 "Erasure request {RequestId} blocked by legal hold but no hold details available")]
	private partial void LogErasureRequestBlockedNoHold(Guid requestId);

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

	[LoggerMessage(
 ComplianceEventId.ErasurePartiallyCompleted,
 LogLevel.Warning,
 "Erasure request {RequestId} partially completed with {ErrorCount} error(s). Keys deleted: {KeysDeleted}")]
	private partial void LogErasurePartiallyCompleted(Guid requestId, int errorCount, int keysDeleted);
}
