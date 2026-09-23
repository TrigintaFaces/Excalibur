// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Erasure;

using Excalibur.Compliance;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// bd-fot516 / bd-5c31jm (S840, AC-6 — CEO-blocking gate) — independent regression lock
/// (author≠impl, TestsDeveloper).
/// <para>
/// GDPR erasure (Art.17) must NOT report a fully-<c>Completed</c> outcome when the discovered data
/// inventory contains a location in an <b>Uncovered</b> store-kind — i.e. one that is neither
/// (a) crypto-shredded (its <see cref="DataLocation.KeyId"/> among the deleted keys), nor
/// (b) covered by a registered <see cref="IErasureContributor"/>, nor (c) a declared exemption
/// (ADR-336 Amendment 1/1a, key-aware coverage). Reporting Completed there silently leaves personal
/// data behind.
/// </para>
/// <para>
/// CEO blocking-gate requirement (<c>5c31jm</c>): this lock is non-vacuous on the <b>ABSENT-contributor</b>
/// case — a contributor-present→erases test would be vacuous. It is RED on the pre-gate-body code
/// (<c>ExecuteAsync</c> drives the outcome solely from <c>errors.Count</c> and never consults
/// <c>inventory.Locations</c> → an uncovered location adds no error → silent <c>Completed</c>) and GREEN
/// once the structural coverage gate lands (<c>Completed</c> reachable only when zero Uncovered locations).
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureCoverageGateShould
{
    private readonly IErasureStore _store = A.Fake<IErasureStore>();
    private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
    private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
    private readonly IDataInventoryService _dataInventoryService = A.Fake<IDataInventoryService>();

    [Fact]
    public async Task NotReportCompletedWhenInventoryHasAnUncoveredStoreLocation()
    {
        // Arrange — the data subject has personal data in the OUTBOX. It is genuinely UNCOVERED:
        //   • its KeyId ("outbox-default-key") is NOT in AssociatedKeys, so deleting the subject's
        //     keys does not crypto-shred it (outbox/inbox/projection decorators encrypt with the
        //     DEFAULT context, not a per-subject key — fot516 sweep + ADR-336 Amd1a), AND
        //   • no registered contributor declares coverage for DataStoreKind.Outbox, AND
        //   • Outbox is not a declared exemption.
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        var inventory = new DataInventory
        {
            DataSubjectId = "abc123hash",
            Locations =
            [
                new DataLocation
                {
                    StoreKind = DataStoreKind.Outbox,           // uncovered store-kind
                    TableName = "Outbox",
                    FieldName = "Payload",
                    DataCategory = "PII",
                    RecordId = "msg-1",
                    KeyId = "outbox-default-key",               // NOT a deleted per-subject key
                },
            ],
            AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
        };
        A.CallTo(() => _dataInventoryService.DiscoverAsync(
                A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(inventory));

        // Only an EventStore-covering contributor is registered — nothing covers Outbox.
        var sut = CreateService(CreateContributor("EventStore", DataStoreKind.EventStore, recordsAffected: 0));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert — the uncovered Outbox location MUST block a fully-Completed erasure (no Art.17 silent loss).
        result.Success.ShouldBeFalse(
            "an erasure leaving an uncovered store (Outbox) must NOT report success/Completed");
        A.CallTo(() => _store.RecordCompletionAsync(requestId, A<int>._, A<int>._, A<Guid>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ReportCompletedWhenAllInventoryLocationsAreCovered()
    {
        // Control — proves the gate keys on COVERAGE, not a blanket failure. Every discovered location's
        // store-kind is covered by a contributor (EventStore) and keys are deleted → erasure completes.
        // (GREEN both pre- and post-gate; guards against a vacuous "always-false" lock.)
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        var inventory = new DataInventory
        {
            DataSubjectId = "abc123hash",
            Locations =
            [
                new DataLocation
                {
                    StoreKind = DataStoreKind.EventStore,       // covered by the contributor below
                    TableName = "Events",
                    FieldName = "Data",
                    DataCategory = "PII",
                    RecordId = "evt-1",
                    KeyId = "key-1",
                },
            ],
            // A CONFIGURED host declares its personal-data locations. A fake returning an empty registry
            // models a MISCONFIGURED one, which is a different scenario and not this arm's subject.
            DeclaredLocations = [new DataLocationKey("Events", "Data")],
            AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
        };
        A.CallTo(() => _dataInventoryService.DiscoverAsync(
                A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(inventory));

        var sut = CreateService(
            CreateContributorDischarging(
                "EventStore", DataStoreKind.EventStore, new DataLocationKey("Events", "Data")));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task RefuseToCompleteWhenTheAnnotationScanWasNotEstablished()
    {
        // THE ARM THAT MATTERS, and it is deliberately the arm above with ONE variable changed: every
        // location is covered, every key is deleted, the registry declares its obligation and the
        // contributor discharges it. The only difference is that the annotation scan could not be
        // completed -- the state a trimmed or ahead-of-time host produces.
        //
        // Before the establishment flag existed, that scan returned an EMPTY category set, an empty set
        // produced an empty uncovered-annotated set, and the gate read the absence as coverage. The arm
        // above asserted SUCCESS on exactly that input shape, so the suite certified the fail-open state
        // as Completed. This arm is what makes that impossible.
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        var inventory = new DataInventory
        {
            DataSubjectId = "abc123hash",
            Locations =
            [
                new DataLocation
                {
                    StoreKind = DataStoreKind.EventStore,
                    TableName = "Events",
                    FieldName = "Data",
                    DataCategory = "PII",
                    RecordId = "evt-1",
                    KeyId = "key-1",
                },
            ],
            DeclaredLocations = [new DataLocationKey("Events", "Data")],
            AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
        };
        A.CallTo(() => _dataInventoryService.DiscoverAsync(
                A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(inventory));

        var sut = CreateServiceWithAnnotations(
            TestAnnotationSource.Unestablished,
            CreateContributorDischarging(
                "EventStore", DataStoreKind.EventStore, new DataLocationKey("Events", "Data")));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Success.ShouldBeFalse(
            "an unestablished annotation scan cannot show that annotated personal data was covered, and a "
            + "certificate issued on it attests to an absence of evidence rather than to an erasure");

        A.CallTo(() => _store.RecordCompletionAsync(
                requestId, A<int>._, A<int>._, A<Guid>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task StillCompleteOnAnUnestablishedScanWhenErasureIsCryptoShredOnly()
    {
        // THE LIVENESS PARTNER FOR THE ARM ABOVE, and it guards a regression the refusal would otherwise
        // introduce. A crypto-shred-only host establishes coverage by DESTROYING THE KEY, not by locating
        // annotated data — so whether the annotation scan ran is irrelevant to whether that host erased.
        // Refusing it would make erasure permanently uncompletable on every trimmed crypto-shred host,
        // which is a legitimate and explicitly supported configuration.
        //
        // The sibling location-side arm carries the same exemption for the same reason; this one exists so
        // the two cannot drift apart silently.
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        var sut = CreateServiceWithAnnotations(TestAnnotationSource.Unestablished, keyShredOnly: true);

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Success.ShouldBeTrue(
            "a crypto-shred-only erasure is established by key destruction, so an unestablished annotation "
            + "scan says nothing about whether it succeeded — refusing here would strand a supported "
            + "configuration on every trimmed host");
    }

    [Fact]
    public async Task NotReportCompletedWhenAnAnnotatedCategoryHasNoDiscoveredLocation()
    {
        // bd-vxp56x — an [PersonalData]-annotated category (Health) exists in the domain, but the discovered
        // inventory has NO location for it (the consumer annotated but never registered it). The discovered
        // location IS store-covered (EventStore contributor + crypto-shred), so ONLY the annotated-coverage
        // gate can block completion — isolating the new gate. RED on the pre-fix code (no annotation scan →
        // the annotated-but-unregistered Health data is silently skipped and the cert reports Completed).
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        var inventory = new DataInventory
        {
            DataSubjectId = "abc123hash",
            Locations =
            [
                new DataLocation
                {
                    StoreKind = DataStoreKind.EventStore,        // store-covered by the contributor below
                    TableName = "Events",
                    FieldName = "Data",
                    DataCategory = "Identity",                   // does NOT cover the annotated "Health"
                    RecordId = "evt-1",
                    KeyId = "key-1",                             // crypto-shredded
                },
            ],
            AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
        };
        A.CallTo(() => _dataInventoryService.DiscoverAsync(
                A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(inventory));

        var sut = CreateServiceWithAnnotations(
            TestAnnotationSource.With(PersonalDataCategory.Health),
            CreateContributor("EventStore", DataStoreKind.EventStore, recordsAffected: 1));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert — annotated Health data was never located ⇒ erasure must NOT report Completed.
        result.Success.ShouldBeFalse(
            "annotated personal data (Health) with no discovered location must block a Completed certificate");
        A.CallTo(() => _store.RecordCompletionAsync(requestId, A<int>._, A<int>._, A<Guid>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ReportCompletedWhenEveryAnnotatedCategoryHasACoveringLocation()
    {
        // Control (non-vacuous) — the annotated category (Identity) IS represented by a discovered, covered
        // location, so the annotated-coverage gate does not fire and erasure completes. Proves the gate keys
        // on the annotated-vs-covered MATCH, not a blanket failure.
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        var inventory = new DataInventory
        {
            DataSubjectId = "abc123hash",
            Locations =
            [
                new DataLocation
                {
                    StoreKind = DataStoreKind.EventStore,
                    TableName = "Events",
                    FieldName = "Data",
                    DataCategory = "Identity",                   // matches the annotated category
                    RecordId = "evt-1",
                    KeyId = "key-1",
                },
            ],
            // Configured host: see the note on the sibling arm above.
            DeclaredLocations = [new DataLocationKey("Events", "Data")],
            AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
        };
        A.CallTo(() => _dataInventoryService.DiscoverAsync(
                A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(inventory));

        var sut = CreateServiceWithAnnotations(
            TestAnnotationSource.With(PersonalDataCategory.Identity),
            CreateContributorDischarging(
                "EventStore", DataStoreKind.EventStore, new DataLocationKey("Events", "Data")));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Success.ShouldBeTrue();
    }

    // ----------------------------------------------------------------------------------------------
    // DECLARED-vs-DISCHARGED gate (kmqvwv). ProductManager's ruling of 2026-09-19 kept this arm and
    // withdrew its neighbour, and recorded the finding underneath the finding: the kept arm was
    // VACUOUS. `DeclaredLocations` was set by ZERO tests in the suite (control: `new DataInventory`
    // appears 6 times in ErasureServiceShould.cs alone), and `outstanding` is built by looping OVER
    // that list — so with it empty the gate could not fire in any test we had. A P0 remedy shipped
    // with nothing binding it.
    //
    // The contract these three arms bind, in falsifiable terms:
    //   a certificate reports Completed only when EVERY declared table-and-field pair for the subject
    //   was NAMED as discharged by a contributor; a contributor reporting success without naming
    //   pairs discharges nothing.
    //
    // Why "we found no rows" cannot be the test: that observation and "we never looked in it" are
    // indistinguishable from the discovered set alone, and only one of them is erasure. The gate
    // therefore judges what contributors SAY THEY ERASED, not what discovery happened to return.
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// SAFETY. A declared obligation that no contributor named is outstanding, so the certificate may
    /// not report Completed — even though the contributor returned Success and the discovered set was
    /// empty. RED against a gate that judges coverage from discovery instead of from what was named.
    /// </summary>
    [Fact]
    public async Task NotReportCompletedWhenADeclaredPairIsNotNamedByAnyContributor()
    {
        // Arrange — one REGISTERED obligation, and a contributor that succeeds while naming NOTHING.
        // Locations is empty on purpose: this is the "we found no rows" case, which must NOT discharge.
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        SetupInventoryDeclaring(new DataLocationKey("Users", "Email"));

        var sut = CreateService(CreateContributor("EventStore", DataStoreKind.EventStore, recordsAffected: 1));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert — refused, and the refusal NAMES the outstanding pair so an operator can act on it.
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Users.Email");
    }

    /// <summary>
    /// LIVENESS. The paired half of the arm above: when the declared pair IS named as discharged, the
    /// erasure completes. Without this, "never report Completed" would satisfy the safety arm and the
    /// gate would be indistinguishable from one that always refuses.
    /// </summary>
    [Fact]
    public async Task ReportCompletedWhenEveryDeclaredPairIsNamedByAContributor()
    {
        // Arrange — same single declared obligation, and a contributor that NAMES it.
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        SetupInventoryDeclaring(new DataLocationKey("Users", "Email"));

        var sut = CreateService(
            CreateContributorDischarging(
                "EventStore", DataStoreKind.EventStore, new DataLocationKey("Users", "Email")));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Success.ShouldBeTrue();
    }

    /// <summary>
    /// The pair-matching is CASE-INSENSITIVE, as <see cref="DataLocationKey.Comparer"/> states. A
    /// contributor reporting `users.email` against a registration of `Users.Email` has discharged it.
    /// Binding this because the failure mode is silent and expensive in the safe-looking direction:
    /// a regression to ordinal comparison would refuse erasures that were genuinely complete, and the
    /// refusal would read exactly like a real outstanding obligation.
    /// </summary>
    [Fact]
    public async Task TreatADischargedPairAsMatchingItsDeclarationIrrespectiveOfCasing()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        SetupScheduledRequest(requestId);
        A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)));

        SetupInventoryDeclaring(new DataLocationKey("Users", "Email"));

        var sut = CreateService(
            CreateContributorDischarging(
                "EventStore", DataStoreKind.EventStore, new DataLocationKey("users", "email")));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Success.ShouldBeTrue();
    }

    /// <summary>
    /// The fixture the suite did not have: an inventory whose DECLARED pairs are populated. Discovery
    /// returns no rows, which is deliberate — the declared-vs-discharged gate is about registered
    /// obligations, not about what discovery found.
    /// </summary>
    private void SetupInventoryDeclaring(params DataLocationKey[] declared)
    {
        var inventory = new DataInventory
        {
            DataSubjectId = "abc123hash",
            Locations = [],
            DeclaredLocations = declared,
            AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
        };
        A.CallTo(() => _dataInventoryService.DiscoverAsync(
                A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(inventory));
    }

    private static IErasureContributor CreateContributorDischarging(
        string name, DataStoreKind covers, params DataLocationKey[] discharged)
    {
        var contributor = A.Fake<IErasureContributor>();
        A.CallTo(() => contributor.Name).Returns(name);
        A.CallTo(() => contributor.CoveredStoreKinds).Returns(new HashSet<DataStoreKind> { covers });
        A.CallTo(() => contributor.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(ErasureContributorResult.Succeeded(1, discharged)));
        return contributor;
    }

    private ErasureService CreateServiceWithAnnotations(
        IPersonalDataAnnotationSource annotations,
        params IErasureContributor[] contributors) =>
        CreateServiceWithAnnotations(annotations, keyShredOnly: false, contributors);

    private ErasureService CreateServiceWithAnnotations(
        IPersonalDataAnnotationSource annotations,
        bool keyShredOnly,
        params IErasureContributor[] contributors)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
        {
            KeyShredOnlyErasure = keyShredOnly,
            Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
        });
        return new ErasureService(
            _store, _keyAdmin, options,
            NullLogger<ErasureService>.Instance,
            TestDataSubjectHasher.Instance,
            _legalHoldService, _dataInventoryService, null,
            annotations, contributors);
    }

    private static IErasureContributor CreateContributor(string name, DataStoreKind covers, int recordsAffected)
    {
        var contributor = A.Fake<IErasureContributor>();
        A.CallTo(() => contributor.Name).Returns(name);
        A.CallTo(() => contributor.CoveredStoreKinds).Returns(new HashSet<DataStoreKind> { covers });
        A.CallTo(() => contributor.EraseAsync(A<ErasureContributorContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(ErasureContributorResult.Succeeded(recordsAffected)));
        return contributor;
    }

    private ErasureService CreateService(params IErasureContributor[] contributors)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
        {
            Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
        });
        // Annotated coverage is pinned EMPTY: this arm's subject is store-level coverage, and the
        // production default would otherwise scan the whole test assembly. See TestAnnotationSource.
        return new ErasureService(
            _store, _keyAdmin, options,
            NullLogger<ErasureService>.Instance,
            TestDataSubjectHasher.Instance,
            _legalHoldService, _dataInventoryService, null,
            TestAnnotationSource.None, contributors);
    }

    private void SetupScheduledRequest(Guid requestId)
    {
        var status = new ErasureStatus
        {
            RequestId = requestId,
            DataSubjectIdHash = "abc123hash",
            IdType = DataSubjectIdType.UserId,
            Scope = ErasureScope.User,
            LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
            Status = ErasureRequestStatus.Scheduled,
            RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
            RequestedBy = "admin",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
            .Returns(Task.FromResult<ErasureStatus?>(status));
        A.CallTo(() => _store.UpdateStatusAsync(requestId, ErasureRequestStatus.InProgress, null, A<CancellationToken>._))
            .Returns(Task.FromResult(true));
        A.CallTo(() => _legalHoldService.CheckHoldsAsync(
                A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));
    }
}
