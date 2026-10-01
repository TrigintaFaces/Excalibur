// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.Projections;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Erasure;

/// <summary>
/// An aggregate type the deployment is legally obliged to keep must survive an erasure of the data
/// subject — whole, and named on the record that attests the erasure.
/// </summary>
/// <remarks>
/// <para>
/// <b>The case these arms are written from.</b> A customer asks to be erased. The controller holds a
/// vehicle sales record for that customer, which the tax code and product-recall traceability require
/// them to keep, and which is worthless without the buyer's identity. Erasing it is not compliance; it
/// is a different breach. So the Customer aggregate is destroyed and the SalesRecord is not.
/// </para>
/// <para>
/// <b>Why the two liveness arms below are not optional.</b> Every safety arm here is satisfiable by a
/// contributor that retains everything — and a contributor that retains everything is a total erasure
/// failure, which is the more dangerous direction. The undeclared-type arm and the no-registry arm are
/// what make retaining everything RED.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ARetainedAggregateSurvivesErasureShould
{
	private const string SubjectHash = "sha256-of-the-subject";
	private const string CustomerId = "customer-1";
	private const string CustomerType = "Customer";
	private const string SalesRecordId = "sales-1";
	private const string SalesRecordType = "SalesRecord";
	private const string Justification =
		"Vehicle sales records are kept for six years under the tax code's record-keeping requirement and "
		+ "for product-recall traceability.";

	private static readonly TimeSpan SixYears = TimeSpan.FromDays(365 * 6);

	private readonly IEventStoreErasure _erasure = A.Fake<IEventStoreErasure>();
	private readonly ISnapshotStore _snapshots = A.Fake<ISnapshotStore>();
	private readonly IAggregateDataSubjectMapping _mapping = A.Fake<IAggregateDataSubjectMapping>();

	/// <summary>
	/// SAFETY, and the operator's own case. The erased aggregate is tombstoned and the retained one is
	/// left entirely alone — no tombstone, no snapshot delete.
	/// </summary>
	/// <remarks>
	/// RED input: remove the retention branch from the contributor's loop, so
	/// <c>EraseEventsAsync</c> runs unconditionally. The SalesRecord assertion fails.
	/// </remarks>
	[Fact]
	public async Task Tombstone_the_erased_aggregate_and_leave_the_retained_one_untouched()
	{
		var context = ResolveBoth();

		var result = await CreateSut(RetainingSalesRecords()).EraseAsync(
			context, TestContext.Current.CancellationToken);

		A.CallTo(() => _erasure.EraseEventsAsync(CustomerId, CustomerType, A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _snapshots.DeleteSnapshotsAsync(CustomerId, CustomerType, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _erasure.EraseEventsAsync(SalesRecordId, SalesRecordType, A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _snapshots.DeleteSnapshotsAsync(SalesRecordId, SalesRecordType, A<CancellationToken>._))
			.MustNotHaveHappened();

		result.Success.ShouldBeTrue(
			"withholding destruction the law requires is not a failure of the erasure, and reporting it as "
			+ "one would leave every request against a retaining deployment permanently incompletable");
	}

	/// <summary>
	/// SAFETY. The retention reaches the result carrying the basis and the justification, so the certificate
	/// can name what was kept — and it does NOT carry a duration with no instant to measure it from.
	/// </summary>
	/// <remarks>
	/// <para>
	/// RED input: drop <c>retained</c> from the result the contributor returns (use the two-argument
	/// <c>Succeeded</c> overload). Every assertion below fails.
	/// </para>
	/// <para>
	/// <b>The period assertion was inverted, and its own stated reason is why.</b> It read
	/// <c>entry.RetentionPeriod.ShouldBe(SixYears)</c> because "a statutory retention ends, and the record
	/// says when" — but a bare duration does not say when. It leaves the reader to supply the instant, and
	/// the instant in front of them on the certificate is its own date, which restarts the clock at the
	/// moment the data subject asked to be erased and reads as a longer retention than the obligation gives.
	/// The declared period is fixed per aggregate type while the instant it runs from is a fact about the
	/// individual record, so this contributor is not in a position to state an end at all.
	/// </para>
	/// <para>
	/// RED input for the flipped assertion: copy the declaration's <c>RetentionPeriod</c> onto the entry
	/// again. The basis and justification assertions are untouched, so this arm still fails if the entry
	/// loses what it DOES establish.
	/// </para>
	/// <para>
	/// This is the arm that makes the defect detectable from outside. A certificate reporting a clean
	/// completion over data deliberately kept cannot be told from one over data destroyed, and nothing
	/// downstream ever learns which it was — silence, not severity, is what makes that catastrophic.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Name_the_retention_with_its_basis_and_justification_and_no_unanchored_period()
	{
		var context = ResolveBoth();

		var result = await CreateSut(RetainingSalesRecords()).EraseAsync(
			context, TestContext.Current.CancellationToken);

		var entry = result.RetainedData.ShouldHaveSingleItem();
		entry.DataCategory.ShouldBe(
			SalesRecordType, "the reader has to know WHICH aggregate type survived");
		entry.Basis.ShouldBe(LegalHoldBasis.LegalObligation);
		entry.RetentionPeriod.ShouldBeNull(
			"a statutory retention does end, and a bare duration does not say when: the reader would anchor "
			+ "it on the certificate's own date and read a retention longer than the obligation gives. This "
			+ "pass holds a period declared per aggregate type and no instant for THIS record");

		entry.Reason.Contains(Justification, StringComparison.Ordinal).ShouldBeTrue(
			"the basis alone names a ground, not an obligation \u2014 the declared justification has to reach "
			+ "the record");
		entry.Reason.Contains("did not tombstone", StringComparison.Ordinal).ShouldBeTrue(
			"and the requesting subject has to be told what this pass DID. A justification written about "
			+ "the record (\"sales records are kept for six years\") does not by itself say that THIS "
			+ "subject's record is one of the ones kept");
	}

	/// <summary>
	/// SAFETY. The entry states what THIS PASS established and does not assert that the retained data is
	/// still readable, because this pass never checked that.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The entry used to read "was not erased and lawfully persists there for the period stated". The second
	/// half is unverified. Personal fields written BEFORE the retention was declared sit under the data
	/// subject's own key handle, which the erasure destroys, so the record survives with those fields no
	/// longer decryptable — and the certificate would be attesting lawful persistence of data the same
	/// erasure had just made unrecoverable, on a signed document retained for years.
	/// </para>
	/// <para>
	/// It is not checked rather than checked-and-reported because it CANNOT be checked here: establishing it
	/// means deriving the retained key handle and asking the key store whether it exists, and this
	/// contributor has neither — the derivation is internal to the compliance package, which does not
	/// name this assembly as a friend, and no key provider is injected. So the claim is dropped rather than
	/// approximated.
	/// </para>
	/// <para>
	/// RED input: restore the withdrawn clause. The arm fails on the persistence claim while every other arm
	/// in this file, including the one asserting "not erased" is present, stays green — which is why
	/// that arm could not have caught this.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_claim_the_retained_data_is_still_readable()
	{
		var context = ResolveBoth();

		var result = await CreateSut(RetainingSalesRecords()).EraseAsync(
			context, TestContext.Current.CancellationToken);

		var entry = result.RetainedData.ShouldHaveSingleItem();

		entry.Reason.Contains("persists", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
			"lawful persistence is a legal claim about the data, and nothing here established it");

		entry.Reason.Contains("not erased", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
			"and neither is the shorter version. \"Personal data was not erased\" is a claim about the "
			+ "ERASURE, which destroyed this subject's own key handle before any contributor ran "
			+ "— for fields written before the declaration, that is the handle protecting them, so "
			+ "the record can survive with its personal data unreadable");

		entry.Reason.Contains("did not tombstone", StringComparison.Ordinal).ShouldBeTrue(
			"the arm must not be satisfiable by emptying the entry: the ACT this pass performed still has "
			+ "to be stated");
		entry.Reason.Contains(Justification, StringComparison.Ordinal).ShouldBeTrue(
			"together with the obligation the record is kept under");
	}

	/// <summary>
	/// SAFETY. Two instances of one retained type are reported once, not twice: the retention is a
	/// statement about the type.
	/// </summary>
	/// <remarks>RED input: move the reporting outside the de-duplicating set — two entries appear.</remarks>
	[Fact]
	public async Task Report_a_retained_type_once_however_many_of_its_aggregates_the_subject_has()
	{
		var context = Resolve(
			new AggregateReference(SalesRecordId, SalesRecordType),
			new AggregateReference("sales-2", SalesRecordType));

		var result = await CreateSut(RetainingSalesRecords()).EraseAsync(
			context, TestContext.Current.CancellationToken);

		_ = result.RetainedData.ShouldHaveSingleItem();

		result.Success.ShouldBeFalse(
			"every aggregate this subject has is of a retained type, so nothing was erased -- and a "
			+ "contributor that erased nothing discharges no registered obligation. Reporting success "
			+ "would name every declared pair as erased while the event store still holds the subject");
		result.DischargedLocations.ShouldBeEmpty();
	}

	/// <summary>
	/// SAFETY. A retention declared for another TENANT does not spare this tenant's aggregate.
	/// </summary>
	/// <remarks>
	/// RED input: drop the tenant from either registry lookup. One tenant then inherits another's statute,
	/// which is over-retention — the Article 17 breach in the other direction.
	/// </remarks>
	[Fact]
	public async Task Erase_an_aggregate_whose_retention_belongs_to_another_tenant()
	{
		var context = Resolve(new AggregateReference(SalesRecordId, SalesRecordType))
			with { TenantId = "tenant-b" };

		_ = await CreateSut(RetainingSalesRecords()).EraseAsync(
			context, TestContext.Current.CancellationToken);

		A.CallTo(() => _erasure.EraseEventsAsync(
				SalesRecordId, SalesRecordType, A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// SAFETY. A retention is still named when the same pass fails on an unrelated aggregate.
	/// </summary>
	/// <remarks>
	/// <para>
	/// RED input: return the one-argument <c>Failed</c> overload from the contributor's error path. The
	/// retention vanishes from the result.
	/// </para>
	/// <para>
	/// What a contributor KEPT is a fact about the store; whether the same pass also failed is a fact
	/// about the run. Letting the second erase the first makes the partial-erasure record — the one a
	/// controller reconciles by hand, and so the one that most needs the list — the only record that
	/// never names what survived. One briefly unreachable read model is enough to reach it.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Name_the_retention_even_when_the_pass_fails_on_another_aggregate()
	{
		var context = ResolveBoth();
		A.CallTo(() => _erasure.EraseEventsAsync(CustomerId, CustomerType, A<Guid>._, A<CancellationToken>._))
			.Throws(new TimeoutException("the event store is unreachable"));

		var result = await CreateSut(RetainingSalesRecords()).EraseAsync(
			context, TestContext.Current.CancellationToken);

		result.Success.ShouldBeFalse("the Customer aggregate was not erased, so the pass did not finish");

		var entry = result.RetainedData.ShouldHaveSingleItem(
			"the retention is a property of the store and survives a failure elsewhere in the pass");
		entry.DataCategory.ShouldBe(SalesRecordType);
		entry.Basis.ShouldBe(LegalHoldBasis.LegalObligation);
	}

	/// <summary>
	/// LIVENESS. An aggregate type with no declared retention is erased exactly as it was before
	/// retentions existed.
	/// </summary>
	/// <remarks>
	/// RED input: make the contributor skip unconditionally — the blanket-retain regression the safety
	/// arms above invite. Without this arm, retaining everything passes, and retaining everything is a
	/// total erasure failure.
	/// </remarks>
	[Fact]
	public async Task Still_erase_an_aggregate_type_that_declares_no_retention()
	{
		var context = ResolveBoth();

		var result = await CreateSut(RetainingSalesRecords()).EraseAsync(
			context, TestContext.Current.CancellationToken);

		A.CallTo(() => _erasure.EraseEventsAsync(CustomerId, CustomerType, A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		result.RecordsAffected.ShouldBe(
			3,
			"only the Customer's three events are erased; the retained aggregate contributes none");
	}

	/// <summary>
	/// LIVENESS. A deployment that declares nothing at all erases every mapped aggregate, and reports no
	/// retention.
	/// </summary>
	/// <remarks>RED input: treat a null registry as "retain" rather than "erase" — both arms below fail.</remarks>
	[Fact]
	public async Task Erase_every_aggregate_when_no_registry_is_supplied()
	{
		var context = ResolveBoth();

		var result = await CreateSut(retentions: null).EraseAsync(
			context, TestContext.Current.CancellationToken);

		A.CallTo(() => _erasure.EraseEventsAsync(CustomerId, CustomerType, A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _erasure.EraseEventsAsync(SalesRecordId, SalesRecordType, A<Guid>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		result.RetainedData.ShouldBeEmpty();
	}

	/// <summary>
	/// SAFETY, the read-model half. A retained aggregate's row in a persisted projection is not cleared —
	/// verified against the real builder-bound clear delegate and the real recovery service, not inferred
	/// from the tombstone assertion.
	/// </summary>
	/// <remarks>
	/// RED input: move the retention branch below <c>ClearProjectionsForAggregateAsync</c>, so the clear
	/// still runs for a retained reference. The retained row is replayed against a live stream, which for
	/// a retained aggregate rewrites it — and the arm names it.
	/// </remarks>
	[Fact]
	public async Task Leave_a_retained_aggregates_read_model_row_in_place()
	{
		var harness = new ProjectionHarness(RetainingSalesRecords());
		await harness.MaterializeAsync();

		harness.Store.Get(CustomerId)!.Name.ShouldBe(
			ProjectionHarness.PersonalData,
			"the arm is worthless unless the read model genuinely held the personal data first");
		harness.Store.Get(SalesRecordId)!.Name.ShouldBe(ProjectionHarness.PersonalData);

		harness.Store.Writes = 0;

		_ = await harness.Contributor.EraseAsync(harness.Context(), TestContext.Current.CancellationToken);

		harness.Store.Get(CustomerId)!.Name.ShouldBeNullOrEmpty(
			"the erased aggregate's read model must drop the subject's fields as part of the erasure");
		harness.Store.Get(SalesRecordId)!.Name.ShouldBe(
			ProjectionHarness.PersonalData,
			"the retained aggregate's read model must be left exactly as it was — clearing it would destroy "
			+ "the record the law requires kept, through a different store");
		harness.Store.Writes.ShouldBe(
			1,
			"exactly one projection row was rewritten: the erased one. A second write means the retained "
			+ "reference reached the clear");
	}

	// The REAL registry through the REAL registration, so an arm going green means the shipped lookup
	// answers this way -- a fake would answer whatever it was told, including for a declaration startup
	// validation would have refused.
	private static IErasureRetentionRegistry RetainingSalesRecords()
	{
		var services = new ServiceCollection();
		_ = services.AddErasureRetention(new ErasureRetention
		{
			AggregateType = SalesRecordType,
			TenantId = TenantScope.UntenantedSentinel,
			Basis = LegalHoldBasis.LegalObligation,
			Justification = Justification,
			RetentionPeriod = SixYears,
		});

		return services.BuildServiceProvider().GetRequiredService<IErasureRetentionRegistry>();
	}

	private EventStoreErasureContributor CreateSut(IErasureRetentionRegistry? retentions) =>
		new(_erasure,
			_mapping,
			NullLogger<EventStoreErasureContributor>.Instance,
			_snapshots,
			serviceProvider: null,
			retentions);

	private ErasureContributorContext ResolveBoth() => Resolve(
		new AggregateReference(CustomerId, CustomerType),
		new AggregateReference(SalesRecordId, SalesRecordType));

	private ErasureContributorContext Resolve(params AggregateReference[] references)
	{
		var context = new ErasureContributorContext
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = SubjectHash,
			IdType = DataSubjectIdType.Hash,
			Scope = ErasureScope.User,
		};

		A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(
				SubjectHash, A<string?>._, A<CancellationToken>._))
			.Returns(references.ToList());

		A.CallTo(() => _erasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => _erasure.EraseEventsAsync(
				CustomerId, CustomerType, A<Guid>._, A<CancellationToken>._))
			.Returns(3);
		A.CallTo(() => _erasure.EraseEventsAsync(
				A<string>._, SalesRecordType, A<Guid>._, A<CancellationToken>._))
			.Returns(9);

		return context;
	}

	/// <summary>
	/// The real projection registry, the real builder-bound clear delegate and the real recovery service,
	/// over two aggregates of different types that both materialized the same read model.
	/// </summary>
	[SuppressMessage(
		"Performance",
		"CA1812:Avoid uninstantiated internal classes",
		Justification = "Instantiated by the read-model arm above.")]
	private sealed class ProjectionHarness
	{
		internal const string PersonalData = "Ada Lovelace";

		private readonly Dictionary<string, List<StoredEvent>> _streams = new(StringComparer.Ordinal)
		{
			[CustomerId] =
			[
				new StoredEvent("e1", CustomerId, CustomerType, "SubjectNamed",
					[1], null, 1, DateTimeOffset.UnixEpoch) { GlobalPosition = 1 },
			],
			[SalesRecordId] =
			[
				new StoredEvent("e2", SalesRecordId, SalesRecordType, "SubjectNamed",
					[1], null, 1, DateTimeOffset.UnixEpoch) { GlobalPosition = 2 },
			],
		};

		public ProjectionHarness(IErasureRetentionRegistry retentions)
		{
			var services = new ServiceCollection();
			var registry = new InMemoryProjectionRegistry();
			_ = services.AddSingleton<IProjectionRegistry>(registry);
			_ = services.AddSingleton<IProjectionStore<SubjectView>>(Store);

			var eventStore = A.Fake<IEventStore>();
			A.CallTo(() => eventStore.LoadAsync(A<string>._, A<string>._, A<CancellationToken>._))
				.ReturnsLazily((string id, string _, CancellationToken _) =>
					new ValueTask<IReadOnlyList<StoredEvent>>(_streams[id].ToList()));

			var serializer = A.Fake<IEventSerializer>();
			A.CallTo(() => serializer.ResolveType("SubjectNamed")).Returns(typeof(SubjectNamed));
			A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(SubjectNamed)))
				.Returns(new SubjectNamed { Name = PersonalData });

			Provider = services.BuildServiceProvider();
			_ = services.AddSingleton<IProjectionRecovery>(new ProjectionRecoveryService(
				registry, eventStore, serializer, Provider, NullLogger<ProjectionRecoveryService>.Instance));
			Provider = services.BuildServiceProvider();

			var builder = new ProjectionBuilder<SubjectView>(registry);
			_ = builder.Inline().When<SubjectNamed>(static (p, e) => p.Name = e.Name);
			builder.Build(registry);

			var erasure = A.Fake<IEventStoreErasure>();
			A.CallTo(() => erasure.IsErasedAsync(A<string>._, A<string>._, A<CancellationToken>._))
				.Returns(false);
			A.CallTo(() => erasure.EraseEventsAsync(
					A<string>._, A<string>._, A<Guid>._, A<CancellationToken>._))
				.ReturnsLazily((string id, string _, Guid _, CancellationToken _) =>
				{
					// Tombstone in place, exactly as a provider's erase statement does: the payload is
					// nulled and the type is rewritten to the reserved marker.
					var stream = _streams[id];
					for (var i = 0; i < stream.Count; i++)
					{
						stream[i] = stream[i] with
						{
							EventType = ErasedEventMarker.EventType,
							EventData = null,
						};
					}

					return stream.Count;
				});

			var mapping = A.Fake<IAggregateDataSubjectMapping>();
			A.CallTo(() => mapping.GetAggregatesForDataSubjectAsync(
					SubjectHash, A<string?>._, A<CancellationToken>._))
				.Returns(new List<AggregateReference>
				{
					new(CustomerId, CustomerType),
					new(SalesRecordId, SalesRecordType),
				});

			Contributor = new EventStoreErasureContributor(
				erasure, mapping, NullLogger<EventStoreErasureContributor>.Instance,
				snapshotStore: null, Provider, retentions);
		}

		public RecordingStore Store { get; } = new();

		public IServiceProvider Provider { get; private set; }

		public EventStoreErasureContributor Contributor { get; }

		public ErasureContributorContext Context() =>
			new()
			{
				RequestId = Guid.NewGuid(),
				DataSubjectIdHash = SubjectHash,
				IdType = DataSubjectIdType.Hash,
				Scope = ErasureScope.User,
			};

		public async Task MaterializeAsync()
		{
			var recovery = Provider.GetRequiredService<IProjectionRecovery>();
			await recovery.ReapplyAsync<SubjectView>(
				CustomerId, CustomerType, TestContext.Current.CancellationToken);
			await recovery.ReapplyAsync<SubjectView>(
				SalesRecordId, SalesRecordType, TestContext.Current.CancellationToken);
		}
	}

	private sealed class SubjectView
	{
		public string? Name { get; set; }
	}

	[MessageName("Test.ARetainedAggregateSurvivesErasure.SubjectNamed")]
	private sealed record SubjectNamed : DomainEvent
	{
		public string Name { get; init; } = string.Empty;
	}

	private sealed class RecordingStore : IProjectionStore<SubjectView>
	{
		private readonly Dictionary<string, SubjectView> _rows = new(StringComparer.Ordinal);

		public int Writes { get; set; }

		public SubjectView? Get(string id) => _rows.TryGetValue(id, out var row) ? row : null;

		public Task<SubjectView?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			Task.FromResult(Get(id));

		public Task UpsertAsync(string id, SubjectView projection, CancellationToken cancellationToken)
		{
			Writes++;
			_rows[id] = projection;

			return Task.CompletedTask;
		}

		public Task DeleteAsync(string id, CancellationToken cancellationToken)
		{
			_ = _rows.Remove(id);

			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<SubjectView>> QueryAsync(
			IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<SubjectView>>([.. _rows.Values]);

		public Task<long> CountAsync(
			IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			Task.FromResult<long>(_rows.Count);
	}
}
