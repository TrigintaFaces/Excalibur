// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Diagnostics;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// Binds a data subject to a dedicated encryption key over the existing key-management subsystem, so that
/// destroying the subject's key crypto-shreds every value encrypted under it.
/// </summary>
/// <remarks>
/// <para>
/// The subject identifier is pseudonymized through <see cref="IDataSubjectHasher"/> to derive a stable,
/// non-reversible key handle — raw identifiers never reach the key store. Key material is minted by the
/// underlying <see cref="IKeyManagementProvider"/> (whose backends use a cryptographically-secure RNG); this
/// adapter never generates key bytes itself.
/// </para>
/// <para>
/// Key destruction is not performed here. Erasure is owned by the erasure service, which honours legal holds
/// and records attestation before destroying a subject's key through <see cref="IKeyManagementAdmin"/>.
/// </para>
/// </remarks>
internal sealed partial class SubjectKeyManager : ISubjectKeyManager
{
    private const string CryptoShredPurpose = "crypto-shred";

    private readonly IKeyManagementProvider _keyProvider;
    private readonly IDataSubjectHasher _hasher;

    /// <summary>
    /// The write-ahead record of every handle this manager has intended to mint.
    /// </summary>
    /// <remarks>
    /// <b>It replaced the retention registry here, and that replacement IS the fix.</b> The registry was
    /// consulted to decide whether to widen the handle, which made the write path a second reader of a
    /// mutable legal fact that the erasure also reads at a different instant. The handle is now derived
    /// with no registry read at all, and this ledger is what makes that safe: enumeration no longer
    /// depends on anyone re-deriving candidates.
    /// </remarks>
    private readonly IKeyMintLedger _mintLedger;

    /// <summary>
    /// The logger, required because a FAIL-OPEN step that cannot report is indistinguishable from one that
    /// succeeded.
    /// </summary>
    /// <remarks>
    /// Confirming a mint deliberately does not fail a consumer's personal-data write, so this log is the
    /// only way a lost confirmation is ever noticed. Without it the ledger would quietly accumulate pending
    /// rows and every erasure for those subjects would report a concurrent write with no trail explaining
    /// why.
    /// </remarks>
    private readonly ILogger<SubjectKeyManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectKeyManager"/> class.
    /// </summary>
    /// <param name="keyProvider">The key-management provider used to look up and create per-subject keys.</param>
    /// <param name="hasher">The data-subject hasher used to derive stable key handles.</param>
    /// <param name="logger">The logger used to report a mint confirmation that could not be recorded.</param>
    /// <param name="mintLedger">
    /// The write-ahead mint ledger. It REPLACED the retention registry on this path: the handle is now
    /// derived with no registry read, and this ledger is the enumeration that makes that safe.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>The ledger is REQUIRED, and for the reason the registry used to be: this is a KEY HANDLE, and a
    /// handle is the name of the thing an erasure destroys.</b> A manager that could be built without a
    /// ledger would mint handles nothing records, so an erasure would enumerate fewer handles than exist,
    /// destroy what it found, and certify. Nothing downstream could tell. A deployment that performs no
    /// erasure says so by registering the no-erasure ledger deliberately, never by omitting one.
    /// </para>
    /// <para>
    /// <b>The handle carries the tenant; the RETENTION DECISION still does not, and only the second of
    /// those is an absence.</b> The handle is <see cref="SubjectKeyHandle"/>, which is injective in the
    /// tenant by construction. What this class still does not need a tenant for is deciding WHETHER to
    /// widen onto a retained variant: that question is answered by
    /// <see cref="IErasureRetentionRegistry.IsRetainedForAnyTenant"/>, because whose retention applies is
    /// decided by the ERASURE against the tenant its own request recorded.
    /// </para>
    /// <para>
    /// This paragraph previously read <i>"There is no tenant here, and its absence is the design"</i>, and
    /// the comment in <see cref="GetOrCreateKeyAsync"/> asserted <i>"THE TENANT IS NOT PART OF THIS
    /// DECISION"</i>. Both reasoned carefully about the retention asymmetry and were silent on the
    /// collision axis — two tenants whose consumer-supplied data-subject identifiers coincide shared one
    /// key, and either tenant's erasure destroyed it for both. The wrong behaviour was written down as the
    /// expectation, which is what a discipline looks like just before it fails; the superseded wording is
    /// quoted here so anyone who absorbed it recognises it.
    /// </para>
    /// </remarks>
    public SubjectKeyManager(
        IKeyManagementProvider keyProvider,
        IDataSubjectHasher hasher,
        IKeyMintLedger mintLedger,
        ILogger<SubjectKeyManager> logger)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _mintLedger = mintLedger ?? throw new ArgumentNullException(nameof(mintLedger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async ValueTask<SubjectKey> GetOrCreateKeyAsync(
        TenantId tenant,
        string subjectId,
        RetentionScope retentionScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var handle = SubjectKeyHandle.ForSubject(tenant, subjectId, _hasher);

        // THE HANDLE IS PER OBLIGATION CLASS, UNCONDITIONALLY, AND READS NO RETENTION REGISTRY.
        //
        // Superseded reasoning, quoted so anyone who absorbed it recognises it -- it was careful, and it
        // was wrong on one axis it never considered:
        //
        // "ONLY a DECLARED aggregate type moves the handle, and that asymmetry is the design rather than an
        // optimisation. An erasure destroys the subject's own handle unconditionally, so every value left
        // under it is already reached. Widening every aggregate type instead would make the common case
        // depend on the data inventory being complete -- a widened key the inventory omitted is a key
        // nothing destroys, which is a subject never erased. Values under the plain subject handle need no
        // inventory to be destroyed, and that is the property being protected.
        //
        // THE TENANT IS NOT PART OF *THIS* DECISION, which is narrower than it used to read: the handle
        // itself carries the tenant (SubjectKeyHandle is injective in it), and what needs no tenant is only
        // the question of WHETHER to widen onto the retained variant. The write has to know whether the TYPE
        // is retained by anyone; WHOSE retention applies is the erasure's decision, made against the tenant
        // its own request recorded.
        //
        // Widening on a type declared by a DIFFERENT tenant withholds erasure from nobody: the erasure
        // finds no declaration matching this subject's tenant, so it destroys the widened handle too. The
        // asymmetry is safe in exactly one direction, and it is this one."
        //
        // THE OBJECTION WAS SOUND AND THE MINT LEDGER RETIRES IT. Enumeration no longer depends on the
        // data inventory: the row written below names every handle, so a widened key cannot be omitted.
        // That is the whole reason the ledger exists.
        //
        // WHAT THE CONDITION COST, and why it had no satisfying assignment. Widening only for a type
        // declared AT WRITE TIME means that for one (tenant, subject), every non-aggregate write and
        // every UNDECLARED-aggregate write land on the SAME plain handle. A retention declared later then
        // binds two obligations to one key with opposite required values: destroying it makes legally
        // retained data unreadable, and sparing it leaves personal data under no obligation recoverable
        // for a subject reported erased. Reachable with no concurrency at all, and a formal model refutes
        // the conditional form on exactly that trace at depth 7 while the unconditional form holds
        // exhaustively.
        //
        // The registry is now read in exactly ONE place -- by the erasure, which is the actor that owns
        // the legal decision and holds the request's own recorded owner. Two readers at two instants
        // cannot disagree when there is only one reader.
        if (retentionScope.IsInAnAggregate)
        {
            handle = handle.Retained(retentionScope.AggregateType!);
        }

        // WRITE-AHEAD OF THE MINT, AND THE ORDER IS THE WHOLE CONTRACT. Mint-then-record admits a crash
        // that leaves live key material with no row -- never enumerated, never destroyed, and a
        // certificate issued over it. Record-then-mint leaves a row with no key, which the erasure
        // handles: the provider answers not-found and nothing is destroyed.
        //
        // FAIL-CLOSED, deliberately, and this is the one place where the rule that cross-cutting
        // infrastructure fails OPEN is inapplicable. Failing open here mints a key that nothing will ever
        // enumerate, which is the catastrophic direction; a refused write is loud and recoverable. The
        // exception is therefore NOT caught.
        await _mintLedger.RecordMintIntentAsync(
            handle.Value,
            tenant,
            _hasher.HashDataSubjectId(subjectId),
            cancellationToken).ConfigureAwait(false);

        // MINTING IS NOT ROTATING, and that distinction is the whole of this call. This used to read the key
        // and then call RotateKeyAsync when it was absent: two steps with no atomicity between them, and a
        // second step that is create-OR-rotate. So two ORDINARY concurrent first writes for one new subject
        // were enough to do harm. The second writer read "absent", found the key present by the time it
        // wrote, took the rotate branch, and retired the version the first writer had just created. Nobody
        // asked for a rotation, and a subject the design gives one key acquired a version per racing write --
        // on a backend that rotates by minting a new key, one whole key apiece.
        //
        // CreateKeyIfAbsentAsync is a single operation whose lost race is a no-op yielding the winner's key,
        // and which never rotates. It also THROWS rather than reporting failure in a result, which repairs
        // the other half of the same defect: the discarded KeyRotationResult let a FAILED provisioning return
        // a key handle with no key behind it, so the real error surfaced later, somewhere else, as a missing
        // key. Key material comes from the provider's CSPRNG-backed backend, never from this adapter.
        // The provisioning reports the generation of the material now at this handle, and it is returned with
        // the handle rather than looked up afterwards. A second lookup could be overtaken -- a concurrent
        // erasure plus an ordinary write can replace the material between the two reads -- and the writer would
        // then bind a generation that does not match what it is about to encrypt under, which authenticates as
        // a corrupt payload at the next read.
        var keyId = handle.Value;

        var provisioned = await _keyProvider.CreateKeyIfAbsentAsync(
            keyId,
            EncryptionAlgorithm.Aes256Gcm,
            CryptoShredPurpose,
            cancellationToken).ConfigureAwait(false);

        // CONFIRM THE MINT, AND THIS IS THE STEP THAT MAKES THE WRITE-AHEAD ROW USEFUL TO AN ERASURE.
        //
        // The row above was written BEFORE the key existed, so it could not say whether the key was ever
        // created. Between those two moments a write is invisible to both observables an erasure has: the
        // ledger's handle set has not changed since the erasure read it, and the key material reads absent
        // because it genuinely is not there yet. That state is OBSERVATIONALLY IDENTICAL to a writer that
        // crashed between its row and its mint -- which is safe, and which an erasure correctly ignores. The
        // two diverge only after the erasure has signed. So no amount of re-reading the handle set and no
        // number of material probes can separate them; the row has to record which side of the mint it is
        // on, and this call is what records it.
        //
        // FAIL-OPEN, which is the opposite of the intent write above and deliberately so. The intent write
        // fails CLOSED because returning without a durable row would let a key exist that nothing
        // enumerates. This one fails OPEN because a row left pending is conservative in the direction that
        // matters: it withholds a clean certificate rather than granting one. Failing a consumer's
        // personal-data write because a confirmation could not be recorded would trade a real write for a
        // bookkeeping failure, and the key and the row both already exist.
        //
        // The cost of a lost confirmation is stated rather than hidden: erasures for this data subject
        // report a concurrent write until some later mint at the same handle confirms the row. That is
        // loud and recoverable, which is the trade this whole mechanism is making.
        try
        {
            await _mintLedger.MarkMintedAsync(keyId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMintConfirmationNotRecorded(keyId, ex);
        }

        return new SubjectKey(keyId, provisioned.Generation);
    }

    [LoggerMessage(
        ComplianceEventId.CryptoShreddingMintConfirmationNotRecorded,
        LogLevel.Error,
        "The key at handle {KeyHandle} was created but its mint confirmation could not be recorded, so the "
        + "ledger row stays pending. The write SUCCEEDED and the key and the row both exist; the consequence "
        + "is that erasures for this data subject report a concurrent write until a later mint at this handle "
        + "confirms the row. Failing the write instead would trade a real personal-data write for a "
        + "bookkeeping failure")]
    private partial void LogMintConfirmationNotRecorded(string keyHandle, Exception exception);
}
