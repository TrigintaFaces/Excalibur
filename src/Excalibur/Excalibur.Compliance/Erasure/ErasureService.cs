// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
	private readonly ILegalHoldService _legalHoldService;
	private readonly IDataInventoryService? _dataInventoryService;
	private readonly IKeyEscrowService? _keyEscrowService;
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
	/// <param name="legalHoldService">Legal hold service for Article 17(3) checks. Required: the constructor rejects <see langword="null"/>. A deployment that operates no legal holds registers the explicit no-op via <c>AddNoLegalHolds()</c> rather than passing <see langword="null"/>, so "we hold nothing" is a stated position rather than an omission that reads the same as a forgotten registration.</param>
	/// <param name="dataInventoryService">Optional data inventory service for discovery. Pass <see langword="null"/> if not available.</param>
	/// <param name="keyEscrowService">
	/// Optional key escrow service. Pass <see langword="null"/> if not available. When present,
	/// a subject's escrowed key copy is revoked before the working key is destroyed, so a consumer who
	/// escrowed a subject key for disaster recovery cannot silently defeat erasure — the escrowed spare
	/// no longer outlives the key it was a backup of.
	/// </param>
	/// <param name="contributors">Optional erasure contributors for additional store erasure (event stores, snapshot stores, etc.).</param>
	public ErasureService(
 IErasureStore store,
 IKeyManagementAdmin keyAdmin,
 IOptions<ErasureOptions> options,
 ILogger<ErasureService> logger,
 IDataSubjectHasher dataSubjectHasher,
 ILegalHoldService legalHoldService,
 IDataInventoryService? dataInventoryService,
 IKeyEscrowService? keyEscrowService,
 IEnumerable<IErasureContributor>? contributors = null)
: this(store, keyAdmin, options, logger, dataSubjectHasher, legalHoldService, dataInventoryService,
 keyEscrowService, IPersonalDataAnnotationSource.CreateDefault(), contributors)
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
 ILegalHoldService legalHoldService,
 IDataInventoryService? dataInventoryService,
 IKeyEscrowService? keyEscrowService,
 IPersonalDataAnnotationSource annotationSource,
 IEnumerable<IErasureContributor>? contributors = null)
	{
 _store = store ?? throw new ArgumentNullException(nameof(store));
 _keyAdmin = keyAdmin ?? throw new ArgumentNullException(nameof(keyAdmin));
 _options = options ?? throw new ArgumentNullException(nameof(options));
 _logger = logger ?? throw new ArgumentNullException(nameof(logger));
 _dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
 _legalHoldService = legalHoldService ?? throw new ArgumentNullException(nameof(legalHoldService));
 _dataInventoryService = dataInventoryService;
 _keyEscrowService = keyEscrowService;
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

 // Legal holds are checked unconditionally. The dependency is required, so a deployment that
 // operates none supplies the declared no-holds service rather than leaving this null -- which is
 // what makes the absence of a check impossible to reach by omission.
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

 // One completion instant, read once. Every field below that describes WHEN is derived from it rather
 // than from its own clock read, so they cannot disagree with each other or with the persisted status
 // this certificate reconstructs.
 var completedAt = status.CompletedAt ?? DateTimeOffset.UtcNow;

 var reconstructed = new ErasureCertificatePayload
 {
 CertificateId = Guid.NewGuid(),
 RequestId = requestId,
 DataSubjectReference = status.DataSubjectIdHash,
 RequestReceivedAt = status.RequestedAt,
 CompletedAt = completedAt,
 Method = DetermineErasureMethod(status.KeysDeleted ?? 0, status.RecordsAffected ?? 0),
 Summary = new ErasureSummary
 {
 KeysDeleted = status.KeysDeleted ?? 0,
 RecordsAffected = status.RecordsAffected ?? 0,
 DataCategories = [],
 TablesAffected = []
 },
 // This fallback reconstructs from persisted status alone, which carries COUNTS but not the deleted
 // key IDs -- those exist only on the live execution path. Without the ids there is nothing to
 // substantiate the erasure with, so Verified is FALSE here unconditionally: an attestation that
 // cannot name what it erased is an attestation of nothing.
 //
 // It previously read `(status.KeysDeleted ?? 0) == 0`, on the reasoning that a zero count is a claim
 // about ABSENCE and so needs no substantiation. That is wrong, and wrong in the dangerous direction.
 // Zero keys deleted does not mean there was nothing to erase -- it is the NORMAL case for a hard
 // deletion, where records were physically removed and no key was ever shredded. So the old condition
 // stamped Verified=true on every reconstructed hard-deletion certificate, substantiating nothing,
 // and the signature now covers that field -- making the false claim an authenticated one.
 Verification =
 new VerificationSummary
 {
 Verified = false,
 // None, NOT the configured methods. The configured list says which checks this deployment RUNS;
 // naming them on a certificate asserts they were run against this erasure, and on this path none
 // of them was. VerificationMethod.None is the enum's own word for "no verification performed",
 // so the document says what actually happened instead of borrowing the configuration's word for it.
 Methods = VerificationMethod.None,
 // The completion instant, NOT a fresh clock read. This path runs no verification -- the determination
 // above is read off the persisted status -- so it is as-of the state it describes. Stamping UtcNow here
 // would assert a verification took place at certificate-generation time, which nothing performed; and
 // the signature now covers this field, so that false claim would be an AUTHENTICATED one.
 VerifiedAt = completedAt,
 DeletedKeyIds = []
 },
 LegalBasis = status.LegalBasis,
 RetainUntil = completedAt.Add(_options.Value.Retention.CertificateRetentionPeriod)
 };

 var certificate = new ErasureCertificate
 {
 Payload = reconstructed,
 Signature = ErasureCertificateSigner.Sign(reconstructed, _options.Value.Retention.SigningKey)
 };

 await certStore.SaveCertificateAsync(certificate, cancellationToken).ConfigureAwait(false);

 LogErasureCertificateGenerated(certificate.Payload.CertificateId, requestId);

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

 // An executed request whose keys are waiting on the provider's destruction window is NOT executed again:
 // its erasure work is done, and a key already scheduled for destruction cannot be scheduled again (AWS
 // KMS rejects it; Azure Key Vault only permits purge or recover). What remains is confirmation, which
 // IErasureCompletionProcessor performs by asking the provider -- never by deleting anything.
 if (status.Status == ErasureRequestStatus.AwaitingKeyDestruction)
 {
 return RefuseReexecution(requestId);
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
 var holdCheck = await _legalHoldService.CheckHoldsAsync(
 status.DataSubjectIdHash, DataSubjectIdType.Hash, status.TenantId, cancellationToken)
.ConfigureAwait(false);

 if (holdCheck.ErasureBlocked)
 {
 _ = await _store.UpdateStatusAsync(requestId, ErasureRequestStatus.BlockedByLegalHold,
 "Legal hold active", cancellationToken).ConfigureAwait(false);
 return ErasureExecutionResult.Failed("Erasure blocked by active legal hold");
 }

 try
 {
 // Discover keys to delete via data inventory (use hash-based lookup)
 var keysToDelete = new List<string>();
 IReadOnlyList<DataLocation> discoveredLocations = [];
 DataInventory? discoveredInventory = null;
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

 // carried whole so the coverage gate can read the DECLARED obligations from it; an erasure
 // cannot be reported complete merely because nothing was discovered.
 discoveredInventory = inventory;

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

 // Keys the provider irreversibly SCHEDULED rather than destroyed. Each one still registers an error
 // (ExecuteKeyDeletionsAsync), so Completed stays unreachable here; they are collected separately only so
 // the outcome below can tell "correctly scheduled, awaiting the provider" from a genuine failure.
 var scheduledKeys = new ScheduledKeyDestructions();

 var deletedCount = await ExecuteKeyDeletionsAsync(keysToDelete, deletedKeyIds, scheduledKeys, errors, requestId, cancellationToken)
.ConfigureAwait(false);

 // Invoke erasure contributors (event stores, snapshot stores, etc.)
 var contributorResults = new List<ErasureContributorResult>();
 var totalRecordsAffected = await InvokeContributorsAsync(requestId, status, discoveredInventory, errors, contributorResults, cancellationToken)
.ConfigureAwait(false);

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
 // A location whose key is irreversibly scheduled is covered PENDING that destruction. Counting it as
 // uncovered here would report the same outstanding key twice -- once as the scheduled-key error, once as
 // an uncovered store -- and the second would read as a genuine failure. The scheduled-key error is what
 // keeps Completed unreachable until the provider confirms; nothing about that gate is relaxed here.
 var coverage = EvaluateCoverageGate(
 errors, discoveredLocations, scheduledKeys, deletedKeyIds, discoveredInventory, contributorResults);

 // Determine outcome. Completed is reachable ONLY when there are zero errors AND zero uncovered
 // locations — the structural invariant: a silent Completed over an uncovered store is inexpressible
 // because this branch is the only path that does NOT call RecordCompletionAsync.
 // Every error is a scheduled key and nothing else failed: this is not a partial failure but a correct
 // intermediate state -- the erasure is done except for the provider's own destruction window. Record it
 // as AwaitingKeyDestruction, which is revisited (IErasureCompletionProcessor) and reaches Completed only
 // once the provider confirms the keys are gone. ANY genuine error keeps the path below, unchanged.
 if (errors.Count > 0 && errors.Count == scheduledKeys.Count)
 {
 var awaiting = await RecordAwaitingKeyDestructionAsync(
 requestId, scheduledKeys, deletedCount, totalRecordsAffected, activity, cancellationToken).ConfigureAwait(false);
 ExecutionDurationHistogram.Record(executionStopwatch.Elapsed.TotalMilliseconds);
 return awaiting;
 }

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
		ScheduledKeyDestructions scheduledKeys,
		List<string> errors,
		Guid requestId,
		CancellationToken cancellationToken)
	{
		var deletedCount = 0;

		foreach (var keyId in keysToDelete)
		{
			try
			{
				// Revoke BEFORE destroy, never the reverse. A crash between the two steps must
				// leave the SAFER partial state -- escrow revoked with the active key still live is
				// recoverable and visible; the active key destroyed with escrow silently unrevoked is a
				// subject who believes their data is gone while a recoverable copy remains. On a revoke
				// failure, skip the destroy this pass (folds into the tri-state gate below via `errors`,
				// so Completed stays unreachable) -- a retry re-attempts revoke-then-destroy in order.
				if (_keyEscrowService is not null)
				{
					try
					{
						_ = await _keyEscrowService.RevokeEscrowAsync(
							keyId, "GDPR erasure: subject key destroyed", cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						errors.Add(
							$"Failed to revoke escrow for key '{keyId}': {ex.Message} -- the key was NOT "
							+ "destroyed this pass, because destroying it now would leave an escrowed spare "
							+ "recoverable after the subject's data is attested erased.");
						continue;
					}
				}

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
						scheduledKeys.Add(keyId, outcome.IrreversibleAt);
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

 // THE ANNOTATION-SIDE TWIN OF THE "COVERAGE MUST BE ESTABLISHED" ARM BELOW, and it was missing.
 // UncoveredAnnotatedCategories is derived SOLELY from the categories the scan returned, so a category
 // the scan never saw is absent from that set for exactly the same reason a properly covered one is.
 // There was no state meaning "the scan was incomplete", which made a narrowed scan and a clean one the
 // same observation -- and an empty annotated set is precisely what a trimmed host produces. An absence
 // of evidence was therefore read as evidence of coverage, which is the one inference a GDPR erasure
 // certificate must never make.
 if (!coverage.AnnotationScanEstablished && !_options.Value.KeyShredOnlyErasure)
 {
 errors.Add(
 "GDPR erasure coverage is UNESTABLISHED: the [PersonalData] annotation scan could not be "
 + "completed, so the annotated categories are a lower bound and a category the scan never observed "
 + "is indistinguishable from one that is covered. Erasure is not Completed on an unestablished "
 + "scan. This is expected on a trimmed or ahead-of-time host, where whole-domain reflection cannot "
 + "see what the trimmer removed. Run the erasure on a host where reflection over the domain model "
 + "is available -- this is an administrative compliance path, not a hot path -- or restrict the "
 + "host to crypto-shred-only erasure, where coverage is established by key destruction rather than "
 + "by locating annotated data.");
 }

 // A registered obligation nobody reported erasing. Judged on what contributors SAY THEY ERASED,
 // not on what was discovered: "we found no rows in that table" and "we never looked in it" are
 // the same observation from the discovered set, and only one of them is erasure.
 if (coverage.OutstandingDeclaredLocations.Count > 0)
 {
 errors.Add(
 "Registered data location(s) were not reported erased by any contributor: "
 + $"{string.Join(", ", coverage.OutstandingDeclaredLocations)} - erasure is not Completed while a "
 + "registered obligation is outstanding. A contributor must report the table-and-field pairs it "
 + "erased; reporting success without naming them discharges nothing.");
 }

  // COVERAGE MUST BE ESTABLISHED, NOT MERELY UNCONTRADICTED. A discovery source is wired and the
 // registry declares NOTHING, so there is no obligation to check an erasure against and a certificate
 // would attest to an absence of evidence. That is this bead's original defect: Completed over zero
 // verified coverage.
 //
 // THIS ARM WAS WITHDRAWN ONCE AND IS BACK DELIBERATELY. What was withdrawn was its PREDICATE, not the
 // requirement. It used to read a SUBJECT-SCOPED, id-type-filtered slice of the registry, which the
 // caller asked for by hash while every shipped registration uses a natural id type -- so the slice was
 // permanently empty and this arm refused EVERY erasure in EVERY configuration. The declared set now
 // reads the whole tenant registry, which is the fact this message always claimed.
 //
 // ASKED HERE RATHER THAN AT STARTUP, and the distinction is the whole placement question: a registry
 // is runtime data, so a clean install is empty BY DEFINITION and refusing to boot would deadlock --
 // the host cannot start in order to run the registration that would let it start. A first boot and a
 // misconfigured host are indistinguishable at startup and trivially distinct HERE, because a first
 // boot has not requested an erasure. Refusing at the point the claim is made refuses a CLAIM, not a
 // host. The startup check remains, logging the same condition without deciding anything.
 if (_dataInventoryService is not null
 	&& !coverage.AnyLocationDeclared
 	&& !_options.Value.KeyShredOnlyErasure)
 {
 	errors.Add(
 		"GDPR erasure coverage is UNESTABLISHED: no data location is registered, so there is nothing to "
 		+ "verify this erasure against. Register the tables and fields that hold personal data with "
 		+ "RegisterDataLocationAsync. A completion certificate is not issued over an empty registry, "
 		+ "because an empty registry is an absence of evidence and not a proof of erasure. "
 		+ "ErasureOptions.KeyShredOnlyErasure = true accepts key-destruction-only erasure instead: the "
   + "per-subject key is shredded and coverage is established by that destruction rather than by "
   + "a registry.");
 }
	}

	/// <summary>
	/// Runs every registered erasure contributor, recording failures in <paramref name="errors"/> and each success in
	/// <paramref name="contributorResults"/>. Returns the records affected by the successful contributors.
	/// </summary>
	private async Task<int> InvokeContributorsAsync(
		Guid requestId,
		ErasureStatus status,
		DataInventory? inventory,
		List<string> errors,
		List<ErasureContributorResult> contributorResults,
		CancellationToken cancellationToken)
	{
 var totalRecordsAffected = 0;

 foreach (var contributor in _contributors)
 {
 try
 {
				// The routing lives in DeclaredObligations: a contributor is offered only the declared pairs whose
				// registered store kind it covers, so it names what it erased rather than guessing. A pair with no
				// registered kind reaches nobody and stays outstanding -- silence fails closed.
				

 var contributorResult = await DeclaredObligations.EraseAsync(requestId, status, inventory, contributor, cancellationToken)
.ConfigureAwait(false);

 if (contributorResult.Success)
 {
 totalRecordsAffected += contributorResult.RecordsAffected;

 // Kept so the coverage gate can read what this contributor reported erasing. A
 // contributor that reports success without naming anything discharges nothing, which
 // leaves every declared obligation outstanding -- silence fails closed.
 contributorResults.Add(contributorResult);

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

 return totalRecordsAffected;
	}

	private CoverageOutcome EvaluateCoverageGate(
		List<string> errors,
		IReadOnlyList<DataLocation> locations,
		ScheduledKeyDestructions scheduledKeys,
		List<string> deletedKeyIds,
		DataInventory? inventory,
		List<ErasureContributorResult> contributorResults)
	{
		var coverage = EvaluateCoverage(locations, scheduledKeys.WithDestroyed(deletedKeyIds), inventory, contributorResults);
		AppendCoverageGateErrors(errors, coverage);
		return coverage;
	}

	private CoverageOutcome EvaluateCoverage(
 IReadOnlyList<DataLocation> locations,
 IReadOnlyCollection<string> deletedKeyIds,
 DataInventory? inventory,
 IEnumerable<ErasureContributorResult> contributorResults) =>
 ErasureCoverageEvaluator.Evaluate(
 locations,
 deletedKeyIds,
 _contributors,
 _annotationSource.GetAnnotatedCategories(),
 inventory,
 contributorResults);

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
 CancellationToken cancellationToken,
 IReadOnlyList<string>? warnings = null,
 Guid? certificateIdOverride = null)
	{
 var certificateId = certificateIdOverride ?? Guid.NewGuid();

 // Some stores do not support certificate operations; record the ID without an eager certificate.
 if (_store.GetService(typeof(IErasureCertificateStore)) is not IErasureCertificateStore certStore)
 {
 return certificateId;
 }

 var completedAt = DateTimeOffset.UtcNow;

 // The one thing this path can positively establish: the key-management admin reported each of these
 // ids irrecoverable. Read once so every claim derived from it agrees with the others.
 var substantiatedByKeyDestruction = deletedKeyIds.Count > 0;

 // Build the payload FIRST, then sign it whole. The signature covers every claim below by construction:
 // there is no field list here to fall out of step with the type.
 var payload = new ErasureCertificatePayload
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
 // Verified states the ONE claim this execution can positively establish: every id in
 // DeletedKeyIds came back KeyDestructionState.Completed -- irrecoverable NOW -- from the
 // key-management admin. So it is true when the certificate names at least one destruction the
 // framework itself confirmed, and false otherwise.
 //
 // It previously read `deletedKeyIds.Count == keysDeleted`, which COULD NEVER BE FALSE: the two
 // are incremented in the same branch of ExecuteKeyDeletionsAsync, one line apart, so the
 // comparison restates an assignment rather than testing anything. A flag that cannot go false is
 // not a check, and on a regulator-facing document it is a claim nothing stands behind.
 //
 // FALSE IS NOT "THE ERASURE FAILED" -- an erasure reaches here only with the whole coverage gate
 // clean. False means the framework verified nothing itself, which is the ordinary outcome of an
 // erasure discharged entirely by record deletion: contributors reported erasing rows and the
 // framework recorded their reports. It took their word; it did not confirm anything.
 //
 // Verified and Methods are derived from ONE value so they cannot disagree. A certificate reading
 // "Verified = true, Methods = None" -- verified by nothing -- is thereby inexpressible rather
 // than merely unlikely.
 Verified = substantiatedByKeyDestruction,
 // The method that ACTUALLY substantiated this erasure, not the configured list. The configured
 // value says which checks this deployment is set up to run; naming it on a certificate asserts it
 // was run against THIS erasure. What ran here is key destruction through the key-management admin,
 // and it ran only if it collected ids -- so an execution that substantiated nothing says None
 // rather than borrowing the configuration's word for it.
 Methods = substantiatedByKeyDestruction
 ? VerificationMethod.KeyManagementSystem
 : VerificationMethod.None,
 VerifiedAt = completedAt,
 DeletedKeyIds = deletedKeyIds,
 Warnings = warnings ?? []
 },
 LegalBasis = status.LegalBasis,
 // enumerate exemptions (e.g. audit store) with their legal basis — explicit, never silent.
 Exceptions = exemptions,
 RetainUntil = completedAt.Add(_options.Value.Retention.CertificateRetentionPeriod)
 };

 var certificate = new ErasureCertificate
 {
 Payload = payload,
 Signature = ErasureCertificateSigner.Sign(payload, _options.Value.Retention.SigningKey)
 };

 await certStore.SaveCertificateAsync(certificate, cancellationToken).ConfigureAwait(false);
 return certificateId;
	}

	/// <summary>
	/// Confirms, with the key-management provider, that every key of a request in
	/// <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/> has been destroyed and, only if so, completes the
	/// request and issues its certificate through the same path an immediate erasure uses.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This never deletes, schedules, or re-executes anything. It asks <paramref name="verifier"/> about each key;
	/// the provider is the authority on whether the key is gone. The instant the provider reported when the key was
	/// scheduled is not consulted at all, so a clock that has passed it can never stand in for a destruction the
	/// provider has not confirmed. One key still recoverable leaves the request exactly as it was.
	/// </para>
	/// <para>
	/// The keys checked are the request's per-subject key plus every key the data inventory associates with the
	/// subject's discovered locations -- a superset of the keys execution destroyed or scheduled, so a key cannot
	/// escape confirmation by having been scheduled.
	/// </para>
	/// </remarks>
	/// <returns><see langword="true"/> if this call moved the request to <see cref="ErasureRequestStatus.Completed"/>.</returns>
	internal async Task<bool> ConfirmKeyDestructionAsync(
		Guid requestId,
		IErasureVerificationService verifier,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(verifier);

		var status = await _store.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false);
		if (status is null || status.Status != ErasureRequestStatus.AwaitingKeyDestruction)
		{
			return false;
		}

		var keyIds = new List<string> { status.DataSubjectIdHash };
		IReadOnlyList<DataLocation> locations = [];
		DataInventory? inventory = null;
		if (_dataInventoryService is not null)
		{
			inventory = await _dataInventoryService.DiscoverAsync(
				status.DataSubjectIdHash, DataSubjectIdType.Hash, status.TenantId, cancellationToken)
				.ConfigureAwait(false);
			locations = inventory.Locations;

			// Locations are read as well as AssociatedKeys, because discovery only associates a key it can still
			// look up -- and a key deleted but recoverable may not be found, which would drop it from the set
			// this method has to confirm.
			foreach (var keyId in inventory.Locations.Select(static l => l.KeyId)
				.Concat(inventory.AssociatedKeys.Select(static k => k.KeyId)))
			{
				if (!string.IsNullOrEmpty(keyId) && !keyIds.Contains(keyId))
				{
					keyIds.Add(keyId);
				}
			}
		}

		foreach (var keyId in keyIds)
		{
			if (!await verifier.VerifyKeyDeletionAsync(keyId, cancellationToken).ConfigureAwait(false))
			{
				LogErasureKeyDestructionNotYetConfirmed(requestId, keyId);
				return false;
			}
		}

		// Exemptions are re-derived for the certificate (they depend only on the discovered locations and the
		// registered contributors' declared coverage), so data retained under a legal basis is still enumerated
		// on it. The rest of the coverage gate passed at execution -- that is the precondition for entering
		// AwaitingKeyDestruction -- and is not re-run, because the contributors' reports are not re-run either.
		var exemptions = EvaluateCoverage(locations, keyIds, inventory, []).Exemptions;
		var recordsAffected = status.RecordsAffected ?? 0;

		// The certificate id is DERIVED from the request, so the certificate insert is the claim: two confirmers
		// racing on the same request (two scheduler instances, or a scheduler and a serverless trigger) produce one
		// certificate, not two. The loser's insert is refused as a duplicate and it adopts the winner's certificate;
		// a confirmer that crashed after the insert and before recording completion is finished the same way.
		var certificateId = ScheduledKeyDestructions.ConfirmationCertificateId(requestId);
		try
		{
			_ = await PersistCompletionCertificateAsync(
				requestId,
				status,
				keyIds,
				keyIds.Count,
				recordsAffected,
				exemptions,
				cancellationToken,
				[
					"Completed after the key-management provider confirmed destruction of keys it had scheduled for "
					+ "irreversible deletion. Records erased by erasure contributors when the request was executed are "
					+ "not re-counted at confirmation; the execution-time counts are recorded on the request.",
				],
				certificateId).ConfigureAwait(false);
		}
		catch (DuplicateErasureCertificateException)
		{
			// Another confirmer issued this request's certificate first; record completion against it.
		}

		await _store.RecordCompletionAsync(requestId, keyIds.Count, recordsAffected, certificateId, cancellationToken)
			.ConfigureAwait(false);

		LogErasureKeyDestructionConfirmed(requestId, keyIds.Count);
		RequestsCompletedCounter.Add(1);
		return true;
	}

	private ErasureExecutionResult RefuseReexecution(Guid requestId)
	{
		LogErasureExecutionRefusedAwaitingDestruction(requestId);
		return ErasureExecutionResult.Failed(
			"Request is awaiting the key-management provider's destruction of its scheduled keys; it is not "
			+ "re-executed. It completes when the provider confirms destruction.");
	}

	private async Task<ErasureExecutionResult> RecordAwaitingKeyDestructionAsync(
		Guid requestId,
		ScheduledKeyDestructions scheduledKeys,
		int deletedCount,
		int recordsAffected,
		Activity? activity,
		CancellationToken cancellationToken)
	{
		var detail = scheduledKeys.Describe(deletedCount, recordsAffected);

		_ = await _store.UpdateStatusAsync(requestId, ErasureRequestStatus.AwaitingKeyDestruction, detail, cancellationToken)
			.ConfigureAwait(false);

		LogErasureAwaitingKeyDestruction(requestId, scheduledKeys.Count, detail);
		KeysDeletedCounter.Add(deletedCount);
		activity?.SetTag("erasure.keys_deleted", deletedCount);
		activity?.SetTag("erasure.keys_awaiting_destruction", scheduledKeys.Count);
		activity?.SetTag("erasure.records_affected", recordsAffected);
		activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "awaiting_key_destruction");

		return ErasureExecutionResult.PartiallySucceeded(deletedCount, recordsAffected, detail);
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

	[LoggerMessage(
 ComplianceEventId.ErasureExecutionRefusedAwaitingDestruction,
 LogLevel.Information,
 "Erasure request {RequestId} is awaiting key destruction and was not re-executed")]
	private partial void LogErasureExecutionRefusedAwaitingDestruction(Guid requestId);

	[LoggerMessage(
 ComplianceEventId.ErasureAwaitingKeyDestruction,
 LogLevel.Information,
 "Erasure request {RequestId} executed; {ScheduledKeyCount} key(s) await the provider's irreversible destruction. {Detail}")]
	private partial void LogErasureAwaitingKeyDestruction(Guid requestId, int scheduledKeyCount, string detail);

	[LoggerMessage(
 ComplianceEventId.ErasureKeyDestructionNotYetConfirmed,
 LogLevel.Debug,
 "Erasure request {RequestId} still awaits destruction: key {KeyId} is not confirmed destroyed by the provider")]
	private partial void LogErasureKeyDestructionNotYetConfirmed(Guid requestId, string keyId);

	[LoggerMessage(
 ComplianceEventId.ErasureKeyDestructionConfirmed,
 LogLevel.Information,
 "Erasure request {RequestId} completed: the provider confirmed {KeyCount} key(s) destroyed")]
	private partial void LogErasureKeyDestructionConfirmed(Guid requestId, int keyCount);
}
