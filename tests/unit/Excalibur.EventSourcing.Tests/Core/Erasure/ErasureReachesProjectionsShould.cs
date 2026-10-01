// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// An erasure must reach the read models that already materialized the subject, with no manual rebuild.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> Erasure tombstoned the event rows in place and notified nothing. The remedy —
/// <see cref="IProjectionRecovery.ReapplyAsync{TProjection}"/> — was fully built and registered
/// automatically on every host with a projection, and had ZERO production callers. So the event store
/// was compliant, every read model was not, and no consumer could detect the divergence from outside.
/// </para>
/// <para>
/// <b>Non-vacuity, and the shape of the mutant that matters.</b> Sever the wiring and leave every type
/// present: unbind the clear delegate in <c>ProjectionBuilder.Build</c>, or drop the
/// <c>ClearProjectionsForAggregateAsync</c> call from the contributor's loop, and
/// <see cref="Clear_the_erased_subject_from_a_projection_that_already_materialized_it"/> goes RED on the
/// PAYLOAD assertion — not on a "was it called" assertion, which is the arm a presence-measuring lock
/// would have written. Widen the bind condition to include keyed projections and
/// <see cref="Refuse_to_clear_a_keyed_projection_and_name_it_instead"/> goes RED.
/// </para>
/// <para>
/// <b>Scope, stated rather than implied.</b> The projection store here records no positions, so the
/// write takes the unconditional branch. The position-conditional branches an erasure reaches on a
/// positioned store — re-fold at the row's own position, and the refusal of a row carrying no usable
/// position — are covered by <c>ErasedProjectionRecoveryShould</c>. These arms bind the WIRING: that a
/// tombstoned aggregate reaches a read model at all, through the real builder-bound delegate and the
/// real contributor.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasureReachesProjectionsShould
{
	private const string AggregateId = "customer-1";
	private const string AggregateType = "Customer";
	private const string SubjectHash = "sha256-of-the-subject";
	private const string PersonalData = "Ada Lovelace";

	/// <summary>SAFETY: the criterion on the bead — erase, and the read model no longer carries the payload.</summary>
	[Fact]
	public async Task Clear_the_erased_subject_from_a_projection_that_already_materialized_it()
	{
		var harness = new Harness(keyed: false);

		// Materialize the read model from the LIVE events, through the real fold and the real store write,
		// so the row under test was built by the framework rather than hand-seeded into the shape the
		// assertion wants.
		await harness.MaterializeAsync();

		harness.Store.Get(AggregateId).ShouldNotBeNull();
		harness.Store.Get(AggregateId)!.Name.ShouldBe(
			PersonalData,
			"the arm is worthless unless the read model genuinely held the personal data first");

		var result = await harness.Contributor.EraseAsync(
			harness.Context(), TestContext.Current.CancellationToken);

		harness.Store.Get(AggregateId)!.Name.ShouldBeNullOrEmpty(
			"the whole bead: a projection that already materialized the subject must drop the erased "
			+ "fields as part of the erasure, with NO manual rebuild in between. This is the assertion a "
			+ "severed wiring fails");

		result.Success.ShouldBeTrue(
			"every persisted projection was reached, so event-store erasure has nothing outstanding to "
			+ "report");
	}

	/// <summary>SAFETY: a keyed projection is not cleared, and is NAMED rather than counted.</summary>
	/// <remarks>
	/// A keyed row is fed by many aggregates, so replaying one aggregate could not produce a correct row
	/// for such a key. The honest outcome is a partial erasure naming the read models a controller still
	/// has to deal with by hand — a count tells them nothing actionable.
	/// </remarks>
	[Fact]
	public async Task Refuse_to_clear_a_keyed_projection_and_name_it_instead()
	{
		var harness = new Harness(keyed: true);
		await harness.SeedAsync(new Customer { Name = PersonalData });

		_ = await harness.Contributor.EraseAsync(harness.Context(), TestContext.Current.CancellationToken);

		harness.Store.Get(AggregateId)!.Name.ShouldBe(
			PersonalData,
			"a keyed projection must NOT be written by a per-aggregate replay: the row is missing every "
			+ "other aggregate that feeds the key, so writing it would destroy their contribution");

		var gap = await new ProjectionErasureGapContributor(harness.Provider)
			.EraseAsync(harness.Context(), TestContext.Current.CancellationToken);

		gap.Success.ShouldBeFalse("a read model still holds the subject, so the erasure is partial");
		gap.ErrorMessage.ShouldNotBeNull();
		gap.ErrorMessage!.Contains(nameof(Customer), StringComparison.Ordinal).ShouldBeTrue(
			"the report must NAME the projections still holding the subject. A controller discharging the "
			+ "request has to deal with each one by hand, and a count is not actionable");
	}

	/// <summary>LIVENESS: a projection that never materialized this aggregate is not written to.</summary>
	/// <remarks>
	/// Recovery CREATES a row when none exists — its primary documented job. Invoking it unconditionally
	/// would materialize an empty row in every persisted projection that never held the aggregate, so a
	/// consumer's read would start returning a default projection where it returned null. That is a
	/// read-model change caused by erasing an unrelated subject.
	/// </remarks>
	[Fact]
	public async Task Leave_a_projection_that_never_held_the_subject_without_a_row()
	{
		var harness = new Harness(keyed: false);

		_ = await harness.Contributor.EraseAsync(harness.Context(), TestContext.Current.CancellationToken);

		harness.Store.Get(AggregateId).ShouldBeNull(
			"erasing a subject must not CREATE a row in a read model that never carried it");
		harness.Store.Writes.ShouldBe(
			0,
			"and it must not have written at all -- the replay is skipped entirely for a projection that "
			+ "never saw the subject");
	}

	/// <summary>LIVENESS: one projection's fault must not leave the others holding the subject.</summary>
	/// <remarks>
	/// Contributors run sequentially and each result is kept, so a throw escaping the per-aggregate loop
	/// would abandon every projection after the faulting one. The failure is recorded instead — after the
	/// tombstone there is no way for a clear to fail BECAUSE the payload is gone (the replay folds
	/// nothing and writes the empty state), so a failure here means a read model may still hold the
	/// subject and the framework cannot establish otherwise.
	/// </remarks>
	[Fact]
	public async Task Report_a_failing_projection_without_abandoning_the_others()
	{
		var harness = new Harness(keyed: false, faultFirstProjection: true);
		await harness.MaterializeAsync();

		var result = await harness.Contributor.EraseAsync(
			harness.Context(), TestContext.Current.CancellationToken);

		harness.Store.Get(AggregateId)!.Name.ShouldBeNullOrEmpty(
			"the second projection must still be cleared after the first one faulted");

		result.Success.ShouldBeFalse(
			"a read model may still hold the subject and we cannot establish otherwise, so this must reach "
			+ "the certificate as a failure rather than a log line");
		result.ErrorMessage.ShouldNotBeNull();
		result.ErrorMessage!.Contains(nameof(Faulting), StringComparison.Ordinal).ShouldBeTrue(
			"the operator has to know WHICH read model was not cleared");
	}

	private sealed class Harness
	{
		private readonly List<StoredEvent> _stream;

		public Harness(bool keyed, bool faultFirstProjection = false)
		{
			_stream =
			[
				new StoredEvent("e1", AggregateId, AggregateType, "CustomerNamed",
					[1], null, 1, DateTimeOffset.UnixEpoch) { GlobalPosition = 1 },
			];

			var services = new ServiceCollection();
			var registry = new InMemoryProjectionRegistry();
			_ = services.AddSingleton<IProjectionRegistry>(registry);
			_ = services.AddSingleton<IProjectionStore<Customer>>(Store);

			if (faultFirstProjection)
			{
				_ = services.AddSingleton<IProjectionStore<Faulting>>(new ThrowingStore());
			}

			var eventStore = A.Fake<IEventStore>();
			A.CallTo(() => eventStore.LoadAsync(AggregateId, AggregateType, A<CancellationToken>._))
				.ReturnsLazily(() => new ValueTask<IReadOnlyList<StoredEvent>>(_stream.ToList()));

			var serializer = A.Fake<IEventSerializer>();
			A.CallTo(() => serializer.ResolveType("CustomerNamed")).Returns(typeof(CustomerNamed));
			A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(CustomerNamed)))
				.Returns(new CustomerNamed { Name = PersonalData });

			Provider = services.BuildServiceProvider();

			// The REAL recovery service and the REAL registry, so the delegate the builder bound is the one
			// under test rather than a stand-in for it.
			_ = services.AddSingleton<IProjectionRecovery>(new ProjectionRecoveryService(
				registry, eventStore, serializer, Provider, NullLogger<ProjectionRecoveryService>.Instance));
			Provider = services.BuildServiceProvider();

			// Registered through the real builder: this is what binds (or declines to bind) the clear
			// delegate, and unbinding it there is the mutant these arms exist to catch.
			if (faultFirstProjection)
			{
				var faulting = new ProjectionBuilder<Faulting>(registry);
				_ = faulting.Inline().When<CustomerNamed>(static (p, e) => p.Name = e.Name);
				faulting.Build(registry);
			}

			var builder = new ProjectionBuilder<Customer>(registry);
			_ = builder.Inline().When<CustomerNamed>(static (p, e) => p.Name = e.Name);

			if (keyed)
			{
				_ = builder.KeyedBy<CustomerNamed>(static _ => "one-shared-row");
			}

			builder.Build(registry);

			var erasure = A.Fake<IEventStoreErasure>();
			A.CallTo(() => erasure.IsErasedAsync(AggregateId, AggregateType, A<CancellationToken>._))
				.Returns(false);
			A.CallTo(() => erasure.EraseEventsAsync(
					AggregateId, AggregateType, A<Guid>._, A<CancellationToken>._))
				.ReturnsLazily(() =>
				{
					// Tombstone IN PLACE, exactly as every provider's erase statement does: the payload is
					// nulled and the type is rewritten to the reserved marker. The position survives.
					var erased = _stream.Count;
					for (var i = 0; i < _stream.Count; i++)
					{
						_stream[i] = _stream[i] with
						{
							EventType = ErasedEventMarker.EventType,
							EventData = null,
						};
					}

					return erased;
				});

			IReadOnlyList<AggregateReference> aggregates = [new AggregateReference(AggregateId, AggregateType)];
			var mapping = A.Fake<IAggregateDataSubjectMapping>();
			A.CallTo(() => mapping.GetAggregatesForDataSubjectAsync(
					SubjectHash, A<string?>._, A<CancellationToken>._))
				.Returns(aggregates);

			Contributor = new EventStoreErasureContributor(
				erasure,
				mapping,
				NullLogger<EventStoreErasureContributor>.Instance,
				snapshotStore: null,
				Provider,
				retentions: null);
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

		/// <summary>Folds the live events into the read model through the real replay and store write.</summary>
		public async Task MaterializeAsync()
		{
			await Provider.GetRequiredService<IProjectionRecovery>()
				.ReapplyAsync<Customer>(AggregateId, AggregateType, TestContext.Current.CancellationToken);

			Store.Writes = 0;
		}

		/// <summary>Puts a row in place without a replay, for the keyed case a replay must never touch.</summary>
		public async Task SeedAsync(Customer state)
		{
			await Store.UpsertAsync(AggregateId, state, TestContext.Current.CancellationToken);
			Store.Writes = 0;
		}
	}

	private sealed class Customer
	{
		public string? Name { get; set; }
	}

	/// <summary>A second persisted projection whose store throws, to prove the loop accumulates.</summary>
	private sealed class Faulting
	{
		public string? Name { get; set; }
	}

	[MessageName("Test.ErasureReachesProjections.CustomerNamed")]
	private sealed record CustomerNamed : DomainEvent
	{
		public string Name { get; init; } = string.Empty;
	}

	private sealed class RecordingStore : IProjectionStore<Customer>
	{
		private readonly Dictionary<string, Customer> _rows = new(StringComparer.Ordinal);

		public int Writes { get; set; }

		public Customer? Get(string id) => _rows.TryGetValue(id, out var row) ? row : null;

		public Task<Customer?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			Task.FromResult(Get(id));

		public Task UpsertAsync(string id, Customer projection, CancellationToken cancellationToken)
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

		public Task<IReadOnlyList<Customer>> QueryAsync(
			IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Customer>>([.. _rows.Values]);

		public Task<long> CountAsync(
			IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			Task.FromResult<long>(_rows.Count);
	}

	private sealed class ThrowingStore : IProjectionStore<Faulting>
	{
		public Task<Faulting?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			Task.FromResult<Faulting?>(new Faulting { Name = PersonalData });

		public Task UpsertAsync(string id, Faulting projection, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("the read model store is unreachable");

		public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

		public Task<IReadOnlyList<Faulting>> QueryAsync(
			IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<Faulting>>([]);

		public Task<long> CountAsync(
			IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			Task.FromResult(0L);
	}
}
