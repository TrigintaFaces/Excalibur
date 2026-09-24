// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Excalibur.Dispatch.Tests.Functional.Erasure;

/// <summary>
///     End-to-end arm for the <b>GDPR Article 17 erasure</b> guarantee, driven from the entry point a
///     consumer actually calls: a data subject's erasure request is submitted to
///     <see cref="IErasureService"/>, executed by the shipped scheduler, and the subject's events are
///     tombstoned in a real SQL Server event store — while another subject's events are untouched.
/// </summary>
/// <remarks>
///     <para>
///     <strong>What is new here, given erasure already has integration coverage.</strong> The existing arms
///     prove that the erasure <em>contributor</em>, invoked directly, tombstones the right rows. That is a
///     component test, and it structurally cannot observe whether anything ever invokes the contributor. The
///     defect class this bead names — an advertised capability that is registered, resolvable and never
///     reached — lives precisely in that gap. This arm therefore never touches
///     <see cref="IErasureContributor"/>: it submits a request, starts the hosted service, and reads the
///     database.
///     </para>
///     <para>
///     <strong>The whole chain is the shipped composition:</strong> <c>AddGdprErasure</c> +
///     <c>AddErasureScheduler</c> + <c>AddEventSourcing(b =&gt; b.UseEventStoreErasure&lt;…&gt;())</c> +
///     <c>AddSqlServerEventStore</c>. The scheduler is started through <see cref="IHostedService"/>, as a
///     host starts it. Nothing is invoked out of band.
///     </para>
///     <para>
///     <strong>The consumer's own pieces</strong> are the two the framework requires a deployment to
///     supply and could not invent: the hashing pepper, and the mapping from a data subject to the
///     aggregates that belong to it. The mapping here is the identity mapping used by the existing erasure
///     arms — the subject's hash IS the aggregate id — so the arm controls exactly which stream is erased.
///     </para>
///     <para>
///     <strong>Non-vacuity.</strong> The safety assertion (the subject's payloads are gone) is paired in
///     the same arm with a liveness assertion (another subject's payloads survive, and the request reaches
///     <c>Completed</c> rather than failing). An erasure that deleted everything, or one that never ran at
///     all, fails one of the two. The arm was additionally proven RED by severing the contributor
///     registration in place — see the bead's evidence.
///     </para>
/// </remarks>
[Collection(nameof(ErasureEventStoreCollection))]
[Trait("Category", "Functional")]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
[Trait("Feature", "GdprErasure")]
public sealed class GdprErasureE2EShould
{
    private const string AggregateType = "Order";

    // The two secrets a deployment supplies from its secret manager: the pseudonymization pepper and the
    // certificate signing key. Both are generated per host from a CSPRNG rather than written as literals —
    // a fixed secret-shaped constant in a tracked file is exactly what the repository's secret hygiene
    // forbids, and nothing here depends on their values, only on their presence.
    private static readonly string TestPepper = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly byte[] TestCertificateSigningKey = RandomNumberGenerator.GetBytes(32);

    private readonly ErasureEventStoreFixture _fixture;

    /// <summary>Initializes the arm with the shared SQL Server event store.</summary>
    /// <param name="fixture">The event-store fixture.</param>
    public GdprErasureE2EShould(ErasureEventStoreFixture fixture) => _fixture = fixture;

    /// <summary>
    ///     SAFETY + LIVENESS. A submitted erasure request runs to <c>Completed</c> through the shipped
    ///     scheduler, the requesting subject's event payloads are tombstoned in the real store, and a
    ///     different subject's events survive intact.
    /// </summary>
    /// <remarks>
    ///     The second subject is what makes the first assertion mean something. "No payloads survive for
    ///     this subject" is satisfied by an erasure that wiped the table, by a store that was never written
    ///     to, and by a query that matched nothing — all of which leave the guarantee unproven. The surviving
    ///     subject discriminates all three.
    /// </remarks>
    [Fact]
    public async Task TombstoneTheRequestingSubjectsEventsAndLeaveEveryOtherSubjectIntact()
    {
        await ReadyAsync().ConfigureAwait(false);

        var erasedSubject = "subject-erased-" + Guid.NewGuid().ToString("N");
        var retainedSubject = "subject-retained-" + Guid.NewGuid().ToString("N");

        await using var provider = BuildConsumerHost();

        // The aggregate ids are the subjects' own hashes, because that is what the consumer-supplied
        // mapping below resolves. Computing them through the registered hasher rather than restating the
        // algorithm keeps the arm honest about which stream belongs to which subject.
        var hasher = provider.GetRequiredService<IDataSubjectHasher>();
        var erasedAggregateId = hasher.HashDataSubjectId(erasedSubject);
        var retainedAggregateId = hasher.HashDataSubjectId(retainedSubject);

        await RegisterTheEventStoreAsADataLocationAsync(provider).ConfigureAwait(false);

        var eventStore = provider.GetRequiredKeyedService<IEventStore>("default");
        await SeedAsync(eventStore, erasedAggregateId).ConfigureAwait(false);
        await SeedAsync(eventStore, retainedAggregateId).ConfigureAwait(false);

        (await _fixture.SurvivingPayloadCountAsync(erasedAggregateId).ConfigureAwait(false))
            .ShouldBe(2, "precondition: the subject's events exist and are readable before the erasure");
        (await _fixture.SurvivingPayloadCountAsync(retainedAggregateId).ConfigureAwait(false))
            .ShouldBe(2, "precondition: the other subject's events exist too");

        // Act — the consumer's call. Nothing below reaches for the contributor, the event store's erasure
        // capability, or any internal executor.
        var erasureService = provider.GetRequiredService<IErasureService>();
        var submitted = await erasureService.RequestErasureAsync(
            new ErasureRequest
            {
                DataSubjectId = erasedSubject,
                IdType = DataSubjectIdType.UserId,
                LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
                RequestedBy = "functional-e2e",
                GracePeriodOverride = TimeSpan.Zero,
            },
            CancellationToken.None).ConfigureAwait(false);

        submitted.Status.ShouldBe(
            ErasureRequestStatus.Scheduled,
            "the request must be accepted and scheduled; a blocked or failed request would make everything " +
            "below vacuous");

        // Start the shipped scheduler exactly as a host does, and let it pick the request up.
        await using var scheduler = await StartSchedulerAsync(provider).ConfigureAwait(false);

        var status = await WaitForTerminalStatusAsync(erasureService, submitted.RequestId).ConfigureAwait(false);

        // LIVENESS — the request actually ran to completion. A PartiallyCompleted or Failed outcome here
        // would mean the chain reported a problem, and a still-Scheduled one would mean nothing ran.
        status.Status.ShouldBe(
            ErasureRequestStatus.Completed,
            $"the erasure must run to completion through the shipped scheduler. Status: {status.Status}; error: {status.ErrorMessage}");
        status.RecordsAffected.ShouldBe(
            2, "the completion must account for the two events it tombstoned — a completion reporting zero " +
               "records is the advertised-but-never-reached shape this arm exists to detect");

        // SAFETY — read from the engine: the subject's payloads are gone and the rows carry the framework's
        // tombstone marker. The stream itself is retained, which is the documented behaviour: the sequence
        // survives so other aggregates referencing it stay readable.
        (await _fixture.SurvivingPayloadCountAsync(erasedAggregateId).ConfigureAwait(false))
            .ShouldBe(0, "no payload of the erased subject may survive in the event store");
        (await _fixture.TombstonedRowCountAsync(erasedAggregateId).ConfigureAwait(false))
            .ShouldBe(2, "each erased row must carry the erasure tombstone marker, so an erased stream is " +
                         "recognisable as erased rather than as corrupt");
        (await _fixture.TotalRowCountAsync(erasedAggregateId).ConfigureAwait(false))
            .ShouldBe(2, "the stream's sequence is preserved; erasure nulls payloads rather than deleting rows");

        // LIVENESS — the erasure was targeted. Another subject's data is untouched.
        (await _fixture.SurvivingPayloadCountAsync(retainedAggregateId).ConfigureAwait(false))
            .ShouldBe(2, "a different data subject's events must be completely untouched by this erasure");
        (await _fixture.TombstonedRowCountAsync(retainedAggregateId).ConfigureAwait(false))
            .ShouldBe(0, "no other subject's row may be tombstoned");
    }

    /// <summary>
    ///     LIVENESS for the certificate: a completed erasure yields a compliance certificate, which is the
    ///     artifact a data subject or a regulator is actually shown.
    /// </summary>
    /// <remarks>
    ///     Separate from the arm above because it asserts a different obligation. Tombstoning the rows
    ///     discharges Article 17; being able to evidence it is what a consumer relies on when asked to prove
    ///     the erasure happened, and a chain that erased correctly but issued nothing would pass every
    ///     assertion above.
    /// </remarks>
    [Fact]
    public async Task IssueAComplianceCertificateForACompletedErasure()
    {
        await ReadyAsync().ConfigureAwait(false);

        var subject = "subject-cert-" + Guid.NewGuid().ToString("N");
        await using var provider = BuildConsumerHost();

        var aggregateId = provider.GetRequiredService<IDataSubjectHasher>().HashDataSubjectId(subject);
        await RegisterTheEventStoreAsADataLocationAsync(provider).ConfigureAwait(false);
        await SeedAsync(provider.GetRequiredKeyedService<IEventStore>("default"), aggregateId)
            .ConfigureAwait(false);

        var erasureService = provider.GetRequiredService<IErasureService>();
        var submitted = await erasureService.RequestErasureAsync(
            new ErasureRequest
            {
                DataSubjectId = subject,
                IdType = DataSubjectIdType.UserId,
                LegalBasis = ErasureLegalBasis.DataSubjectRequest,
                RequestedBy = "functional-e2e",
                GracePeriodOverride = TimeSpan.Zero,
            },
            CancellationToken.None).ConfigureAwait(false);

        await using var scheduler = await StartSchedulerAsync(provider).ConfigureAwait(false);
        var status = await WaitForTerminalStatusAsync(erasureService, submitted.RequestId).ConfigureAwait(false);
        status.Status.ShouldBe(
            ErasureRequestStatus.Completed, $"precondition: the erasure completes. Status: {status.Status}; error: {status.ErrorMessage}");

        var certificate = await erasureService
            .GenerateCertificateAsync(submitted.RequestId, CancellationToken.None)
            .ConfigureAwait(false);

        certificate.ShouldNotBeNull("a completed erasure must be evidenced by a certificate");
        certificate.Payload.RequestId.ShouldBe(
            submitted.RequestId, "the certificate must evidence the request that was actually executed");

        // The certificate must describe an erasure that actually happened. Asserting only that a certificate
        // EXISTS would be satisfied by one issued over an erasure that erased nothing — which is exactly the
        // defect this bead is about, one level further out: a compliance artifact attesting to work no one
        // did. Measured: with the event-store contributor unregistered, a certificate is still issued and
        // these two assertions are what go red.
        certificate.Payload.Summary.RecordsAffected.ShouldBe(
            2, "the certificate must attest to the two records actually erased, not to an empty erasure");
        certificate.Signature.ShouldNotBeNullOrWhiteSpace(
            "the certificate must be signed, or it is not tamper-evident evidence of anything");
    }

    /// <summary>
    ///     Registers the event store's payload column as a data location holding personal data, which is
    ///     the act a consumer performs and the only way a location enters the framework's inventory.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <strong>This is the path a real consumer takes, and it is deliberately not the opt-out.</strong>
    ///     <c>ErasureOptions.KeyShredOnlyErasure = true</c> would also let these arms reach a certificate,
    ///     by declaring that coverage rests on key destruction and no registry is consulted. That is a
    ///     legitimate configuration and it is not the story this arm tells: it would green the arm on the
    ///     one path where nothing has to be registered, discharged or accounted for, which is precisely
    ///     the accounting the certificate is evidence of.
    ///     </para>
    ///     <para>
    ///     A registration declares WHERE to look — the table and the columns that carry the subject
    ///     identifier and the encryption key. The annotation on a type could not supply it: it names a
    ///     property, not a table, so nothing derives a data location from one.
    ///     </para>
    /// </remarks>
    private Task RegisterTheEventStoreAsADataLocationAsync(IServiceProvider provider) =>
        provider.GetRequiredService<IDataInventoryService>().RegisterDataLocationAsync(
            new DataLocationRegistration
            {
                TableName = _fixture.TableName,
                FieldName = "EventData",
                DataCategory = nameof(PersonalDataCategory.General),
                DataSubjectIdColumn = "AggregateId",
                IdType = DataSubjectIdType.UserId,
                // The store kind routes this obligation to the contributor that covers it. Without it
                // the registration is Unknown, reaches no contributor, is discharged by nobody, and the
                // erasure correctly refuses to complete.
                StoreKind = DataStoreKind.EventStore,
                KeyIdColumn = "EncryptionKeyId",
                Description = "Event payloads written for a data subject's aggregate stream.",
            },
            CancellationToken.None);

    private static async Task SeedAsync(IEventStore store, string aggregateId) =>
        _ = await store.AppendAsync(
            aggregateId,
            AggregateType,
            [new OrderPlaced(aggregateId, 0), new OrderPlaced(aggregateId, 1)],
            -1,
            CancellationToken.None).ConfigureAwait(false);

    /// <summary>
    ///     Starts every hosted service the container registered — which is how the erasure scheduler runs in
    ///     a real host, and the reason this arm does not call any executor itself.
    /// </summary>
    private static async Task<HostedServiceScope> StartSchedulerAsync(IServiceProvider provider)
    {
        var hosted = provider.GetServices<IHostedService>().ToList();
        hosted.ShouldNotBeEmpty(
            "the erasure scheduler must be registered as a hosted service; without it nothing executes the " +
            "request and this arm would be asserting against a chain that never ran");

        foreach (var service in hosted)
        {
            await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        }

        return new HostedServiceScope(hosted);
    }

    /// <summary>Polls the request's own status until it leaves the pending states, or the budget expires.</summary>
    private static async Task<ErasureStatus> WaitForTerminalStatusAsync(
        IErasureService erasureService,
        Guid requestId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        ErasureStatus? status = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            status = await erasureService.GetStatusAsync(requestId, CancellationToken.None)
                .ConfigureAwait(false);

            if (status is not null
                && status.Status is not ErasureRequestStatus.Scheduled
                and not ErasureRequestStatus.Pending
                and not ErasureRequestStatus.InProgress)
            {
                return status;
            }

            await Task.Delay(100).ConfigureAwait(false); // delay-ok: poll pacing; the loop exits on a terminal status and throws TimeoutException on the budget
        }

        throw new TimeoutException(
            $"The erasure request did not reach a terminal status within the budget. Last seen: "
            + $"{status?.Status.ToString() ?? "no status"}.");
    }

    private async Task ReadyAsync()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "GDPR erasure is a legal obligation — this real-SQL-Server arm must never be skipped");
        await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
        await _fixture.CleanupAsync().ConfigureAwait(false);
    }

    /// <summary>The consumer's composition: GDPR erasure, its scheduler, and an erasure-capable event store.</summary>
    private ServiceProvider BuildConsumerHost()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(static b => b.SetMinimumLevel(LogLevel.Warning));

        // GDPR erasure, with the grace period allowed to be waived so the arm does not wait 72 hours.
        _ = services.AddGdprErasure(static o =>
        {
            o.AllowImmediateErasure = true;
            o.MinimumGracePeriod = TimeSpan.Zero;

            // A completion certificate is tamper-evident evidence, so erasure refuses to issue one without
            // an HMAC signing key. Supplying it is part of the deployment's composition.
            o.Retention.SigningKey = TestCertificateSigningKey;
        });

        // The pepper a deployment supplies from its secret manager; the hashing options fail closed without it.
        _ = services.Configure<DataSubjectHashingOptions>(static o => o.Pepper = TestPepper);

        // This host keeps its keys in memory, which erasure refuses by default because a crypto-shred over
        // keys that were lost on restart cannot be attested. Declaring it is the documented opt-in.
        _ = services.Configure<KeyDurabilityOptions>(static o => o.AllowVolatileKeyProvider = true);

        _ = services.AddInMemoryErasureStore();
        _ = services.AddNoLegalHolds();

        // The data-inventory discovery source. Erasure refuses to certify completion without one, because
        // the absence of discovered-uncovered stores is not proof of coverage when discovery never ran.
        _ = services.AddDataInventoryService();
        _ = services.AddInMemoryDataInventoryStore();

        // The shipped scheduler, polling fast enough for a test but through its real loop.
        _ = services.AddErasureScheduler(static o => o.PollingInterval = TimeSpan.FromMilliseconds(200));

        // The erasure-capable event store, on real SQL Server, registered through the public builder.
        _ = services.AddExcalibur(static x => x.AddEventSourcing(static b =>
            b.UseEventStoreErasure<HashIsAggregateIdMapping>()));
        _ = services.AddSqlServerEventStore(
            () => _fixture.CreateConnection(), _fixture.SchemaName, _fixture.TableName);

        // The default JSON event serializer refuses unregistered event types, so a host declares the ones
        // it stores. Declaring it is part of the consumer composition, not a test affordance.
        _ = services.AddEventTypes<OrderPlaced>();

        return services.BuildServiceProvider();
    }

    /// <summary>Stops the hosted services started for one arm.</summary>
    private sealed class HostedServiceScope(IReadOnlyList<IHostedService> services) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (var service in services)
            {
                try
                {
                    await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // A shutdown race must not mask the arm's own result.
                }
            }
        }
    }

    /// <summary>
    ///     The consumer-supplied mapping from a data subject to its aggregates. This deployment stores one
    ///     aggregate per subject, named by the subject's own hash.
    /// </summary>
    private sealed class HashIsAggregateIdMapping : IAggregateDataSubjectMapping
    {
        public Task<IReadOnlyList<AggregateReference>> GetAggregatesForDataSubjectAsync(
            string dataSubjectIdHash,
            string? tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AggregateReference>>(
                [new AggregateReference(dataSubjectIdHash, AggregateType)]);
    }

    /// <summary>The event this arm seeds. Its payload is what erasure must remove.</summary>
    [MessageName("Test.GdprErasureE2E.OrderPlaced")]
    private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();

        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

        public IDictionary<string, object>? Metadata { get; init; }
    }
}
