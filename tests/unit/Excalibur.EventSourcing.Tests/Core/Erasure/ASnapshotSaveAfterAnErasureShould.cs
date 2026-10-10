// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Compliance;
using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.Implementation;
using Excalibur.EventSourcing.InMemory;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Erasure;

/// <summary>
/// What an aggregate load returns when a snapshot save lands AFTER an erasure has already completed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this needs no concurrency.</b> The state these arms examine -- a live snapshot beside a
/// tombstoned event stream -- is the final state of an interleaving in which a save is in flight while an
/// erasure runs. That final state is also reachable in plain program order, because a snapshot store
/// creates a document when none exists: erase to completion, then save a payload built before the
/// erasure. So the arms are deterministic and use no lock, sleep, thread or poll; the interleaving is
/// only the route by which a production host arrives at the same state.
/// </para>
/// <para>
/// <b>Why the combination is the dangerous one.</b> A snapshot at count <c>N</c> over events
/// <c>0..N-1</c> makes the load compute <c>fromVersion = N-1</c> and the store filter
/// <c>Version &gt; fromVersion</c>, so zero event rows load. The tombstone check in the replay loop
/// never sees a row, the erased sentinel never returns, and the snapshot is applied and handed back.
/// </para>
/// <para>
/// <b>Scope.</b> The event store, the snapshot store and the erasure write path are the real in-memory
/// implementations; the contributor and the repository are the real shipped types. Only the
/// data-subject-to-aggregate mapping is a fake, because it is a lookup table and not the mechanism under
/// examination. The serializer is a fake that THROWS on any call, which is itself an assertion: the
/// resurrection load must reach its answer without reading an event row.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ASnapshotSaveAfterAnErasureShould : IDisposable
{
	// The repository resolves the store key from the aggregate's own AggregateType, so this constant is
	// derived from the type rather than written out: a literal that drifted from the type name would send
	// every append to one key and every load to another, and the arms would report an erasure that erased
	// nothing.
	private const string AggregateTypeName = nameof(ErasureSubjectAggregate);

	private const string SubjectEmail = "erika.mustermann@example.invalid";
	private const string SubjectName = "Erika Mustermann";

	private readonly InMemoryEventStore _eventStore = new(UntenantedContext.Instance);

	private readonly InMemorySnapshotStore _snapshotStore = new(
		Options.Create(new InMemorySnapshotOptions()),
		NullLogger<InMemorySnapshotStore>.Instance,
		UntenantedContext.Instance);

	private readonly IAggregateDataSubjectMapping _mapping = A.Fake<IAggregateDataSubjectMapping>();

	// THE RESURRECTION ARM. Steps, in program order: persist three events, build and save the
	// pre-erasure snapshot, run the erasure to completion and assert it reports success, then save the
	// pre-erasure payload once more (the save that was already in flight), then load. The load must not
	// hand back the erased subject's personal data.
	[Fact]
	public async Task NotReturnTheErasedSubjectsDataWhenAPreErasureSnapshotIsSavedAfterTheErasure()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var preErasureSnapshot = await PersistSubjectAndBuildSnapshotAsync(aggregateId).ConfigureAwait(false);

		await _snapshotStore.SaveSnapshotAsync(preErasureSnapshot, CancellationToken.None).ConfigureAwait(false);

		// The erasure runs to COMPLETION. Everything after this is about a FINISHED erasure.
		var erasure = await RunErasureAsync(aggregateId).ConfigureAwait(false);
		erasure.Success.ShouldBeTrue(
			"these arms are about a completed erasure; a partial one gives the consumer a signal and is a "
			+ "different question");
		erasure.RecordsAffected.ShouldBe(3);
		(await _snapshotStore.GetLatestSnapshotAsync(aggregateId, AggregateTypeName, CancellationToken.None)
			.ConfigureAwait(false))
			.ShouldBeNull("the erasure must have destroyed the snapshot before tombstoning the events");

		// The save that was already in flight lands, carrying the payload it captured BEFORE the erasure.
		// The store creates a document because none exists.
		await _snapshotStore.SaveSnapshotAsync(preErasureSnapshot, CancellationToken.None).ConfigureAwait(false);

		var loaded = await CreateRepository().GetByIdAsync(aggregateId, CancellationToken.None).ConfigureAwait(false);

		// POSITIVE predicate, deliberately. "Not the pre-erasure state" is satisfied by a throw, by a
		// null and by any half-erased aggregate, so it would pass for reasons unrelated to the defect.
		// This asserts the loaded aggregate EQUALS the erased sentinel, field for field; and reaching the
		// assertion at all is the no-throw half, since a throwing load never gets here.
		_ = loaded.ShouldNotBeNull("a load that returned null would not be this defect -- it is a "
			+ "never-existed answer, and the arm must not accept it as an erasure");

		Observe(loaded).ShouldBe(
			ErasedState,
			"a completed erasure must leave the erased sentinel readable and nothing else");
	}

	// POSITIVE CONTROL, and it is the one control that cannot be skipped. It proves the snapshot this
	// fixture writes is genuinely ON the hydration path. Same fixture, same save, erasure OMITTED: the
	// load must return the FULL pre-erasure state, and it can only come from the snapshot, because the
	// serializer throws if a single event row is read.
	//
	// Why it earns its place even though the resurrection arm already implies it: the repository reaches
	// a snapshot store only through a NULLABLE ISnapshotManager. A fixture that forgot to wire it would
	// make the resurrection arm pass deterministically and forever, with the resurrected document sitting
	// in the store unread -- a green that looks like a refutation of a P0. This arm fails first in that
	// case and says why.
	[Fact]
	public async Task LoadTheFullPreErasureStateFromTheSnapshotWhenNoErasureRuns()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var preErasureSnapshot = await PersistSubjectAndBuildSnapshotAsync(aggregateId).ConfigureAwait(false);

		await _snapshotStore.SaveSnapshotAsync(preErasureSnapshot, CancellationToken.None).ConfigureAwait(false);

		var loaded = await CreateRepository().GetByIdAsync(aggregateId, CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull();
		Observe(loaded).ShouldBe(
			(3L, SubjectName, SubjectEmail, true),
			"the snapshot this fixture writes must be on the hydration path -- if this fails, the snapshot "
			+ "manager is not wired and EVERY other result in this class is void");
	}

	// THE PROBE ARM. The opt-in empty-tail probe is the one detection path a consumer can switch on, so
	// whether it sees this state is load-bearing for severity. The erasure tombstones rows in place and
	// does not touch Version, so the stream maximum still accounts for the snapshot and the probe's
	// comparison is expected to be false. This arm reads the two values the probe compares, so it names
	// WHY the probe answers as it does rather than only that it did.
	[Fact]
	public async Task RecordWhetherTheStreamReachesSnapshotProbeDetectsTheResurrection()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var preErasureSnapshot = await PersistSubjectAndBuildSnapshotAsync(aggregateId).ConfigureAwait(false);

		await _snapshotStore.SaveSnapshotAsync(preErasureSnapshot, CancellationToken.None).ConfigureAwait(false);
		(await RunErasureAsync(aggregateId).ConfigureAwait(false)).Success.ShouldBeTrue();
		await _snapshotStore.SaveSnapshotAsync(preErasureSnapshot, CancellationToken.None).ConfigureAwait(false);

		var maxVersion = await ((IEventStoreVersionProbe)_eventStore)
			.GetMaxVersionAsync(aggregateId, AggregateTypeName, CancellationToken.None).ConfigureAwait(false);
		maxVersion.ShouldBe(
			2L,
			"the erasure rewrites rows in place, so the stream's highest version survives it unchanged");
		preErasureSnapshot.Version.ShouldBe(3L);

		var repository = CreateRepository(verifyStreamReachesSnapshot: true);
		var loaded = await repository.GetByIdAsync(aggregateId, CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull(
			"with the probe on, a detected version hole would throw rather than return -- so reaching a "
			+ "non-null aggregate here is itself the evidence that the probe did not fire");
		Observe(loaded).ShouldBe(
			ErasedState,
			"with the probe ON -- the only detection path a consumer can switch on -- a completed erasure "
			+ "must still leave only the erased sentinel readable");
	}

	// LIVENESS. Without this, both arms above are satisfied by a repository that refuses every load and
	// by an erasure that destroys the snapshot and nothing else. A completed erasure with NO late save
	// must still return the erased sentinel rather than null or a throw, and the snapshot the other arms
	// re-save must genuinely have held the subject's personal data.
	[Fact]
	public async Task StillReturnTheErasedSentinelWhenNoLateSaveLands()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var preErasureSnapshot = await PersistSubjectAndBuildSnapshotAsync(aggregateId).ConfigureAwait(false);

		ReadState(preErasureSnapshot).Email.ShouldBe(
			SubjectEmail,
			"the payload the other arms re-save must really carry the subject's data, or they measure nothing");

		await _snapshotStore.SaveSnapshotAsync(preErasureSnapshot, CancellationToken.None).ConfigureAwait(false);
		(await RunErasureAsync(aggregateId).ConfigureAwait(false)).Success.ShouldBeTrue();

		var loaded = await CreateRepository().GetByIdAsync(aggregateId, CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull("an erased stream returns a defined sentinel, never null");
		Observe(loaded).ShouldBe(
			ErasedState,
			"erasure must be effective in THIS fixture -- if it is not, the resurrection arm is measuring "
			+ "an erasure that never happened");
	}

	// The erased sentinel, stated once: a fresh aggregate at version 0 holding none of the subject's
	// data. Every arm compares against this by equality rather than asserting a negation.
	private static (long Version, string? FullName, string? Email, bool Consent) ErasedState =>
		(0L, null, null, false);

	public void Dispose() => _snapshotStore.Dispose();

	private static (long Version, string? FullName, string? Email, bool Consent) Observe(
		ErasureSubjectAggregate aggregate) =>
		(aggregate.Version, aggregate.FullName, aggregate.EmailAddress, aggregate.ConsentRecorded);

	private static SubjectState ReadState(ISnapshot snapshot) =>
		JsonSerializer.Deserialize<SubjectState>(snapshot.Data.Span)!;

	private async Task<ISnapshot> PersistSubjectAndBuildSnapshotAsync(string aggregateId)
	{
		var events = new List<IDomainEvent>
		{
			new SubjectRegistered
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				Version = 0,
				FullName = SubjectName,
				Email = SubjectEmail,
			},
			new SubjectEmailChanged
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				Version = 1,
				Email = SubjectEmail,
			},
			new SubjectConsentRecorded
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				Version = 2,
			},
		};

		_ = await _eventStore.AppendAsync(
			aggregateId, AggregateTypeName, events, -1, CancellationToken.None).ConfigureAwait(false);

		var aggregate = new ErasureSubjectAggregate(aggregateId);
		aggregate.LoadFromHistory(events.Select((e, i) => new HistoricEvent(e, i)));
		aggregate.Version.ShouldBe(3L, "three events at versions 0..2 leave the aggregate at count 3");

		return aggregate.CreateSnapshot();
	}

	private async Task<ErasureContributorResult> RunErasureAsync(string aggregateId)
	{
		var context = new ErasureContributorContext
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hash-of-the-subject",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			TenantId = null,
		};

		_ = A.CallTo(() => _mapping.GetAggregatesForDataSubjectAsync(
				context.DataSubjectIdHash, context.TenantId, A<CancellationToken>._))
			.Returns(new List<AggregateReference> { new(aggregateId, AggregateTypeName) });

		var contributor = new EventStoreErasureContributor(
			_eventStore,
			_mapping,
			NullLogger<EventStoreErasureContributor>.Instance,
			_snapshotStore,
			serviceProvider: null,
			retentions: null);

		return await contributor.EraseAsync(context, CancellationToken.None).ConfigureAwait(false);
	}

	private EventSourcedRepository<ErasureSubjectAggregate> CreateRepository(
		bool verifyStreamReachesSnapshot = false)
	{
		// A serializer that throws on any call. The resurrection load must reach its answer from the
		// snapshot alone, having read zero event rows -- if it ever deserializes one, this throws and the
		// arm says so rather than quietly passing.
		var serializer = A.Fake<IEventSerializer>();
		_ = A.CallTo(() => serializer.ResolveType(A<string>._))
			.Throws(new InvalidOperationException("the load must not deserialize an event row"));

		return new EventSourcedRepository<ErasureSubjectAggregate>(
			_eventStore,
			serializer,
			id => new ErasureSubjectAggregate(id),
			Options.Create(new EventSourcedRepositoryOptions
			{
				VerifyStreamReachesSnapshot = verifyStreamReachesSnapshot,
			}),
			snapshotManager: new SnapshotStoreManager(_snapshotStore, AggregateTypeName));
	}

	private sealed record SubjectState(string? FullName, string? Email, bool ConsentRecorded);

	/// <summary>
	/// The repository reaches a snapshot store only through <see cref="ISnapshotManager"/>, and no
	/// implementation of that interface ships in this solution, so the suite supplies a pass-through. It
	/// adds no behaviour of its own: every call forwards to the real store under examination.
	/// </summary>
	private sealed class SnapshotStoreManager(ISnapshotStore store, string aggregateType) : ISnapshotManager
	{
		public Task<ISnapshot> CreateSnapshotAsync<TAggregate>(
			TAggregate aggregate,
			CancellationToken cancellationToken)
			where TAggregate : IAggregateRoot, IAggregateSnapshotSupport =>
			Task.FromResult(aggregate.CreateSnapshot());

		public Task SaveSnapshotAsync(string streamId, ISnapshot snapshot, CancellationToken cancellationToken) =>
			store.SaveSnapshotAsync(snapshot, cancellationToken).AsTask();

		public async Task<ISnapshot?> GetLatestSnapshotAsync(string streamId, CancellationToken cancellationToken) =>
			await store.GetLatestSnapshotAsync(streamId, aggregateType, cancellationToken).ConfigureAwait(false);

		public Task<TAggregate> RestoreFromSnapshotAsync<TAggregate>(
			ISnapshot snapshot,
			CancellationToken cancellationToken)
			where TAggregate : IAggregateRoot, IAggregateSnapshotSupport, new()
		{
			var aggregate = new TAggregate();
			aggregate.LoadFromSnapshot(snapshot);
			return Task.FromResult(aggregate);
		}
	}

	internal sealed class ErasureSubjectAggregate : AggregateRoot
	{
		public ErasureSubjectAggregate()
		{
		}

		public ErasureSubjectAggregate(string id) : base(id)
		{
		}

		public string? FullName { get; private set; }

		public string? EmailAddress { get; private set; }

		public bool ConsentRecorded { get; private set; }

		public override ISnapshot CreateSnapshot() =>
			Snapshot.Create(
				Id,
				Version,
				JsonSerializer.SerializeToUtf8Bytes(new SubjectState(FullName, EmailAddress, ConsentRecorded)),
				AggregateTypeName);

		protected override void ApplySnapshot(ISnapshot snapshot)
		{
			var state = ReadState(snapshot);
			FullName = state.FullName;
			EmailAddress = state.Email;
			ConsentRecorded = state.ConsentRecorded;
		}

		protected override bool ApplyEventInternal(IDomainEvent @event)
		{
			switch (@event)
			{
				case SubjectRegistered e:
					FullName = e.FullName;
					EmailAddress = e.Email;
					return true;

				case SubjectEmailChanged e:
					EmailAddress = e.Email;
					return true;

				case SubjectConsentRecorded:
					ConsentRecorded = true;
					return true;

				default:
					return false;
			}
		}
	}

	[MessageName("Test.Erasure.SubjectRegistered")]
	internal sealed class SubjectRegistered : IDomainEvent
	{
		public required string EventId { get; init; }

		public required string AggregateId { get; init; }

		public required long Version { get; init; }

		public required string FullName { get; init; }

		public required string Email { get; init; }

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	[MessageName("Test.Erasure.SubjectEmailChanged")]
	internal sealed class SubjectEmailChanged : IDomainEvent
	{
		public required string EventId { get; init; }

		public required string AggregateId { get; init; }

		public required long Version { get; init; }

		public required string Email { get; init; }

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	[MessageName("Test.Erasure.SubjectConsentRecorded")]
	internal sealed class SubjectConsentRecorded : IDomainEvent
	{
		public required string EventId { get; init; }

		public required string AggregateId { get; init; }

		public required long Version { get; init; }

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;

		public IDictionary<string, object>? Metadata { get; init; }
	}
}
