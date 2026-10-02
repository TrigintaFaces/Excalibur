// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

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
internal sealed class SubjectKeyManager : ISubjectKeyManager
{
    private const string CryptoShredPurpose = "crypto-shred";

    private readonly IKeyManagementProvider _keyProvider;
    private readonly IDataSubjectHasher _hasher;
    private readonly IErasureRetentionRegistry _retentions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectKeyManager"/> class.
    /// </summary>
    /// <param name="keyProvider">The key-management provider used to look up and create per-subject keys.</param>
    /// <param name="hasher">The data-subject hasher used to derive stable key handles.</param>
    /// <param name="retentions">
    /// The declared erasure retentions. An EMPTY registry is how a deployment that declares none says so;
    /// an ABSENT one is a second answer to the same question, which is the defect.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>The registry is REQUIRED, and on a key handle that matters more than on a store.</b> A manager
    /// that could be built without one answers the retention question differently depending on whether a
    /// registration happened to supply it — and the two answers name different key handles. A handle is
    /// the name of the thing an erasure destroys, so one state writes under a handle the erasure spares and
    /// the other under a handle it destroys. Either way the stored record and the signed certificate
    /// disagree, and nothing reports it.
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
        IErasureRetentionRegistry retentions)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _retentions = retentions ?? throw new ArgumentNullException(nameof(retentions));
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

        // ONLY a DECLARED aggregate type moves the handle, and that asymmetry is the design rather than an
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
        // asymmetry is safe in exactly one direction, and it is this one.
        if (retentionScope.IsInAnAggregate && _retentions.IsRetainedForAnyTenant(retentionScope.AggregateType))
        {
            handle = handle.Retained(retentionScope.AggregateType!);
        }

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

        return new SubjectKey(keyId, provisioned.Generation);
    }
}
