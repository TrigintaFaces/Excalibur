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
[SuppressMessage(
	"Design",
	"CA1506:AvoidExcessiveClassCoupling",
	Justification =
		"NAMED AS A TRADE, not waved through. This class orchestrates the whole erasure lifecycle -- "
		+ "discovery, legal holds, key deletion, contributors, the coverage gate, certificates and "
		+ "verification -- so it necessarily touches every type in those subsystems, and it sat at the "
		+ "budget before the partial-certificate work. That work is a compliance correctness fix: "
		+ "without it a consumer with any persisted projection can never obtain an erasure certificate "
		+ "at all. Splitting a 1000-line compliance orchestrator days before a release cut is the "
		+ "larger risk, so the coupling is accepted and the split is tracked as debt. The construction "
		+ "of the certificate's unreached-data record was extracted to UnreachedDataFactory rather than "
		+ "added here, which is why this is +2 and not +5.")]
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

	/// <summary>
	/// How long the terminal outcome write is given, on a token of its OWN rather than the caller's.
	/// </summary>
	/// <remarks>
	/// <b>A write whose purpose is to record what happened must not be cancellable by the same token that
	/// stopped the work.</b> On a graceful shutdown the caller's token is ALREADY cancelled by the time the
	/// handler below runs, and both SQL stores open their connection with <c>OpenAsync(cancellationToken)</c>
	/// -- so the outcome write throws instead of landing, the request stays claimed, and nothing downstream
	/// ever learns that the erasure stopped half-way. Bounded rather than unbounded because this runs inside
	/// the host's shutdown budget: an unbounded wait here would hang the shutdown it is trying to report on.
	/// </remarks>
	private static readonly TimeSpan OutcomeWriteBudget = TimeSpan.FromSeconds(5);

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
	private readonly IErasureRetentionRegistry _retentions;

	/// <summary>
	/// Decides whether this instance holds every retention declaration the deployment has recorded.
	/// </summary>
	/// <remarks>
	/// SCOPED WITH THIS SERVICE, so its refusal cache lives for one execution -- which is what dedupes
	/// the pre-claim and pre-destruction reads into one. The per-PROCESS advance is NOT done here:
	/// the scheduler owns it, against the singleton registration, because an advance fired from the
	/// executor would begin protecting other instances only at this instance's FIRST ERASURE, which
	/// in a low-volume deployment is days after the deploy that created the hazard.
	/// </remarks>
	private readonly ErasureRetentionStalenessGate _retentionStaleness;

	/// <summary>
	/// The write-ahead record of every key handle minted for a data subject.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>It is what makes the destruction set COMPLETE rather than DERIVED, and that distinction is the
	/// whole reason it exists.</b> Every other source of handles on this path is a re-derivation: the
	/// declared retentions produce the handles a declaration implies, and the data inventory produces the
	/// handles a consumer registered. Both answer "which handles SHOULD exist", and a handle either of them
	/// omits is a key nothing destroys -- a subject reported erased whose personal data is still readable,
	/// with nothing downstream able to learn otherwise.
	/// </para>
	/// <para>
	/// This ledger answers the different question: <b>which handles the write path actually minted.</b> It
	/// is written ahead of each mint, so a handle can appear here with no key behind it (which the
	/// destruction loop handles -- the provider answers not-found and nothing is attested from the absence)
	/// but a key can never exist with no row here.
	/// </para>
	/// </remarks>
	private readonly IKeyMintLedger _mintLedger;

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
	/// <param name="retentionLedger">
	/// The deployment-wide record of declared retentions, or <see langword="null"/> when the configured
	/// store provides none. With none this instance cannot be behind another, so the staleness gate
	/// passes everything -- correct for a single-process deployment rather than blind.
	/// </param>
	/// <param name="retentions">
	/// The aggregate types this deployment declares it must keep through an erasure, or <see langword="null"/>
	/// when it declares none. Their keys are excluded from destruction, because a retained record whose key
	/// was destroyed is present and unreadable — which discharges neither obligation.
	/// </param>
	/// <param name="mintLedger">
	/// The write-ahead record of every key handle minted for a data subject, which this erasure enumerates
	/// to find the handles it must decide about. <b>Required.</b> Every other source of handles here is a
	/// re-derivation — the declared retentions and the data inventory each answer which handles SHOULD
	/// exist — and a handle neither of them implies is a key nothing destroys, so the subject's personal
	/// data stays readable under a signed completion. A deployment that performs no erasure registers the
	/// no-erasure ledger, which refuses enumeration loudly rather than answering "no handles".
	/// </param>
	public ErasureService(
 IErasureStore store,
 IKeyManagementAdmin keyAdmin,
 IOptions<ErasureOptions> options,
 ILogger<ErasureService> logger,
 IDataSubjectHasher dataSubjectHasher,
 ILegalHoldService legalHoldService,
 IDataInventoryService? dataInventoryService,
 IKeyEscrowService? keyEscrowService,
 IErasureRetentionRegistry retentions,
 IKeyMintLedger mintLedger,
 IEnumerable<IErasureContributor>? contributors = null,
 IErasureRetentionDeclarationLedger? retentionLedger = null)
: this(store, keyAdmin, options, logger, dataSubjectHasher, legalHoldService, dataInventoryService,
 keyEscrowService, IPersonalDataAnnotationSource.CreateDefault(), retentions, mintLedger, contributors, retentionLedger)
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
 IErasureRetentionRegistry retentions,
 IKeyMintLedger mintLedger,
 IEnumerable<IErasureContributor>? contributors = null,
 IErasureRetentionDeclarationLedger? retentionLedger = null)
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
		_retentions = retentions ?? throw new ArgumentNullException(nameof(retentions));
		_retentionStaleness = new ErasureRetentionStalenessGate(retentions, retentionLedger, logger);

		// REQUIRED, never nullable, and for the same reason the retention registry is: an ABSENT ledger is
		// not a failure but a DIFFERENT ANSWER. A null-tolerant field would make "this subject has no
		// handles" the reading of a mis-composition, so the erasure would destroy only what it can derive
		// and then sign a completion certificate over whatever it missed. A deployment that keeps no mint
		// rows says so by registering the no-erasure ledger, which THROWS when enumerated -- loud at the
		// first erasure rather than silent at every one.
		_mintLedger = mintLedger ?? throw new ArgumentNullException(nameof(mintLedger));
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

 // The same refusal at acceptance time, so a request that cannot be honoured is declined when it is
 // made rather than accepted and failed later. Identical reasoning to the execute path.
 if (holdCheck.IsPartiallyBlocked && holdCheck.ActiveHolds.Count > 0)
 {
  RequestsBlockedCounter.Add(1, new TagList { { ErasureTelemetryConstants.Tags.Scope, request.Scope.ToString() } });
  activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "blocked");
  var partialHold = holdCheck.ActiveHolds[0];
  LogErasureRequestBlocked(request.RequestId, partialHold.HoldId);
  return ErasureResult.Blocked(request.RequestId, partialHold);
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

 // A PARTIALLY completed erasure still produces a certificate, and refusing one was a defect
 // rather than caution. What the consumer otherwise gets is ErrorMessage: an unsigned, unstructured,
 // mutable string on a status row, with no retention and no canonical form. Refusing the certificate
 // does not make the failure legible -- it destroys the only durable, signed record that the work
 // which DID happen happened, and the request is terminal, so it can never be produced later.
 //
 // Failed is still refused. There, nothing succeeded, so there is nothing to attest.
 if (status.Status is not (ErasureRequestStatus.Completed or ErasureRequestStatus.PartiallyCompleted))
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

 // A PARTIAL erasure is evidenced by the certificate written on the execution path, and ONLY by it.
 // The reconstruction below rebuilds from persisted status, which keeps counts and one joined error
 // string -- it cannot name which store kind went unreached, so a certificate reconstructed here
 // would attest a partial erasure while being silent about the part that did not happen. That is the
 // dishonesty this whole change exists to remove, arriving from the other direction.
 if (status.Status == ErasureRequestStatus.PartiallyCompleted)
 {
 throw new InvalidOperationException(string.Format(
 CultureInfo.CurrentCulture,
 CannotGenerateCertificateFormat,
 requestId,
 status.Status));
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

 // Issued through the same boundary the execution path uses, so this payload's claims are checked by the
 // same code rather than by a second reading of the same rules. There is no errors collection to fold the
 // findings into here -- this path reconstructs a request the store already recorded as Completed, and
 // that determination is not this method's to revise -- so an unestablished claim is logged loudly
 // instead. It cannot be silent: a reconstructed certificate is the only evidence a consumer will ever
 // get for this request.
 var certificate = ErasureCertificateSigner.Issue(
 reconstructed, _options.Value.Retention.SigningKey, out var unestablishedClaims);

 if (unestablishedClaims.Count > 0)
 {
 LogCertificateClaimsNotEstablished(requestId, string.Join("; ", unestablishedClaims));
 }

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
 return ErasureExecutionResult.Refused("Request not found");
 }

 // An executed request whose keys are waiting on the provider's destruction window is NOT executed again:
 // its erasure work is done, and a key already scheduled for destruction cannot be scheduled again (AWS
 // KMS rejects it; Azure Key Vault only permits purge or recover). What remains is confirmation, which
 // IErasureCompletionProcessor performs by asking the provider -- never by deleting anything.
 if (status.Status == ErasureRequestStatus.AwaitingKeyDestruction)
 {
 return RefuseReexecution(requestId);
 }

 // A LAPSED IN-PROGRESS REQUEST IS A RECOVERY, NOT AN ERROR, and admitting it here is what gives the
 // claim lease a consumer. The lease made an interrupted erasure re-CLAIMABLE and the discovery
 // predicate made it re-OFFERED; this guard refused it on arrival, so it was offered on every poll and
 // executed on none -- the request sat in-progress forever with its keys possibly destroyed, which is
 // the stranding the lease exists to end, surviving one level above the lease.
 //
 // WHETHER THE REQUEST IS ACTUALLY AVAILABLE IS NOT DECIDED HERE. This status was read a moment ago and
 // carries no custody, so a test against it would be a read followed by a write with a window between
 // them. The STORE decides, in one indivisible step: it tests custody before status and grants only
 // when no live claim exists. So a healthy in-flight run answers AlreadyClaimed and is left alone, and
 // only a lapsed one is recovered.
 var expectedStatus = status.Status switch
 {
 ErasureRequestStatus.Scheduled => ErasureRequestStatus.Scheduled,
 ErasureRequestStatus.InProgress => ErasureRequestStatus.InProgress,
 _ => (ErasureRequestStatus?)null,
 };

 if (expectedStatus is null)
 {
 return ErasureExecutionResult.Refused($"Invalid status: {status.Status}");
 }

 // Atomically transition to InProgress FIRST — if another caller already claimed this request, abort.
 // This prevents concurrent execution and establishes exclusive ownership before any further checks.
 // THE PRE-CLAIM STALENESS GATE. This is a LIVENESS device, not the safety mechanism: the record only
 // grows, so this read observes a SUBSET of what the pre-destruction read observes and can only ever
 // refuse LESS. Its value is that it refuses BEFORE the claim, so the request stays Scheduled and any
 // current instance takes it on the next poll -- where a refusal AFTER the claim would have nowhere to
 // put the request, because the store will not return a claimed request to Scheduled and nothing
 // enumerates a new status.
 //
 // It also runs before the hold re-check below, so in a fully stale deployment a held request stays
 // Scheduled rather than reaching the deferred status -- the better of the two, since Scheduled is
 // re-polled unconditionally.
 var preClaimRefusal = await _retentionStaleness.DescribeRefusalAsync(status.TenantId, cancellationToken)
.ConfigureAwait(false);

 if (preClaimRefusal is not null)
 {
 return ErasureExecutionResult.Refused(preClaimRefusal);
 }

 // TAKING CUSTODY, which is now a different act from advancing the status. The store mints an
 // identity and records it with a server-side expiry in the SAME indivisible step that moves the
 // request out of Scheduled, so exactly one of N callers is granted. Every attestation write below
 // presents that identity, and the store refuses any that another claim has superseded.
 //
 // The identity never leaves this method: it is not on the result, not on the status and not on the
 // wire. That is what makes it impossible for anything else to present one, and therefore
 // impossible for anything to compare two of them for order.
 var claim = await _store.ClaimForExecutionAsync(
 		requestId,
 		expectedStatus.Value,
 		_options.Value.Execution.ClaimDuration,
 		cancellationToken)
 	.ConfigureAwait(false);

 if (claim.Outcome != ErasureClaimOutcome.Granted)
 {
 	// Tested BY NAME rather than by excluding the refusals, so an outcome added later cannot
 	// silently begin to mean "go ahead".
 	return ErasureExecutionResult.Refused(claim.Outcome switch
 	{
 		ErasureClaimOutcome.AlreadyClaimed =>
 			"Another instance holds this request. It returns on its own when that claim lapses.",
 		ErasureClaimOutcome.StatusMismatch =>
 			"Request is no longer in Scheduled status (concurrent execution detected)",
 		ErasureClaimOutcome.NotFound => "No such erasure request exists for this tenant.",
 		_ => "The store did not grant custody of this request and did not say why.",
 	});
 }

 var claimIdentity = claim.ClaimIdentity!;

 // NO EXECUTION DEADLINE, AND THE ABSENCE IS DELIBERATE. A lease-derived deadline was built here and
 // removed: the ruling it implemented -- "the lapse must cause the owner to stop" -- does not survive
 // contact with this loop.
 //
 // Three measured reasons, in the order they bite. The per-key handler below catches every exception,
 // OperationCanceledException included, and CONTINUES to the next key, so a deadline never stopped the
 // owner at all; it only converted each remaining key into a spurious "failed to delete" error and
 // drove the request terminal. It could fire between a provider destroy and the ledger write that
 // records it, losing the record of an irreversible act -- a cancellation is not a safety device once a
 // pass has destroyed something. And it bought nothing: the fence already makes a late write safe,
 // because a superseded identity is refused and a lapse does not revoke, so a slow holder is harmless
 // while a STOPPED holder is the one state nothing can repair.
 //
 // What the lapse needs to cause is a SUCCESSOR, not a stop, and that is the entry guard above.
 // If a deadline is wanted later it belongs at the TOP of the destruction loop, between keys, where no
 // irreversible act is waiting for its record -- and the claim must return its expiry rather than have
 // a caller re-derive it from a local clock.

 // Re-check legal holds AFTER the atomic InProgress transition (TOCTOU fix: tightens the window
 // by ensuring we own the request exclusively before checking holds, and check holds immediately
 // before executing erasure operations)
 var holdCheck = await _legalHoldService.CheckHoldsByHashAsync(
 status.DataSubjectIdHash, status.TenantId, cancellationToken)
.ConfigureAwait(false);

 if (holdCheck.ErasureBlocked)
 {
 ReportRefusedOutcomeWrite(
 	requestId,
 	await _store.UpdateStatusAsync(
 		requestId,
 		ErasureRequestStatus.BlockedByLegalHold,
 		expectedStatus: null,
 		"Legal hold active",
 		claimIdentity,
 		cancellationToken).ConfigureAwait(false),
 	"a legal hold blocking this erasure");
 return ErasureExecutionResult.Refused("Erasure blocked by active legal hold");
 }

 // A PARTIAL legal hold names categories that must be RETAINED, and neither mechanism this service
 // runs can exclude a category from destruction.
 //
 // The per-subject key is queued unconditionally below and destroyed BEFORE any contributor runs, and
 // it is scoped to the SUBJECT rather than to a category -- so destroying it takes every annotated
 // field of theirs, including the ones the hold exists to keep. The event-store pass that follows is
 // whole-aggregate. Proceeding would destroy data the controller has asserted a legal basis to retain,
 // and would report success.
 //
 // Refusing is the conservative direction: withholding an erasure is recoverable by releasing or
 // narrowing the hold, and destroying held records is not. The hold carries basis, case reference and
 // description, which is what makes the refusal auditable and why the hold rather than an inferred
 // annotation is the authority read here.
 if (holdCheck.IsPartiallyBlocked)
 {
  var retained = string.Join(", ", holdCheck.ExemptCategories);
  ReportRefusedOutcomeWrite(
  	requestId,
  	await _store.UpdateStatusAsync(
  		requestId,
  		ErasureRequestStatus.BlockedByLegalHold,
  		expectedStatus: null,
  		$"Legal hold retains specific categories ({retained})",
  		claimIdentity,
  		cancellationToken).ConfigureAwait(false),
  	"a legal hold retaining specific categories");
  return ErasureExecutionResult.Refused(
  	$"An active legal hold requires these data categories to be retained: {retained}. Erasure here "
  	+ "destroys the data subject's encryption key, which is scoped to the subject and not to a "
  	+ "category, and tombstones whole aggregates, so neither step can exclude a retained category. "
  	+ "Nothing was erased. Release or narrow the hold, or erase the subject where the retained "
  	+ "categories are stored separately.");
 }

 try
 {
 // Discover keys to delete via data inventory (use hash-based lookup)
 var keysToDelete = new List<string>();
 IReadOnlyList<DataLocation> discoveredLocations = [];
 DataInventory? discoveredInventory = null;

 // Handles the erasure MUST NOT destroy, because the aggregate types behind them are declared as
 // legally required to survive. Destroying one would leave the retained record present but
 // unreadable, which is the worse of the two failures: the obligation to keep the data is not
 // discharged by keeping ciphertext nobody can open.
 //
 // Derived here rather than injected, from the same options the registry reads, so there is one
 // declaration and not two. Empty for every deployment that declares no retention, which is what
 // makes this loop a no-op for them.
 // Only the retentions belonging to THIS erasure's tenant. A declaration made for another tenant is
 // another controller's obligation, and honouring it here would withhold erasure from a subject whose
 // own controller has no such duty -- over-retention, which is the Article 17 breach in the other
 // direction.
 //
 // THE HANDLE IS RECORDED HERE, ON THE PATH THAT DECIDES TO SPARE IT, and that placement is the point
 // rather than a convenience. A handle list assembled by a later pass can disagree with what was
 // actually excluded, and then the consumer destroys the wrong key or misses one. These two
 // collections are one iteration, so they cannot disagree: every handle the guard below spares is a
 // handle the certificate names, and nothing else is.
 //
 // THE TENANT IS COMPARED ONLY HERE, and only between two values of the same kind: the tenant this
 // erasure REQUEST recorded, and the tenant a DECLARATION names. Both are consumer-supplied terms on
 // erasure-domain objects, and the registry collapses every spelling of "untenanted" onto one before
 // comparing, so a single-tenant deployment matches its own declarations. No ambient identity takes part
 // HERE either: the tenant used below is the one THIS REQUEST recorded, read from the status rather than
 // from whatever scope the background processor running the erasure happens to have. An erasure is filed by
 // one actor and executed later by another, so ambient state is never authoritative for it -- and on this
 // path a wrong tenant means deriving a handle nothing was ever written under, which destroys nothing while
 // reporting a completed erasure.
 var requestingTenant = SubjectKeyHandle.OwningTenant(status.TenantId);
 var subjectKeyHandle = SubjectKeyHandle.ForSubjectHash(
 requestingTenant, status.DataSubjectIdHash, _dataSubjectHasher);

 var retainedKeyHandles = new HashSet<string>(StringComparer.Ordinal);
 var retainedHandleByType = new Dictionary<string, string>(StringComparer.Ordinal);

 // Every DECLARED aggregate type's widened handle for this subject, whoever declared it. The write path
 // widens on the type alone, so a handle exists here for a type declared by ANOTHER tenant -- and that
 // handle holds this subject's data under no obligation at all. Destroying it is required, not optional:
 // leaving it is a key nothing destroys, which is a subject never erased.
 var declaredHandleByType = new Dictionary<string, string>(StringComparer.Ordinal);

 foreach (var retention in _retentions.Declared)
 {
 	if (string.IsNullOrWhiteSpace(retention.AggregateType))
 	{
 		continue;
 	}

 	var handle = subjectKeyHandle.Retained(retention.AggregateType).Value;
 	declaredHandleByType[retention.AggregateType] = handle;

 	// THE HANDLE IS RECORDED ON THE PATH THAT DECIDES TO SPARE IT, and that placement is the point
 	// rather than a convenience. A handle list assembled by a later pass can disagree with what was
 	// actually excluded, and then the consumer destroys the wrong key or misses one.
 	// TWO FACTS, AND THIS LINE READS THE RIGHT ONE. The join is against the PARTITION a declaration
 	// names; :572 above derives the key handle from status.TenantId, the OWNER the data was encrypted
 	// under. They were one field, and a single-tenant deployment's owner (the ambient default identity)
 	// never equalled the partition its declaration named (untenanted), so this join missed and the loop
 	// below crypto-shredded a legally retained key while the certificate reported the record as kept.
 	if (_retentions.TryGetRetention(status.TenantPartition, retention.AggregateType, out _))
 	{
 		_ = retainedKeyHandles.Add(handle);
 		retainedHandleByType[retention.AggregateType] = handle;
 	}
 }

 // A widened handle NO declaration spares for this erasure is destroyed, and it is enumerated from the
 // DECLARATIONS rather than from the data inventory. The inventory reports what a consumer registered, so
 // a handle it omits would survive forever with this subject's data inside it. The declared set is small,
 // known at startup and fully enumerable, which is what makes widening on a foreign tenant's declaration
 // safe at all.
 foreach (var (aggregateType, handle) in declaredHandleByType)
 {
 	if (!retainedHandleByType.ContainsKey(aggregateType) && !keysToDelete.Contains(handle))
 	{
 		keysToDelete.Add(handle);
 	}
 }

 // EVERY HANDLE THE WRITE PATH ACTUALLY MINTED FOR THIS SUBJECT, read from the ledger rather than
 // re-derived. This is the enumeration the two sources above cannot be: they answer which handles a
 // declaration or an inventory IMPLIES, and a handle neither of them implies is a key nothing destroys.
 //
 // IT IS WHY THE WRITE PATH MAY WIDEN UNCONDITIONALLY. The write widens onto a per-obligation-class
 // handle for every aggregate type, declared or not, because the alternative had no satisfying
 // assignment: widening only for a type declared AT WRITE TIME puts every non-aggregate write and every
 // undeclared-aggregate write on the SAME plain handle, and a retention declared later then binds two
 // obligations to one key with opposite required values -- destroying it makes legally retained data
 // unreadable, sparing it leaves personal data recoverable for a subject reported erased. Reachable with
 // no concurrency at all. Unconditional widening removes that, and it is sound ONLY because this
 // enumeration exists: without it, a widened handle no declaration implies would survive forever.
 //
 // FAIL-CLOSED, and the exception is deliberately NOT caught. An enumeration that cannot be completed is
 // indistinguishable from a subject with no handles, which is the catastrophic direction -- the erasure
 // would destroy what it could derive and certify. The ledger's contract is complete enumeration or
 // throw, and a throw here leaves the request non-terminal and retryable rather than falsely attested.
 var mintLedgerAtEnumeration = await _mintLedger.ListHandlesAsync(
 requestingTenant, status.DataSubjectIdHash, cancellationToken).ConfigureAwait(false);

 // BOTH SETS ARE ATTEMPTED, minted and pending alike. A pending row may already have a live key behind
 // it -- the confirmation write fails OPEN, so it can be lost while the key survives -- and treating a
 // pending row as "no key" would leave exactly the handle this ledger exists to reach. Attempting a
 // handle that holds nothing is harmless: the provider answers not-found and nothing is attested from
 // the absence.
 foreach (var mintedHandle in mintLedgerAtEnumeration.All)
 {
 	// A handle a declaration spares for THIS erasure is left alone, exactly as the derived path does.
 	// The ledger says the handle exists; it does not say whether destroying it is lawful, and that
 	// decision belongs to the retention join above -- the one actor holding this request's own
 	// recorded owner.
 	if (retainedKeyHandles.Contains(mintedHandle) || keysToDelete.Contains(mintedHandle))
 	{
 		continue;
 	}

 	keysToDelete.Add(mintedHandle);
 }
 if (_dataInventoryService is not null)
 {
 var inventory = await _dataInventoryService.DiscoverAsync(
 status.DataSubjectIdHash, DataSubjectIdType.Hash, status.TenantId, cancellationToken)
.ConfigureAwait(false);

 foreach (var keyRef in inventory.AssociatedKeys)
 {
 	// A discovered key belonging to a retained aggregate type is left alone. The inventory
 	// enumerates what a consumer registered, so it can legitimately surface the retained
 	// handle; destroying it here would undo the retention the contributor is about to honour.
 	if (retainedKeyHandles.Contains(keyRef.KeyId))
 	{
 		continue;
 	}

 	keysToDelete.Add(keyRef.KeyId);
 }

 // the discovered locations drive the structural coverage gate below.
 discoveredLocations = inventory.Locations;

 // carried whole so the coverage gate can read the DECLARED obligations from it; an erasure
 // cannot be reported complete merely because nothing was discovered.
 discoveredInventory = inventory;

 LogErasureKeysDiscovered(requestId, keysToDelete.Count);
 }

 // Per-subject crypto-shred: a subject's dedicated key handle is deterministically derived from the
 // (requesting tenant, subject-id hash) pair, by the SAME function the write path uses -- SubjectKeyHandle,
 // which both callers reach and which refuses a derivation with no tenant. Always destroy it -- even when
 // the data inventory does not enumerate it -- so destroying the key erases the subject regardless of
 // inventory coverage.
 //
 // THIS LINE USED TO QUEUE status.DataSubjectIdHash ITSELF, which is constant in the tenant. Two tenants
 // whose consumer-supplied data-subject identifiers coincided shared one key, and this destruction took it
 // from both: the other tenant's reads then returned an erasure tombstone over data that was never erased
 // and could no longer be recovered. The raw hash is still the right key for every ROW this erasure
 // touches -- those queries carry a tenant column -- and is now the wrong value for a key HANDLE, which has
 // no row beside it.
 var subjectKeyId = subjectKeyHandle.Value;
 if (!keysToDelete.Contains(subjectKeyId))
 {
 	keysToDelete.Add(subjectKeyId);
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

 // What earlier passes of THIS request already destroyed. A retry must be able to attest the coverage
 // its own first pass achieved, and the key store cannot tell it: a key it destroyed reports as absent,
 // exactly as a key that never existed does.
 // Handles that raised an error of their own during the destruction. The attestation subtracts them so
 // one outstanding key is not reported twice under two descriptions, the second of which reads as a
 // more serious fact than the first.
 var handlesThatReportedForThemselves = new HashSet<string>(StringComparer.Ordinal);

 // THE SAFETY MECHANISM, immediately before the first irreversible act. ONE refusal here covers BOTH
 // readers of the declaration set -- the key spared in this method, and the rows spared by the
 // event-store contributor, which runs after this line -- so the whole-erasure requirement is
 // satisfiable at a single point and does not need two.
 //
 // Per-key refusal would be UNSOUND rather than merely less simple: a key destroyed here while the
 // contributor spares the rows leaves retained records PRESENT AND PERMANENTLY UNREADABLE, which this
 // service elsewhere calls the worse of the two failures.
 var destructionRefusal = await _retentionStaleness.DescribeRefusalAsync(status.TenantId, cancellationToken)
.ConfigureAwait(false);

 if (destructionRefusal is not null)
 {
 return ErasureExecutionResult.Refused(destructionRefusal);
 }

 var deletedCount = await ExecuteKeyDeletionsAsync(keysToDelete, deletedKeyIds, status.DestroyedKeyHandles, scheduledKeys, errors, handlesThatReportedForThemselves, requestId, status, cancellationToken)
.ConfigureAwait(false);

 // Invoke erasure contributors (event stores, snapshot stores, etc.)
 var contributorResults = new List<ErasureContributorResult>();
 var failedContributors = new List<(string Name, string? Error)>();
 var retainedData = new List<ErasureException>();
 var totalRecordsAffected = await InvokeContributorsAsync(requestId, status, discoveredInventory, errors, contributorResults, failedContributors, retainedData, cancellationToken)
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

 // What the certificate ATTESTS as lawfully retained: the framework's own store-kind exemptions,
 // plus whatever each contributor kept and said so. A contributor that withholds destruction and
 // stays silent is the failure this concatenation exists to prevent -- a certificate that reads as
 // a clean completion over data deliberately kept cannot be told from one over data destroyed, and
 // nothing downstream ever learns otherwise.
 //
 // Each retention is stamped with the handle that still protects this subject inside it, taken from the
 // map the spare-decision above built. Without the handle the declared period is unactionable: the
 // retained key is not the subject's own handle, so nothing ever queues it again, and destroying it when
 // the obligation lapses can only be done by name.
 //
 // A retention entry with NO handle is a real signal rather than a blank: it means a contributor spared
 // an aggregate type whose key this erasure did not decide about. Left as null rather than invented,
 // because a fabricated handle is worse than an absent one -- it would be destroyed with confidence.
 var certificateExemptions = new List<ErasureException>(coverage.Exemptions);
 foreach (var retention in retainedData)
 {
 	certificateExemptions.Add(
 		retention.DataCategory is { Length: > 0 }
 		&& retainedHandleByType.TryGetValue(retention.DataCategory, out var handle)
 			? retention with { RetainedKeyHandle = handle }
 			: retention);
 }

 // ASKED HERE, BEFORE THE OUTCOME IS DECIDED, and asked of the signing boundary rather than re-derived.
 // An exemption whose Article 17(3) ground was never established is an UNMET OBLIGATION, and presenting an
 // unmet obligation as a lawful basis is what turns a failure into a defensible retention. So it folds into
 // the same errors collection every other unmet obligation uses, which is what makes the Completed branch
 // below unreachable -- rather than a refusal at signing, which would leave the consumer with the
 // destruction performed and no evidence that it was performed.
 //
 // The ORDER is the whole reason this call is here and not at the signature: the outcome is decided a few
 // lines down and the certificate is signed after that, so a finding raised at signing time would arrive
 // after the attestation it had to prevent.
 foreach (var unestablished in ErasureCertificateSigner.UnestablishedClaims(status.LegalBasis, certificateExemptions))
 {
 errors.Add(unestablished);
 }

 // KEY STATE RE-ESTABLISHED BEFORE ANYTHING IS ATTESTED. Destruction ran once, above, before the
 // contributors; it establishes the state of these handles at THAT instant and nothing later. A write for
 // this same data subject landing afterwards mints a live key at a handle this erasure destroyed, so by the
 // time the certificate is signed there is personal data of an erased subject under a live key -- and a
 // certificate signed on the earlier measurement asserts a fact about now from a reading taken then. That
 // is the silent class: undetectable from outside, because the evidence asserts the opposite.

 // RE-READ THE LEDGER AT ATTESTATION, AND THE RE-READ IS ONLY WORTH ANYTHING BECAUSE THE ROW CARRIES ITS
 // OWN STATE. Re-reading the handle SET alone buys nothing: a row whose mint has not run yet is present
 // in both reads and its material probes absent, so the dangerous state is observationally identical to a
 // writer that crashed between its row and its mint -- which is safe and correctly ignored. The two
 // diverge only after this certificate is signed. What separates them is whether the row says the key was
 // created, and that is what this read is for.
 //
 // A HANDLE STILL PENDING HERE IS A WRITE IN FLIGHT. It is reported through the existing
 // concurrent-writes outcome rather than as a failure, because nothing the erasure attempted failed: the
 // keys it set out to destroy are gone, and a write arrived alongside. That outcome already names the
 // handles and tells the consumer to erase again once the writes have stopped.
 //
 // FAIL-CLOSED, and not caught, for the same reason the first enumeration is not: a read that cannot be
 // completed must not be read as "nothing pending", which would restore the silent completion this whole
 // mechanism removes.
 var mintLedgerAtAttestation = await _mintLedger.ListHandlesAsync(
 SubjectKeyHandle.OwningTenant(status.TenantId), status.DataSubjectIdHash, cancellationToken)
.ConfigureAwait(false);

 // WHAT THIS DOES NOT DO, and the width of it is NO LONGER ONE ROUND TRIP: the read and the status write
 // below are two steps rather than one atomic action at the store, so a row inserted between them is not
 // seen. This DETECTS a concurrent write; it does not EXCLUDE one. Closing the remainder needs the
 // completion write to be conditional at the store on there being no pending row for this subject, which
 // this service does not do.
 //
 // THE WINDOW IS THE PROBE LOOP, and that is a cost this read's own position paid for. The ledger read
 // used to come AFTER the destruction-status probes, so a row inserted during those probes still showed
 // up in this snapshot's pending set. It now comes BEFORE them, because it supplies their domain -- so a
 // row landing during the loop is no longer seen, and the loop is one provider call per candidate rather
 // than one in total. The trade buys the whole-ledger probe below, which closes a wider and entirely
 // silent hole, and it is a trade rather than a free widening. The earlier wording here claimed a
 // one-round-trip window and a caught dominant case; both were true of a write already IN FLIGHT and
 // neither was true of a write that started and finished inside the span.
 var deliberatelyRetained = new HashSet<string>(retainedHandleByType.Values, StringComparer.Ordinal);

 // A pending row at a DELIBERATELY RETAINED handle is not a finding: live material there is the point,
 // and the row will confirm when that write completes. Left in, it reported a concurrent write for
 // every erasure that lawfully retained anything while a write to it was in flight.
 var pendingMints = mintLedgerAtAttestation.PendingMint
 .Where(handle => !deliberatelyRetained.Contains(handle))
 .ToList();
 // THE PROBE'S DOMAIN IS WHAT THE LEDGER HOLDS, NOT WHAT THIS PASS DESTROYED, and the difference was a
 // defect in this method. A write whose row AND mint both landed between the enumeration and this moment
 // was in no set examined anywhere: not in the enumerated set, which predates it, so the coverage gate
 // never reached it; not in the destroyed set, so the probe below never reached it; and not PENDING,
 // because its mint completed, so the pending check above never reached it either. The certificate was
 // signed over live key material for an erased subject with nothing raised, which is the silent class
 // this whole mechanism exists to remove. The comment below used to describe the remaining window as one
 // round trip wide; that was true of a write already IN FLIGHT and false of one that started and
 // finished inside the span, for which the window was the whole span.
 //
 // THE PREDICATE IS "IS THE MATERIAL GONE", NOT "IS THIS HANDLE IN A LIST I KEPT", and that choice is
 // what keeps a RETRY honest. A handle an earlier pass of this same request destroyed has absent
 // material, so the probe itself excuses it and no bookkeeping is needed. An accounted-set formulation
 // would have to carry every earlier pass's handles and would indict its own first pass the moment it
 // forgot one -- and a false positive here is worse than the defect being fixed, because it makes a
 // correct erasure unable to ever report completion.
 //
 // DELIBERATELY RETAINED HANDLES ARE EXCLUDED, because for them live material is the POINT. The map is
 // the one the spare decision built, so a retention this erasure could not attach to a handle is not in
 // it -- and that is correct rather than a gap: a live handle this erasure cannot name is genuinely
 // unaccounted for, and saying so is more useful than assuming it was meant.
 // THE DOMAIN IS THE LEDGER, MINUS ONLY WHAT HAS ALREADY REPORTED -- never minus what the pass merely
 // LOOKED AT. The distinction is the whole correction and it was got wrong once here, in the direction
 // that reopens a silent certificate.
 //
 // The earlier version subtracted everything the erasure ENUMERATED, on the argument that each
 // enumerated handle already has an outcome: destroyed and gone, destroyed and live again, irreversibly
 // scheduled, or a destruction that failed. There is a FIFTH, and it is the one that reports nothing at
 // all -- enumerated, attempted, and nothing was there to destroy. The absent branch of the destruction
 // loop says so in its own words: "nothing was destroyed and nothing is attested. It is still not an
 // error." So a handle whose ledger row existed BEFORE this enumeration, and whose key was created AFTER
 // the destruction attempt, was enumerated, acted on, and reported by nothing -- and subtracting the
 // enumerated set is what hid it. That needs one ordinary writer and no race beyond the row landing
 // before the enumeration, and it ends in a Completed certificate over live key material.
 //
 // ENUMERATED IS AN INTENT. DESTROYED IS A FACT. The set where they differ is exactly the blind spot of
 // any check whose domain subtracts the enumerated set, and it is non-empty whenever a destruction
 // attempt found nothing to destroy.
 //
 // So the subtraction is the handles that raised an error of their own, which is the set the
 // double-reporting objection was actually about, and it is collected by construction rather than by
 // listing the branches that report.
 //
 // THE DESTROYED SET STAYS IN THE DOMAIN UNCONDITIONALLY. An attempt before that one replaced it with
 // the ledger set rather than adding to it, which REMOVED the re-occupation check wherever the ledger is
 // silent about a handle -- a second source that happens to say nothing must not retire the first.
 var attestationCandidates = new List<string>(deletedKeyIds);
 var alreadyACandidate = new HashSet<string>(deletedKeyIds, StringComparer.Ordinal);

 foreach (var handle in mintLedgerAtAttestation.Minted)
 {
 if (deliberatelyRetained.Contains(handle)
 || handlesThatReportedForThemselves.Contains(handle))
 {
 continue;
 }

 // The Add is the de-duplication AND the guard, so it is on its own statement rather than inside the
 // condition above: a set mutation buried in a short-circuiting || chain works and is a trap for
 // whoever reorders the predicates next.
 if (alreadyACandidate.Add(handle))
 {
 attestationCandidates.Add(handle);
 }
 }

 var reMinted = await FindUncoveredLiveHandlesAsync(
 attestationCandidates, deletedKeyIds, errors, requestId, cancellationToken).ConfigureAwait(false);

 // A HANDLE THE REQUEST'S OWN RECORD SAYS IT DESTROYED, FOUND LIVE, IS NOT "A WRITE ARRIVED ALONGSIDE".
 // It still raises an error above, which is what keeps Completed unreachable, but it must NOT be routed
 // into the concurrent-writes outcome, because that outcome asserts that everything the erasure set out
 // to destroy is gone -- and here the request's own record contradicts that. Dressing it as a concurrent
 // write would report Success for an erasure whose attestation is broken, which is the opposite of what
 // the record is for: the record makes a retry COUNTABLE, it does not make the destruction TRUE, and the
 // provider's answer is the one about now.
 //
 // THE EXCLUSION IS MEMBERSHIP IN THE REQUEST'S OWN RECORD, and nothing finer, because nothing finer is
 // available here. A first attempt to qualify it with "unless this pass also destroyed it" was wrong:
 // the retry counts a recorded handle into this pass's destroyed set precisely so it can attest its
 // first pass's coverage, so the qualifier re-admitted every case it was meant to exclude.
 //
 // The cost of the blunt form is one case reported less precisely: a handle destroyed, re-occupied,
 // destroyed again by this pass, and re-occupied a second time is genuinely concurrent with THIS pass
 // and will now be reported as a partial failure rather than as a concurrent write. That is the right
 // direction to be imprecise in. Something is unresolved either way, and a compliance attestation that
 // is unsure must not be the one that claims success.
 var recordedByThisRequest = new HashSet<string>(status.DestroyedKeyHandles, StringComparer.Ordinal);

 var concurrentlyWrittenHandles = reMinted
 .Where(handle => !recordedByThisRequest.Contains(handle))
 .ToList();

 concurrentlyWrittenHandles.AddRange(
 pendingMints.Where(h => !reMinted.Contains(h, StringComparer.Ordinal)));

 // ONE ERROR PER PENDING HANDLE, mirroring the re-mint probe above, because that is what makes the
 // Completed branch UNREACHABLE. Completed is guarded by errors.Count == 0, so a pending row that added no
 // error would be detected and then certified over anyway -- the detection has to cost an error or it
 // changes nothing.
 foreach (var pending in pendingMints)
 {
 	if (reMinted.Contains(pending, StringComparer.Ordinal))
 	{
 		continue;
 	}

 	errors.Add(
 		$"key handle '{pending}' has a mint-ledger row whose key creation is not confirmed, so a write for "
 		+ "this data subject is in flight and its key may come into existence after this erasure finishes. "
 		+ "The row is written before the key, so a pending row cannot be told apart from a key that will "
 		+ "exist in a moment -- this erasure therefore does not cover that handle.");
 }

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
 requestId, scheduledKeys, deletedCount, totalRecordsAffected, activity, claimIdentity, cancellationToken).ConfigureAwait(false);
 ExecutionDurationHistogram.Record(executionStopwatch.Elapsed.TotalMilliseconds);
 return awaiting;
 }

 // Every error is a handle that was live again when the erasure finished, and nothing else went wrong. That
 // is NOT a partial failure -- nothing the erasure attempted failed -- and it is not completion either, so
 // it gets the state that says exactly what happened. Reporting it as PartiallyCompleted would tell an
 // auditor something failed when nothing did; reporting it as Completed would attest an erasure over data
 // written after the key was destroyed. Same shape as the scheduled-key branch above: a specific,
 // fully-attributed residue gets its own outcome, and ANY other error falls through to the partial path.
 if (errors.Count > 0 && errors.Count == concurrentlyWrittenHandles.Count)
 {
 var concurrent = await RecordCompletedExceptConcurrentWritesAsync(
 requestId, concurrentlyWrittenHandles, deletedKeyIds, status, deletedCount, totalRecordsAffected,
 coverage.UncoveredStoreKinds, failedContributors, certificateExemptions, activity, claimIdentity, cancellationToken)
.ConfigureAwait(false);
 ExecutionDurationHistogram.Record(executionStopwatch.Elapsed.TotalMilliseconds);
 return concurrent;
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

 // Persist the certificate BEFORE recording the status, and only when something succeeded.
 // This is what makes a partial erasure evidenced rather than merely logged: the structured
 // residue is in hand HERE and nowhere later -- ErasureStatus persists counts and one joined
 // error string, so a certificate reconstructed after the fact could not name a store kind
 // without parsing prose back into data.
 if (hasAnySuccess)
 {
 // FAILS OPEN, and this is a correctness requirement rather than defensive coding.
 //
 // Writing the certificate signs a payload and resolves a store, so it can throw. Without this
 // catch the throw reaches the handler at the bottom of this method, which records the request
 // as Failed and returns a result carrying NO key count -- so an erasure that ACTUALLY DESTROYED
 // THE KEY reports as never having happened. The key is gone and the request is terminal, so the
 // consumer cannot redo it and cannot obtain evidence that it was done. That is a worse outcome
 // than the missing certificate this whole path exists to provide.
 //
 // The erasure's outcome is decided by what the erasure DID, never by whether we managed to
 // write the document about it. Same rule the framework applies to every other cross-cutting
 // concern: an optional surface skips and logs, it does not take the operation down with it.
 try
 {
 _ = await PersistCompletionCertificateAsync(
 requestId,
 status,
 deletedKeyIds,
 deletedCount,
 totalRecordsAffected,
 certificateExemptions,
 cancellationToken,
 warnings: null,
 certificateIdOverride: null,
 uncoveredStoreKinds: coverage.UncoveredStoreKinds,
 failedContributors: failedContributors).ConfigureAwait(false);
 }
 catch (Exception certificateFailure) when (certificateFailure is not OperationCanceledException)
 {
 // Loud, because the consumer now has a partly-completed erasure with no signed evidence of
 // it and needs to know that rather than discover it at an audit.
 LogPartialCertificateNotWritten(requestId, certificateFailure);
 errors.Add(
 "the erasure completed in part but its certificate could not be written: "
 + certificateFailure.Message);
 errorSummary = string.Join("; ", errors);
 }
 }

 ReportRefusedOutcomeWrite(
 	requestId,
 	await _store.UpdateStatusAsync(
 		requestId,
 		partialStatus,
 		expectedStatus: null,
 		errorSummary,
 		claimIdentity,
 		cancellationToken)
.ConfigureAwait(false),
 	"the partial outcome of this erasure");

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
 requestId, status, deletedKeyIds, deletedCount, totalRecordsAffected, certificateExemptions, cancellationToken)
.ConfigureAwait(false);
 // THE SIGNED ATTESTATION, and the one write where losing the claim must change what we report.
 // A superseded executor stands down silently: another instance owns this request and is handling
 // it, so reporting success here would attest coverage this run no longer establishes.
 var completion = await _store
 	.RecordCompletionAsync(requestId, deletedCount, totalRecordsAffected, certificateId, claimIdentity, cancellationToken)
 	.ConfigureAwait(false);

 if (completion != ErasureWriteOutcome.Applied)
 {
 	LogErasureOutcomeNotRecorded(
 		requestId,
 		"another instance took this request over before its completion could be recorded, so this "
 		+ "run stood down without certifying; the instance holding it now records the outcome");

 	return ErasureExecutionResult.Refused(
 		"Another instance took this request over while it was executing. Its keys and records were "
 		+ "destroyed and that destruction is on the ledger, so the instance holding the request now "
 		+ "completes and certifies it.");
 }

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
 await RecordTerminalFailureAsync(requestId, ex.Message, claimIdentity).ConfigureAwait(false);
 return ErasureExecutionResult.Failed(ex.Message);
 }
	}

	/// <summary>
	/// Records a destruction that has ALREADY happened, on a token the caller cannot cancel.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>THE DESTROY AND ITS RECORD ARE ONE UNIT WITH RESPECT TO CANCELLATION, and this is where that is
	/// enforced.</b> The provider call that destroys a generation is irreversible; the ledger row is the
	/// only evidence it happened. A cancellation landing between them leaves material destroyed with
	/// nothing recording it, and on a later pass the provider cannot distinguish a generation it
	/// destroyed for us from one it never held — so the request can become uncertifiable permanently
	/// wherever no destruction-status provider is registered.
	/// </para>
	/// <para>
	/// So the caller's token is deliberately NOT passed. A host shutdown may stop this run between KEYS,
	/// where nothing irreversible is waiting for its record; it may not stop it here. Bounded by the same
	/// budget the terminal-failure write uses, because an unbounded write would hold host shutdown open
	/// on a store that is not answering, and a bounded one that lapses is no worse than the cancellation
	/// it replaced.
	/// </para>
	/// </remarks>
	private async Task RecordDestroyedGenerationAsync(Guid requestId, string keyHandle, string keyGeneration)
	{
		using var ledgerWrite = new CancellationTokenSource(OutcomeWriteBudget);

		await _store.RecordKeyDestroyedAsync(requestId, keyHandle, keyGeneration, ledgerWrite.Token)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Reports a status write that did not apply, distinguishing the race from the defect.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A DISCARDED OUTCOME SWALLOWS ALL THREE REFUSALS, AND ONLY ONE OF THEM IS ORDINARY.</b> These
	/// writes present the run's claim identity, so a refusal is usually
	/// <see cref="ErasureWriteOutcome.ClaimLost"/>: another instance took the request over, and standing
	/// down silently is exactly right — reporting it would turn every handover into a false alarm.
	/// </para>
	/// <para>
	/// The other two are not ordinary. <see cref="ErasureWriteOutcome.StatusRefused"/> while holding the
	/// claim means this run asked for a transition the store's partition forbids, and
	/// <see cref="ErasureWriteOutcome.NotFound"/> means the request it was erasing is gone. Both are
	/// defects in this framework or signs the row was changed underneath it, and both used to be
	/// indistinguishable from the handover because the outcome was assigned to a discard.
	/// </para>
	/// </remarks>
	/// <param name="requestId">The request whose outcome write was refused.</param>
	/// <param name="outcome">What the store answered.</param>
	/// <param name="what">What this run was trying to record, for the operator reading the log.</param>
	private void ReportRefusedOutcomeWrite(Guid requestId, ErasureWriteOutcome outcome, string what)
	{
		if (outcome is ErasureWriteOutcome.Applied or ErasureWriteOutcome.ClaimLost)
		{
			return;
		}

		LogErasureOutcomeNotRecorded(
			requestId,
			$"the store refused the write of {what} with {outcome}, so that outcome is not recorded "
			+ "against the request and no later pass can learn it from the store");
	}

	/// <summary>
	/// Records the terminal failure of an execution on a token of its OWN, so a shutdown that cancelled the
	/// work cannot also cancel the record of it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The caller's token is deliberately not passed.</b> A graceful shutdown cancels it before this runs,
	/// and both SQL stores open their connection with <c>OpenAsync(cancellationToken)</c>, so the write would
	/// throw rather than land -- leaving the request claimed, with no terminal status, no error and no
	/// certificate, which nothing in the system ever enumerates again.
	/// </para>
	/// <para>
	/// <b>Neither a throw nor a refusal escapes.</b> This runs inside the execution handler, so an exception
	/// raised here would replace the original fault and the caller would never learn why the erasure actually
	/// stopped. A refusal is reported rather than discarded: it means the request is absent or already in a
	/// status this transition may not overwrite, which is precisely what an operator needs to see. The log is
	/// the only surface left once the durable write has failed, which is why it is Critical.
	/// </para>
	/// </remarks>
	/// <param name="requestId">The request whose execution stopped.</param>
	/// <param name="errorMessage">The fault to record against the request.</param>
	/// <param name="claimIdentity">
	/// The identity of the claim the failing run held. Presented so the store can refuse this write when
	/// another instance has taken the request over -- in which case the failure belongs to a run that no
	/// longer owns the request, and marking it terminally failed would condemn an erasure in progress
	/// elsewhere.
	/// </param>
	private async Task RecordTerminalFailureAsync(Guid requestId, string errorMessage, string claimIdentity)
	{
		try
		{
			using var outcomeWrite = new CancellationTokenSource(OutcomeWriteBudget);

			var recorded = await _store.UpdateStatusAsync(
					requestId,
					ErasureRequestStatus.Failed,
					expectedStatus: null,
					errorMessage,
					claimIdentity,
					outcomeWrite.Token)
				.ConfigureAwait(false);

			if (recorded == ErasureWriteOutcome.ClaimLost)
			{
				// NOT a defect, and deliberately not logged as one: another instance owns the request and is
				// handling it. A terminal failure written here would mark as failed an erasure still running.
				return;
			}

			if (recorded != ErasureWriteOutcome.Applied)
			{
				LogErasureOutcomeNotRecorded(
					requestId,
					"the store refused the write, so the request is absent or already in a status this "
					+ "transition may not overwrite");
			}
		}
		catch (Exception recordFailure)
		{
			LogErasureOutcomeNotRecorded(requestId, recordFailure.Message);
		}
	}

	/// <summary>
	/// Destroys each crypto-shred key and classifies the tri-state outcome: a <see cref="KeyDestructionState.Completed"/>
	/// key is counted as erased (added to <paramref name="deletedKeyIds"/>); a <see cref="KeyDestructionState.ScheduledIrreversible"/>
	/// key registers an error so the completion gate cannot attest it as irrecoverable;
	/// <see cref="KeyDestructionState.NotFound"/> attests the key only when THIS request destroyed it on an
	/// earlier pass, and is otherwise an idempotent no-op. Returns the count of keys destroyed by this pass.
	/// </summary>
	/// <param name="keysToDelete">The key handles discovered for this subject.</param>
	/// <param name="deletedKeyIds">The coverage set: handles whose material is established as irrecoverable.</param>
	/// <param name="alreadyDestroyedByThisRequest">
	/// Handles an earlier pass of this same request recorded as destroyed. This is what makes a retry able to
	/// attest the coverage its first pass achieved: the key store reports a key it already destroyed as
	/// absent, which is the same answer it gives for a key that never existed, so without this record the
	/// retry attests less than the request accomplished and completion is unreachable forever.
	/// </param>
	/// <param name="scheduledKeys">Keys the provider scheduled rather than destroyed.</param>
	/// <param name="errors">The gate's error list; any entry makes completion unreachable.</param>
	/// <param name="handlesThatReportedForThemselves">
	/// Receives every handle about which this method raised an error. The attestation subtracts this set
	/// from its probe domain so one outstanding key is not reported twice under two descriptions, the
	/// second of which reads as a more serious fact than the first. Membership is decided by comparing the
	/// error count across each handle's iteration rather than at each reporting branch, so a branch added
	/// later cannot forget to register its handle.
	/// </param>
	/// <param name="requestId">The erasure request.</param>
	/// <param name="status">
	/// The request, carrying the subject hash and tenant needed to RE-CHECK the legal hold before each
	/// destruction. The run-level check cannot serve: minutes of discovery work separate it from the first
	/// irreversible act, and a hold placed in that interval would otherwise go unobserved.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task<int> ExecuteKeyDeletionsAsync(
		IEnumerable<string> keysToDelete,
		List<string> deletedKeyIds,
		IReadOnlyCollection<string> alreadyDestroyedByThisRequest,
		ScheduledKeyDestructions scheduledKeys,
		List<string> errors,
		HashSet<string> handlesThatReportedForThemselves,
		Guid requestId,
		ErasureStatus status,
		CancellationToken cancellationToken)
	{
		var deletedCount = 0;

		foreach (var keyId in keysToDelete)
		{
			// CANCELLATION IS OBSERVED HERE AND NOWHERE ELSE IN THIS LOOP. Between keys nothing
			// irreversible is waiting for its record, so stopping is safe and the bottom handler records
			// the outcome. Inside the body it is not safe: a cancellation between a provider destroy and
			// its ledger row destroys material and loses the only evidence of it, which is why the record
			// now runs on a budgeted token of its own rather than the caller's.
			cancellationToken.ThrowIfCancellationRequested();

			// MEMBERSHIP BY CONSTRUCTION, not by remembering. This loop has six branches that can raise an
			// error about one handle -- scheduled-irreversible, unrecognised state, a staged generation that
			// could not be confirmed, a revoke failure, a generation read failure, and the catch. Threading
			// a set through all six is a sweep, and a sweep can miss one; worse, a branch added later
			// inherits the omission silently. Comparing the error count across the iteration cannot miss a
			// branch, because every branch that reports goes through it.
			//
			// What depends on this: the attestation subtracts these handles from its probe domain, so a
			// handle that reported here is not reported a second time under a different and more serious
			// description. A handle that did NOT report here stays in the domain -- which is the whole
			// correction, because the absent branch below reports nothing at all.
			var errorsBeforeThisHandle = errors.Count;
			// RE-CHECK THE HOLD BEFORE EVERY DESTRUCTION, not once per run, and this is a correctness
			// requirement rather than defensive polish.
			//
			// The run-level check sits immediately after the InProgress claim and its own comment concedes it
			// only "tightens the window". Between that check and here runs the whole key-discovery pass, which
			// is minutes wide by design -- so a legal hold placed in that interval was never observed and the
			// keys were destroyed anyway. Destruction is IRREVERSIBLE, so that is spoliation of data a hold
			// exists to preserve, and ARCHITECTURE.md states the blocking guarantee without qualification.
			//
			// What this CAN and CANNOT achieve, stated because the difference is the whole point. It cannot
			// make the race impossible: the hold lives in our store and the destruction happens at an external
			// KMS that cannot roll back, so some window is irreducible. What it does is bound the window to a
			// SINGLE key instead of the entire discovery-and-contributor pass, stop at the first opportunity,
			// and make the conflict LOUD -- the error below keeps completion unreachable, so the request can
			// never be attested Completed, and the count of keys already destroyed is recorded for the auditor.
			// A silent irreversible violation becomes a bounded and recorded one, which is the discriminator
			// that matters here: catastrophic is about SILENCE, not severity.
			//
			// COST: one hold read per key. That is deliberate. A read per irreversible act is the cheapest
			// thing in this method, and the alternative was to keep trading it for a wider window.
			var holdBeforeDestruction = await _legalHoldService.CheckHoldsByHashAsync(
				status.DataSubjectIdHash, status.TenantId, cancellationToken)
				.ConfigureAwait(false);

			if (holdBeforeDestruction.ErasureBlocked)
			{
				errors.Add(
					$"A legal hold became active for this subject after {deletedCount} key(s) had already been "
					+ $"destroyed, and before key '{keyId}'. Destruction STOPPED here and no further key was "
					+ "touched. The keys already destroyed CANNOT be recovered -- that is recorded rather than "
					+ "hidden, because an auditor needs to know a hold arrived mid-erasure. This request cannot "
					+ "reach Completed while this error stands; a re-execution sees the hold at the run-level "
					+ "check and settles on BlockedByLegalHold.");
				break;
			}

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

				// A1 — READ THE GENERATION, BEFORE the destruction, because the destruction destroys it.
				//
				// The generation identifier IS backend material on every provider this framework supports: a
				// key-vault version id, a CMK id, a sidecar marker, an in-memory entry. None of them is
				// readable once the material is gone, so this read has exactly one chance and it is here.
				var generationRead = await ReadKeyGenerationAsync(keyId, cancellationToken).ConfigureAwait(false);
				var generation = generationRead.Generation;

				// A GENERATION WE COULD NOT READ ABORTS THE DESTRUCTION FOR THIS KEY THIS PASS. We do not
				// destroy what we cannot record.
				//
				// THE DESTRUCTION IS THE STEP THAT CANNOT BE TAKEN BACK, which is what makes this the safe
				// direction rather than the timid one. Destroying anyway would leave the request PERMANENTLY
				// NON-TERMINAL: nothing is recorded, so Completed is unreachable, and a retry cannot repair it
				// either -- the generation the retry would need to read was annihilated by the very destruction
				// that proceeded. The subject's data is then unreadable forever, with no ledger row that could
				// ever be written and no pass that could ever attest the erasure.
				//
				// Aborting trades that for a LATE erasure: the key is still live, the subject's data still
				// reads, and a retry re-attempts read-then-stage-then-destroy in order and can genuinely
				// succeed. A delayed erasure is a timeliness problem; an unattestable one is permanent.
				//
				// This is the same shape as the escrow-revoke failure above, for the same reason: do not take
				// the irreversible step when the reversible precondition failed. The error folds into the
				// tri-state gate via `errors`, so Completed stays unreachable while this stands.
				//
				// A SECOND CHECK DEPENDS ON THIS ONE. The deferred-confirmation path refuses completion on an
				// AGGREGATE test -- "did this request record any generation at all" -- rather than per handle,
				// because the handles it examines are a superset and a handle that never held material has
				// nothing staged. That reading is sound only while "destroyed but unstaged" is unreachable, and
				// this refusal is what makes it unreachable. Relaxing it to destroy-anyway reopens the hole
				// there, not here.
				if (generationRead.Outcome == KeyGenerationReadOutcome.Unreadable)
				{
					errors.Add(
						$"The generation identifier of key '{keyId}' could not be read, so the key was NOT "
						+ "destroyed this pass. Destroying it would have been irreversible AND unrecordable: "
						+ "the identifier a destruction record needs is destroyed along with the material, so "
						+ "no later pass could ever attest the erasure and every read of this subject would "
						+ "fail permanently. A retry re-reads the generation, stages it, and then destroys.");
					continue;
				}

				// ABSENT IS NOT UNREADABLE, and conflating them is a regression this branch exists to prevent.
				// A handle that holds no material needs no destruction record, because there is no ciphertext
				// this erasure could have made unreadable -- so it falls through to the destroy, where the
				// NotFound branch below decides correctly between "this request destroyed it earlier" and "we
				// never held it". Aborting here instead would turn a discovered location whose key never
				// existed into an error that blocks completion forever.
				//
				// A1' — STAGE IT DURABLY, still before the destruction.
				//
				// This is NOT a destruction record and the read predicate cannot reach it: between here and the
				// destroy it names LIVE material. It is the only copy of the generation that survives the
				// destroy, so a pass that crashes in that window leaves a successor something to work from
				// instead of leaving the subject permanently undecryptable with no repair available.
				if (generation is not null)
				{
					await _store.StageKeyDestructionAsync(requestId, keyId, generation, cancellationToken)
						.ConfigureAwait(false);
				}

				// A2 — DESTROY. Reached only when the generation was staged, or when the handle holds no
				// material at all and there is therefore nothing to record.
				var outcome = await _keyAdmin.DeleteKeyAsync(keyId, 0, cancellationToken).ConfigureAwait(false);
				switch (outcome.State)
				{
					case KeyDestructionState.Completed:
						// A3 — RECORD. Irrecoverable NOW, and the ONLY state that may be recorded or attested.
						//
						// THIS GATE IS LOAD-BEARING FOR THE WHOLE LEDGER, and nothing else at this call site
						// says so. A ledger row is the sole basis on which a read of this subject's ciphertext
						// reports their field as erased, so a row written for anything weaker than Completed
						// attests an erasure over material that still exists. ScheduledIrreversible is exactly
						// that case and it is not hypothetical: a CMK pending deletion can be UN-destroyed --
						// AWS KMS CancelKeyDeletion restores it -- so recording one would put a resurrectable
						// key in the ledger and make every read of that subject report a lawful erasure of data
						// that is still recoverable. That failure is silent in both directions: the consumer
						// sees a tombstone and the auditor sees a certificate. Hence this case and no other.
						//
						// Recorded durably BEFORE it is counted, and per key rather than at completion. The
						// pass that needs this record is precisely the one that does not reach completion, so
						// a write deferred to the end is a write that never happens in the only case it is
						// for. If the record fails, the destruction is NOT attested this pass: attesting a
						// destruction we could not record would leave a retry unable to attest it either,
						// which is the defect this record exists to close.
						// A provider that reports Completed destroyed material, so it HAD material, so the read
						// above cannot have found the handle absent. This guard is therefore a contradiction
						// check rather than a branch anyone expects to take -- and it refuses to count an
						// unrecordable destruction rather than dereferencing a null and crashing the pass.
						if (generation is null)
						{
							errors.Add(
								$"Key '{keyId}' reported no material before the destruction and the provider "
								+ "then reported destroying it. Those cannot both be true, so this destruction "
								+ "is not attested: there is no generation to record it under, and a completion "
								+ "claimed over it would rest on nothing.");
							break;
						}

						// Completed covers the whole handle, including generations destroyed by an earlier
						// attempt whose ledger write failed before ordinary encryption reminted the handle.
						// Drain only this request's exact-handle intents; recording clears each intent only
						// after its durable ledger row exists. A partial failure therefore remains retryable.
						var completedGenerations = await _store.GetStagedKeyGenerationsAsync(requestId, keyId, cancellationToken)
							.ConfigureAwait(false);
						foreach (var completedGeneration in completedGenerations)
						{
							await RecordDestroyedGenerationAsync(requestId, keyId, completedGeneration)
								.ConfigureAwait(false);
						}

						// Also require the current pair if a store returned no matching staged entry.
						if (!completedGenerations.Contains(generation, StringComparer.Ordinal))
						{
							await RecordDestroyedGenerationAsync(requestId, keyId, generation)
								.ConfigureAwait(false);
						}

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
						// An earlier pass may have destroyed the material and lost its ledger write.
						// Inspect remaining intents even if another generation at this handle was recorded.
						var remaining = await _store.GetStagedKeyGenerationsAsync(requestId, keyId, cancellationToken)
							.ConfigureAwait(false);
						if (remaining.Count > 0)
						{
							var destructionStatus =
								(_keyAdmin as IServiceProvider)?.GetService(typeof(IKeyDestructionStatusProvider)) as IKeyDestructionStatusProvider
								?? _keyAdmin as IKeyDestructionStatusProvider;
							if (destructionStatus is null ||
								!await destructionStatus.IsKeyDestroyedAsync(keyId, cancellationToken).ConfigureAwait(false))
							{
								errors.Add($"Key '{keyId}' has staged generations but destruction could not be confirmed; "
									+ "the intents are retained and no erasure is attested from absence.");
								break;
							}

							foreach (var stagedGeneration in remaining)
							{
								await RecordDestroyedGenerationAsync(requestId, keyId, stagedGeneration)
									.ConfigureAwait(false);
							}
						}

						// ABSENT, and absence has two causes with opposite consequences. The key store cannot
						// tell them apart -- it reports a key it destroyed itself exactly as it reports one it
						// never held -- so the request's own record is what decides.
						//
						// "THIS request destroyed it on an earlier pass": the material is gone, which is the
						// same fact a Completed gives us, so it is attested. Without this, an erasure that
						// destroyed a key and then failed part-way through the remaining ones attested that key
						// on its first pass and nothing for it on the retry -- the subject's data destroyed and
						// their erasure permanently uncertifiable.
						//
						// "we never held it": nothing was destroyed and nothing is attested. It is still not an
						// error, because a discovered location whose key never existed has no ciphertext this
						// erasure could have made unreadable -- but it must not be counted as coverage either,
						// or a wrong inventory entry would read as an erased location.
						if (remaining.Count > 0 || alreadyDestroyedByThisRequest.Contains(keyId, StringComparer.Ordinal))
						{
							deletedKeyIds.Add(keyId);
						}

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
			catch (OperationCanceledException)
			{
				// NEVER SWALLOWED. This used to fall into the handler below, which added a "failed to
				// delete" error and CONTINUED to the next key -- so a cancelled run did not stop, it
				// manufactured one spurious failure per remaining key and drove the request terminal with
				// a report that named cancellation nowhere. Rethrown so the run ends and the outcome is
				// recorded once, by the handler that owns that decision.
				throw;
			}
			catch (Exception ex)
			{
				errors.Add($"Failed to delete key {keyId}: {ex.Message}");
				LogErasureKeyDeletionFailed(keyId, requestId, ex);
			}

			if (errors.Count > errorsBeforeThisHandle)
			{
				_ = handlesThatReportedForThemselves.Add(keyId);
			}
		}

		return deletedCount;
	}

	/// <summary>
	/// Reads the generation identifier of the material currently behind <paramref name="keyId"/>, before it is
	/// destroyed.
	/// </summary>
	/// <returns>
	/// The generation, or <see langword="null"/> when the backend cannot supply one -- the key is already absent,
	/// the provider does not report a generation, or the lookup failed.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Resolved from the admin rather than from a new constructor parameter, matching how the destruction-status
	/// capability is reached: a provider that composes answers through <see cref="IServiceProvider"/>, and the
	/// single-backend providers implement the interface directly, so both shapes are asked.
	/// </para>
	/// <para>
	/// A failure here returns <see langword="null"/> rather than throwing, and the caller then declines to
	/// RECORD rather than declining to DESTROY. Those are the two ways to be wrong and they are not
	/// symmetric: refusing to destroy because a metadata lookup failed leaves recoverable personal data in
	/// place against a lawful request, while destroying without a record leaves the subject erased and the
	/// erasure unreportable -- which the caller raises as an error, so it is loud.
	/// </para>
	/// </remarks>
	private async Task<KeyGenerationRead> ReadKeyGenerationAsync(string keyId, CancellationToken cancellationToken)
	{
		var keyProvider =
			(_keyAdmin as IServiceProvider)?.GetService(typeof(IKeyManagementProvider)) as IKeyManagementProvider
			?? _keyAdmin as IKeyManagementProvider;

		if (keyProvider is null)
		{
			// The configured admin cannot answer key lookups at all, so no destruction it performs could ever
			// be recorded. UNREADABLE rather than absent: we did not establish that the handle holds nothing,
			// we established that we cannot ask.
			return KeyGenerationRead.Unreadable;
		}

		try
		{
			var metadata = await keyProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

			if (metadata is null)
			{
				// No material at this handle. Nothing to record, and nothing this erasure can have made
				// unreadable, so the caller proceeds to the destroy rather than refusing.
				return KeyGenerationRead.Absent;
			}

			// A generation is well-formed by the time it is a KeyGeneration at all, so the old
			// string.IsNullOrEmpty guard has nothing left to catch: the only remaining question is whether the
			// provider reported one. Rendered to the characters the record stores.
			var generation = metadata.Generation?.ToString();

			// MATERIAL EXISTS AND THE PROVIDER NAMED NO GENERATION FOR IT. That is the unreadable case, not the
			// absent one: destroying this would annihilate ciphertext with no identifier under which any pass
			// could ever record the destruction.
			return generation is null ? KeyGenerationRead.Unreadable : KeyGenerationRead.Readable(generation);
		}
		catch (Exception ex)
		{
			// We could not ask, which is never the same as an answer of "nothing here". The caller declines to
			// destroy on this outcome, so the failure costs a late erasure rather than an unattestable one.
			LogKeyGenerationUnreadable(keyId, ex);

			return KeyGenerationRead.Unreadable;
		}
	}

	/// <summary>What a pre-destruction generation lookup established.</summary>
	/// <remarks>
	/// Three states, because the two the framework used to collapse have opposite consequences. "No material
	/// here" means there is nothing to record and nothing to lose, so the destruction may proceed. "I could not
	/// find out" means material may exist whose destruction could never be recorded, so the destruction must
	/// NOT proceed. A two-state result forces one of those to masquerade as the other, and whichever way it is
	/// collapsed it is wrong: treat unreadable as absent and an erasure becomes permanently unattestable; treat
	/// absent as unreadable and a discovered location whose key never existed blocks completion forever.
	/// </remarks>
	private enum KeyGenerationReadOutcome
	{
		/// <summary>The handle holds no material. Nothing to stage, nothing to record.</summary>
		Absent,

		/// <summary>The handle holds material and the provider named its generation.</summary>
		Readable,

		/// <summary>The lookup could not be answered. Whether material exists is UNKNOWN.</summary>
		Unreadable,
	}

	/// <summary>The outcome of a pre-destruction generation lookup, and the generation when there is one.</summary>
	private readonly record struct KeyGenerationRead(KeyGenerationReadOutcome Outcome, string? Generation)
	{
		public static KeyGenerationRead Absent { get; } = new(KeyGenerationReadOutcome.Absent, null);

		public static KeyGenerationRead Unreadable { get; } = new(KeyGenerationReadOutcome.Unreadable, null);

		public static KeyGenerationRead Readable(string generation) =>
			new(KeyGenerationReadOutcome.Readable, generation);
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
		List<(string Name, string? Error)> failedContributors,
		List<ErasureException> retainedData,
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

 // Harvested BEFORE the outcome is examined, deliberately. What a contributor kept is a
 // fact about the store; whether the same pass also failed is a fact about the run. Reading
 // the retentions only off a successful result made one unreachable read model enough to
 // strike every retention from the certificate -- and the partial certificate is the one a
 // controller reconciles by hand, so it is the one that most needs the list.
 retainedData.AddRange(contributorResult.RetainedData);

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

				// Kept STRUCTURALLY as well as in the joined error string, because the certificate must
				// name what was not reached and parsing it back out of prose is not a contract.
 failedContributors.Add((contributor.Name, contributorResult.ErrorMessage));
 LogErasureContributorFailed(contributor.Name, requestId, contributorResult.ErrorMessage ?? "Unknown error");
 }
 }
 catch (Exception ex)
 {
 errors.Add($"Contributor '{contributor.Name}' threw exception: {ex.Message}");
 failedContributors.Add((contributor.Name, ex.Message));
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
 Guid? certificateIdOverride = null,
 IReadOnlyCollection<string>? uncoveredStoreKinds = null,
 IReadOnlyList<(string Name, string? Error)>? failedContributors = null)
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
 // Null, never an empty list: the canonical form omits a null and a certificate with nothing
 // outstanding must serialize exactly as it did before this field existed, or every signature
 // already in the field stops verifying.
 UnreachedData = uncoveredStoreKinds is null && failedContributors is null
 ? null
 : UnreachedDataFactory.Build(uncoveredStoreKinds ?? [], failedContributors ?? []),
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

 // The claims were already asked of this boundary before the outcome was decided, so an unestablished one
 // has already made the Completed branch unreachable. Issuing here re-applies the same rule to the payload
 // that is actually signed -- which is what stops a claim from reaching a signature by a path nobody asked
 // about -- and the findings are logged rather than re-folded, because the outcome is settled by now.
 var certificate = ErasureCertificateSigner.Issue(
 payload, _options.Value.Retention.SigningKey, out var unestablishedClaims);

 if (unestablishedClaims.Count > 0)
 {
 LogCertificateClaimsNotEstablished(requestId, string.Join("; ", unestablishedClaims));
 }

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

		// THIS PASS TAKES ITS OWN CLAIM, and it must: it writes a status and a signed completion, and
		// both are fenced. It previously wrote them holding NO claim at all, coordinating only by
		// deriving the certificate id from the request id so the certificate insert was the exclusion.
		// That derivation stays -- it gives DEDUP, and it still covers a confirmer that crashed after
		// inserting the certificate and before recording completion -- but it is not custody, and a
		// fenced write needs custody.
		//
		// Claimed FROM AwaitingKeyDestruction rather than from Scheduled, and the claim leaves the
		// status exactly where it is: that status is a STAGE of the work, not a statement about who is
		// doing it, so advancing it here would un-stage the request mid-confirmation.
		var claim = await _store.ClaimForExecutionAsync(
				requestId,
				ErasureRequestStatus.AwaitingKeyDestruction,
				_options.Value.Execution.ClaimDuration,
				cancellationToken)
			.ConfigureAwait(false);

		if (claim.Outcome != ErasureClaimOutcome.Granted)
		{
			// Another confirmer holds it, or it moved on. Either way this pass has nothing to do and
			// says so by returning false, exactly as it does for a request in the wrong status.
			return false;
		}

		var claimIdentity = claim.ClaimIdentity!;

		// THE SAME DERIVATION AS THE DESTRUCTION, and it has to be: this method confirms that the keys
		// execution destroyed are gone, so a second spelling of the handle would ask about a key nothing ever
		// held. A handle nothing created answers "not destroyed" forever and the request could never complete.
		// This line previously held the bare subject-id hash, which is constant in the tenant.
		var keyIds = new List<string>
		{
			SubjectKeyHandle
				.ForSubjectHash(
					SubjectKeyHandle.OwningTenant(status.TenantId), status.DataSubjectIdHash, _dataSubjectHasher)
				.Value
		};
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
			// A handle that answers false is not yet destroyed and the request stays where it is.
			//
			// PRE-EXISTING LIVENESS ISSUE, noted rather than fixed because it predates the ledger and is LOUD:
			// if the handle is re-provisioned between the delete and this confirm, the backend truthfully
			// answers false about the NEW material and this request can never complete. It fails visibly and
			// forever rather than attesting something untrue, which is the correct direction, but it is a
			// liveness hole and it is not closed here.
			if (!await verifier.VerifyKeyDeletionAsync(keyId, cancellationToken).ConfigureAwait(false))
			{
				LogErasureKeyDestructionNotYetConfirmed(requestId, keyId);

				// RELEASE THE CLAIM BEFORE LEAVING. This pass took custody and is not going to complete, so
				// holding on would block every later pass for the whole lease -- the erasure would stall for
				// one claim duration per poll, which is the stranding this mechanism exists to prevent,
				// reintroduced one level up. Writing the stage back under our own identity releases custody,
				// because every status write through that member does.
				ReportRefusedOutcomeWrite(
					requestId,
					await _store.UpdateStatusAsync(
						requestId,
						ErasureRequestStatus.AwaitingKeyDestruction,
						expectedStatus: null,
						errorMessage: null,
						claimIdentity,
						cancellationToken)
					.ConfigureAwait(false),
					"that this erasure is awaiting key destruction");

				return false;
			}
		}

		// A3, DEFERRED. This is the SECOND door that writes ledger rows, and it is deliberately narrower than
		// the first rather than a relaxation of it -- do not simplify one onto the other.
		//
		// WHAT MAKES THE EVIDENCE SUFFICIENT HERE, because the backend answer alone is NOT:
		//
		// A handle-scoped attestation says "I hold no recoverable copy of this handle", whose true-case is
		// still three-valued on its own -- destroyed, or recoverable, or NEVER HELD HERE. Writing a ledger row
		// on that alone would reintroduce absence-as-evidence at a new site, which is the exact defect this
		// ledger exists to remove.
		//
		// THE STAGED INTENT EXCLUDES THE THIRD STATE. An intent records that we read generation g at handle h
		// FROM THIS BACKEND, at a moment before we issued the delete. So "never held here" is ruled out by our
		// own record, not by the backend's silence. The conjunction -- we staged g at h, we issued the destroy,
		// and the backend now states it holds no recoverable copy of h -- is an affirmative statement that g's
		// material is gone. The record is still written by the actor that performed the destruction, on
		// evidence that the destruction completed, which is what the guarantee requires.
		//
		// And the attestation is sound for the SET: if the backend holds no recoverable copy of the handle,
		// every generation that was ever at that handle is gone, including ones nobody staged. So each row
		// written from an intent is TRUE; the generations we cannot name are MISSING rather than wrong.
		//
		// THE GENERATION COMES FROM THE INTENT, NEVER FROM THE BACKEND. The material is gone by now, so a
		// fresh read is impossible -- that impossibility is the whole reason the intent is staged before the
		// destroy rather than after it.
		var recordedFromIntents = 0;
		foreach (var keyId in keyIds)
		{
			var staged = await _store.GetStagedKeyGenerationsAsync(requestId, keyId, cancellationToken)
				.ConfigureAwait(false);

			foreach (var generation in staged)
			{
				await RecordDestroyedGenerationAsync(requestId, keyId, generation)
					.ConfigureAwait(false);
				recordedFromIntents++;
			}
		}

		// NOTHING STAGED MEANS NOTHING TO STAND ON, and the request is refused rather than completed. A request
		// reaches this method only because destructions were SCHEDULED, so an empty intent set means we cannot
		// say which generations we destroyed -- and a completed erasure with no ledger row is one whose
		// subject's every read fails forever while a certificate says their data was lawfully erased. That is
		// the bare-absence case the second door exists to refuse, so it is refused here explicitly rather than
		// by producing zero rows and continuing.
		//
		// THIS REFUSAL IS AGGREGATE RATHER THAN PER-HANDLE, AND THAT IS SAFE ONLY BECAUSE THE DESTROY PATH
		// GUARANTEES A DESTROYED HANDLE ALWAYS HAS AN INTENT. Do not relax that guarantee without revisiting
		// this check.
		//
		// The handles examined here are a declared SUPERSET -- the subject key plus every key the inventory
		// associates with a discovered location -- so a handle that never held material has nothing staged and
		// never did. Refusing per-handle would therefore block completion for handles this erasure never
		// destroyed, which is why the test is "did we record anything at all".
		//
		// What makes that sound is upstream: ExecuteKeyDeletionsAsync REFUSES TO DESTROY a key whose generation
		// it could not read, so "destroyed but unstaged" is unreachable. Remove that refusal and this check
		// develops a hole with no local evidence of it: three handles where one was staged and destroyed and
		// another was destroyed with an unreadable generation would pass this aggregate test on the first
		// handle's intent, write no row for the second, and issue a certificate over a handle whose reads are
		// permanently broken. The two checks are one mechanism; neither is complete alone.
		// A previous confirmer may have committed the last ledger row (which clears its intent),
		// then crashed before completing the request. Re-read this request's durable evidence after
		// consuming intents; the initial status can also be stale after a concurrent confirmer.
		// Existing evidence is acceptable only after the positive provider verification above.
		var previouslyRecorded = recordedFromIntents == 0
			? await _store.GetStatusAsync(requestId, cancellationToken).ConfigureAwait(false)
			: null;
		if (recordedFromIntents == 0 &&
			!(previouslyRecorded?.DestroyedKeyHandles.Any(handle => keyIds.Contains(handle, StringComparer.Ordinal)) ?? false))
		{
			const string NoIntentDetail =
				"The key provider confirmed destruction, but this request staged no key generation before "
				+ "destroying, so there is nothing to record in the key-destruction ledger. Completion is "
				+ "refused: without a ledger record no read of this subject's data can report their erasure, "
				+ "and a certificate issued now would attest an erasure nothing stands behind.";

			LogErasureKeyDestructionNotRecordable(requestId, keyIds.Count);
			ReportRefusedOutcomeWrite(
				requestId,
				await _store.UpdateStatusAsync(
					requestId,
					ErasureRequestStatus.AwaitingKeyDestruction,
					expectedStatus: null,
					NoIntentDetail,
					claimIdentity,
					cancellationToken)
					.ConfigureAwait(false),
				"that this erasure is awaiting key destruction with no staged intent");

			return false;
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

		// Fenced. A superseded confirmer stands down silently rather than certifying a run it lost.
		var completion = await _store
			.RecordCompletionAsync(requestId, keyIds.Count, recordsAffected, certificateId, claimIdentity, cancellationToken)
			.ConfigureAwait(false);

		if (completion != ErasureWriteOutcome.Applied)
		{
			return false;
		}

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

	/// <summary>
	/// Re-asks the key provider whether each candidate handle holds material, and names the
	/// ones that are not.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The measurement and the attestation are separated by the whole contributor pass, and this closes
	/// that gap.</b> Destruction runs once, before the contributors; the certificate is signed after them. A
	/// write for the same data subject arriving in between finds no key at the handle and mints a live one, so
	/// the handle the erasure destroyed is occupied again by material the erasure never covered. Asking now is
	/// the only way the document can describe the state it is signed over rather than an earlier one.
	/// </para>
	/// <para>
	/// <b>A provider that cannot answer is an erasure that cannot attest.</b>
	/// <see cref="IKeyManagementProvider.GetKeyAsync"/> returning <see langword="null"/> is not an answer to
	/// this question — a soft-deleted key is invisible to that lookup for its whole recovery window — so the
	/// question goes to <see cref="IKeyDestructionStatusProvider"/>, which is the only member that answers it
	/// authoritatively. Without that capability the state is unmeasured, and an unmeasured state is recorded
	/// as such rather than assumed clean: silence is the one answer this must not give. Every key provider
	/// this framework ships implements it, and startup validation already warns a deployment whose provider
	/// does not.
	/// </para>
	/// <para>
	/// <b>Scoped to the ledger, less what already reported.</b> An erasure discharged entirely by record
	/// deletion destroyed nothing, so there is nothing whose state could have changed and no finding is
	/// produced — a check with no subject must not manufacture one.
	/// </para>
	/// </remarks>
	/// <returns>
	/// The handles that answered "not destroyed", in the order the candidate list presents them -- this
	/// pass's destroyed handles first, then the ledger's remaining confirmed handles.
	/// </returns>
	/// <param name="candidateHandles">
	/// The handles to probe: everything this pass destroyed, plus every confirmed handle on the subject's
	/// mint ledger that was neither deliberately retained nor already the subject of its own error. The
	/// second half is what reaches a handle this erasure never destroyed -- including one it enumerated and
	/// found nothing at, which reports through no other path.
	/// </param>
	/// <param name="deletedKeyIds">
	/// The handles this pass established as irrecoverable, used only to choose which of two sentences a
	/// finding gets: a handle this erasure destroyed has been RE-OCCUPIED, while one it never destroyed was
	/// never covered at all, and reporting the second as a re-mint tells a reader the erasure destroyed
	/// something it never touched.
	/// </param>
	/// <param name="errors">The gate's error list; any entry makes completion unreachable.</param>
	/// <param name="requestId">The erasure request, for the diagnostic log.</param>
	/// <param name="cancellationToken">A token that cancels the provider probes.</param>
	private async Task<IReadOnlyList<string>> FindUncoveredLiveHandlesAsync(
		IReadOnlyList<string> candidateHandles,
		IReadOnlyList<string> deletedKeyIds,
		List<string> errors,
		Guid requestId,
		CancellationToken cancellationToken)
	{
		if (candidateHandles.Count == 0)
		{
			return [];
		}

		// Resolved from the admin rather than from a new constructor parameter. The capability is advertised
		// through IServiceProvider by a provider that composes (the multi-region provider answers for its
		// regions), and implemented directly on the single-backend providers, so both shapes are asked.
		var destructionStatus =
			(_keyAdmin as IServiceProvider)?.GetService(typeof(IKeyDestructionStatusProvider)) as IKeyDestructionStatusProvider
			?? _keyAdmin as IKeyDestructionStatusProvider;

		if (destructionStatus is null)
		{
			LogKeyStateNotReestablished(requestId, _keyAdmin.GetType().Name, candidateHandles.Count);
			errors.Add(
				$"the state of {candidateHandles.Count} key handle(s) on this data subject's mint ledger could not "
				+ "be re-established before attesting, because the configured key provider cannot report whether a "
				+ "key is destroyed. A write for this data subject during the erasure would have left live material "
				+ "at one of them, and this erasure cannot say whether one did.");

			return [];
		}

		List<string>? reMinted = null;

		foreach (var keyId in candidateHandles)
		{
			// A failure to obtain an answer throws by this capability's contract, and it is deliberately NOT
			// caught here: "could not ask" must never be recorded as "destroyed". The handler at the bottom of
			// ExecuteAsync records the request as Failed, which is honest -- the erasure's work stands and its
			// attestation does not.
			if (await destructionStatus.IsKeyDestroyedAsync(keyId, cancellationToken).ConfigureAwait(false))
			{
				continue;
			}

			(reMinted ??= []).Add(keyId);

			// TWO SENTENCES, because the two cases are different facts and an auditor reads this. A handle this
			// pass destroyed has been RE-OCCUPIED. A handle it never destroyed was never covered at all, and
			// calling that a re-mint would tell the reader the erasure destroyed something it never touched.
			errors.Add(
				deletedKeyIds.Contains(keyId, StringComparer.Ordinal)
					? $"key handle '{keyId}' was destroyed by this erasure and holds recoverable material again, so "
						+ "personal data for this data subject was written while the erasure was running and is not "
						+ "covered by it."
					: $"key handle '{keyId}' is on this data subject's mint ledger and holds live material, and this "
						+ "erasure neither destroyed it nor retained it deliberately. Its mint completed after the "
						+ "enumeration this pass acted on, so the handle was never in the set it set out to destroy "
						+ "and this erasure does not cover it.");
		}

		return reMinted ?? [];
	}

	/// <summary>
	/// Records an erasure that did everything asked of it and was overtaken by a write for the same data
	/// subject, and issues its certificate.
	/// </summary>
	/// <remarks>
	/// The certificate is written BEFORE the status, and it is written at all, because the destruction already
	/// happened: withholding the document would leave the consumer with an irreversible act performed and no
	/// evidence of it, which is worse than the coverage gap being reported. It carries the same structured
	/// residue the partial path carries, so the uncovered store kinds and failed contributors are named rather
	/// than flattened into prose.
	/// </remarks>
	private async Task<ErasureExecutionResult> RecordCompletedExceptConcurrentWritesAsync(
		Guid requestId,
		IReadOnlyList<string> reMintedHandles,
		IReadOnlyList<string> deletedKeyIds,
		ErasureStatus status,
		int deletedCount,
		int recordsAffected,
		IReadOnlyCollection<string> uncoveredStoreKinds,
		IReadOnlyList<(string Name, string? Error)> failedContributors,
		IReadOnlyList<ErasureException> exemptions,
		Activity? activity,
		string claimIdentity,
		CancellationToken cancellationToken)
	{
		var detail =
			$"The erasure destroyed {deletedCount} key(s) and its contributors acted on {recordsAffected} "
			+ $"record(s). {reMintedHandles.Count} key handle(s) held live material for this data subject when "
			+ "it finished, so personal data for this data subject was written during the erasure and is not "
			+ $"covered by it: {string.Join(", ", reMintedHandles)}. Nothing the erasure attempted failed. "
			+ "Erase this data subject again once the writes have stopped.";

		// FAILS OPEN for the reason the partial path documents: the erasure's outcome is decided by what the
		// erasure DID, never by whether the document about it could be written. A throw here would reach the
		// handler in ExecuteAsync and report a request that destroyed keys as never having happened.
		try
		{
			_ = await PersistCompletionCertificateAsync(
				requestId,
				status,
				deletedKeyIds,
				deletedCount,
				recordsAffected,
				exemptions,
				cancellationToken,
				warnings: [detail],
				certificateIdOverride: null,
				uncoveredStoreKinds: uncoveredStoreKinds,
				failedContributors: failedContributors).ConfigureAwait(false);
		}
		catch (Exception certificateFailure) when (certificateFailure is not OperationCanceledException)
		{
			LogPartialCertificateNotWritten(requestId, certificateFailure);
		}

		ReportRefusedOutcomeWrite(
			requestId,
			await _store.UpdateStatusAsync(
				requestId, ErasureRequestStatus.CompletedExceptConcurrentWrites, expectedStatus: null, detail, claimIdentity, cancellationToken)
				.ConfigureAwait(false),
			"that this erasure completed except for concurrent writes");

		LogErasureCompletedExceptConcurrentWrites(requestId, reMintedHandles.Count, deletedCount);
		KeysDeletedCounter.Add(deletedCount);
		RequestsFailedCounter.Add(
			1, new TagList { { ErasureTelemetryConstants.Tags.ErrorType, "concurrent_write" } });
		activity?.SetTag("erasure.keys_deleted", deletedCount);
		activity?.SetTag("erasure.records_affected", recordsAffected);
		activity?.SetTag("erasure.handles_reoccupied", reMintedHandles.Count);
		activity?.SetTag(ErasureTelemetryConstants.Tags.ResultStatus, "completed_except_concurrent_writes");

		return ErasureExecutionResult.CompletedExceptConcurrentWrites(deletedCount, recordsAffected, detail);
	}

	private async Task<ErasureExecutionResult> RecordAwaitingKeyDestructionAsync(
		Guid requestId,
		ScheduledKeyDestructions scheduledKeys,
		int deletedCount,
		int recordsAffected,
		Activity? activity,
		string claimIdentity,
		CancellationToken cancellationToken)
	{
		var detail = scheduledKeys.Describe(deletedCount, recordsAffected);

		ReportRefusedOutcomeWrite(
			requestId,
			await _store.UpdateStatusAsync(requestId, ErasureRequestStatus.AwaitingKeyDestruction, expectedStatus: null, detail, claimIdentity, cancellationToken)
				.ConfigureAwait(false),
			"that this erasure is awaiting key destruction");

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
 ComplianceEventId.ErasureOutcomeNotRecorded,
 LogLevel.Critical,
 "Erasure request {RequestId} stopped and its terminal outcome could NOT be recorded: {Reason}. The "
 + "request stays in its claimed status and no pass enumerates it again; it needs operator attention")]
	private partial void LogErasureOutcomeNotRecorded(Guid requestId, string reason);

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
 ComplianceEventId.ErasureKeyDestructionNotRecordable,
 LogLevel.Error,
 "Erasure request {RequestId} had its destruction of {KeyCount} key handle(s) confirmed by the provider, "
 + "but staged no generation beforehand, so the destruction cannot be recorded in the ledger. The request "
 + "cannot be completed")]
	private partial void LogErasureKeyDestructionNotRecordable(Guid requestId, int keyCount);

	[LoggerMessage(
 ComplianceEventId.ErasureKeyGenerationUnreadable,
 LogLevel.Error,
 "Could not read the generation identifier of key {KeyId} before destroying it. The destruction will "
 + "proceed, but it cannot be recorded in the key-destruction ledger and the erasure cannot be attested")]
	private partial void LogKeyGenerationUnreadable(string keyId, Exception exception);

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
		EventId = ComplianceEventId.ErasurePartialCertificateNotWritten,
		Level = LogLevel.Error,
		Message =
			"Erasure {RequestId} completed in part but its certificate could not be written. The erasure "
			+ "itself stands and its counts are reported; there is no signed evidence of it.")]
	private partial void LogPartialCertificateNotWritten(Guid requestId, Exception exception);

	[LoggerMessage(
		EventId = ComplianceEventId.ErasureCertificateClaimsNotEstablished,
		Level = LogLevel.Error,
		Message =
			"Erasure {RequestId} issued a certificate carrying claims nobody established, and the request was "
			+ "already recorded as complete so the outcome could not be revised: {Claims}")]
	private partial void LogCertificateClaimsNotEstablished(Guid requestId, string claims);

	[LoggerMessage(
		EventId = ComplianceEventId.ErasureCompletedExceptConcurrentWrites,
		Level = LogLevel.Warning,
		Message =
			"Erasure {RequestId} did everything asked of it and {ReoccupiedHandles} destroyed key handle(s) held "
			+ "recoverable material again when it finished, so data written during the erasure is not covered by "
			+ "it. Keys destroyed: {KeysDeleted}")]
	private partial void LogErasureCompletedExceptConcurrentWrites(
		Guid requestId, int reoccupiedHandles, int keysDeleted);

	[LoggerMessage(
		EventId = ComplianceEventId.ErasureKeyStateNotReestablished,
		Level = LogLevel.Error,
		Message =
			"Erasure {RequestId} destroyed {KeysDeleted} key(s) and could not re-establish their state before "
			+ "attesting: key provider {ProviderType} does not report whether a key is destroyed. Implement "
			+ "IKeyDestructionStatusProvider on the provider so erasures that destroy its keys can complete.")]
	private partial void LogKeyStateNotReestablished(Guid requestId, string providerType, int keysDeleted);

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
