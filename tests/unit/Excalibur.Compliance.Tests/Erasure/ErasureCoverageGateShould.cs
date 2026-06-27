// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
            .Returns(Task.FromResult(true));

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
            .Returns(Task.FromResult(true));

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
            AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
        };
        A.CallTo(() => _dataInventoryService.DiscoverAsync(
                A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(inventory));

        var sut = CreateService(CreateContributor("EventStore", DataStoreKind.EventStore, recordsAffected: 1));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Success.ShouldBeTrue();
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
            .Returns(Task.FromResult(true));

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
            new StubAnnotationSource(PersonalDataCategory.Health),
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
            .Returns(Task.FromResult(true));

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
            AssociatedKeys = [new KeyReference { KeyId = "key-1", KeyScope = EncryptionKeyScope.User }],
        };
        A.CallTo(() => _dataInventoryService.DiscoverAsync(
                A<string>._, DataSubjectIdType.Hash, A<string?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(inventory));

        var sut = CreateServiceWithAnnotations(
            new StubAnnotationSource(PersonalDataCategory.Identity),
            CreateContributor("EventStore", DataStoreKind.EventStore, recordsAffected: 1));

        // Act
        var result = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Success.ShouldBeTrue();
    }

    /// <summary>Deterministic in-test <see cref="IPersonalDataAnnotationSource"/> for the annotated-coverage gate.</summary>
    private sealed class StubAnnotationSource(params PersonalDataCategory[] categories) : IPersonalDataAnnotationSource
    {
        public IReadOnlySet<PersonalDataCategory> GetAnnotatedCategories() => new HashSet<PersonalDataCategory>(categories);
    }

    private ErasureService CreateServiceWithAnnotations(
        IPersonalDataAnnotationSource annotations,
        params IErasureContributor[] contributors)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ErasureOptions
        {
            Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
        });
        return new ErasureService(
            _store, _keyAdmin, options,
            NullLogger<ErasureService>.Instance,
            _legalHoldService, _dataInventoryService,
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
        return new ErasureService(
            _store, _keyAdmin, options,
            NullLogger<ErasureService>.Instance,
            _legalHoldService, _dataInventoryService,
            contributors);
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
